using System;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 手调参数的 2D 平台跳跃控制器：包含加速度曲线、土狼时间、跳跃缓冲、
    /// 可变跳跃高度、二段跳、贴墙滑行/墙跳与冲刺。
    /// 使用旧版 Input Manager，与本项目已有的输入配置保持一致。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        // ---- 跑动 ----
        [Header("Run")]
        public float moveSpeed = 7.2f;      // 地面最大水平速度（米/秒）
        public float acceleration = 78f;    // 地面加速度给得很大，起步与转向要几乎即时
        public float airAcceleration = 52f; // 空中加速度更小，留一点惯性让落点可控
        public float friction = 82f;        // 松键后的减速，比加速度更大才不会出现“溜冰”手感

        // ---- 跳跃 ----
        [Header("Jump")]
        public float gravity = -48f;
        public float fallMultiplier = 1.35f; // 下落段重力放大，抛物线更利落、不拖沓
        public float maxFallSpeed = 27f;
        public float jumpVelocity = 18.4f;
        public float doubleJumpVelocity = 16.2f;
        public float coyoteTime = 0.11f;     // 土狼时间：刚离开地面仍允许起跳的宽限窗口
        public float jumpBufferTime = 0.12f; // 跳跃缓冲：落地前按下也算数，消除“明明按了却没跳”

        // ---- 贴墙 ----
        [Header("Wall")]
        public float wallSlideSpeed = -4.2f; // 用负值当上限，限制贴墙时的下落速度
        public float wallJumpPush = 11.5f;
        public float wallJumpUp = 17f;
        public float wallJumpLock = 0.16f;   // 墙跳后锁定水平输入的时长，防止玩家按着墙的方向被拉回墙面

        // ---- 冲刺 ----
        [Header("Dash")]
        public float dashSpeed = 21f;
        public float dashTime = 0.14f;       // 只为一次“瞬步”：0.14 秒 × 21 的速度，位移约 3 米
        public float dashCooldownTime = 0.55f;

        /// <summary>外部逻辑（过场、受击、死亡）可临时关闭玩家输入。</summary>
        public bool ControlEnabled { get; set; } = true;
        /// <summary>当前朝向：1 为右、-1 为左，动画翻转与击退方向都依赖它。</summary>
        public int Facing { get; private set; } = 1;
        /// <summary>本帧是否站在地面上，由脚下的 BoxCast 探测得出。</summary>
        public bool Grounded { get; private set; }
        /// <summary>本帧是否处于贴墙下滑状态。</summary>
        public bool WallSliding { get; private set; }
        /// <summary>冲刺是否仍在进行中。</summary>
        public bool Dashing => dashTimer > 0f;
        /// <summary>当前水平速度；rb 尚未缓存时返回 0。</summary>
        public float SpeedX => rb != null ? rb.velocity.x : 0f;
        /// <summary>当前垂直速度；rb 尚未缓存时返回 0。</summary>
        public float SpeedY => rb != null ? rb.velocity.y : 0f;
        /// <summary>脚下实际踩到的碰撞体，用于判断能不能下穿单向平台。</summary>
        public Collider2D GroundCollider { get; private set; }

        // ---- 事件：动画、音效、相机只订阅这里，避免控制器反向依赖表现层 ----
        public event Action Jumped;
        public event Action DoubleJumped;
        public event Action WallJumped;
        public event Action Landed;
        public event Action Dashed;

        // 物理完全由本脚本自行积分：Rigidbody2D 只负责碰撞与位移，gravityScale 保持 0。
        Rigidbody2D rb;
        BoxCollider2D box;
        Vector2 velocity;              // 自己维护的速度，每帧物理计算完后写回 rb.velocity
        int wallDirection;             // 贴墙方向：1 为右墙、-1 为左墙、0 为没贴墙
        float coyoteTimer;
        float jumpBufferTimer;
        float wallJumpLockTimer;
        float dashTimer;
        float dashCooldown;
        int dashDirection = 1;
        bool doubleJumpAvailable = true; // 落地或墙跳后恢复
        bool airDashAvailable = true;    // 空中冲刺额度，落地前只能用一次
        bool jumpHeldLastFrame;
        bool landingWasAirborne;
        float ghostTimer;              // 冲刺残影的生成间隔计时
        Coroutine dropRoutine;         // 下穿平台的协程句柄，重复触发时先取消上一次

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            rb.gravityScale = 0f; // 重力由本脚本按 fallMultiplier 手算，交给引擎会和手感参数打架
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate; // 插值消除摄像机跟随时的抖动
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 冲刺速度很快，必须连续检测防穿墙

            if (box.sharedMaterial == null)
            {
                // 用无摩擦材质：水平加减速全由脚本控制，贴墙时才不会被摩擦力“粘”住。
                box.sharedMaterial = new PhysicsMaterial2D("PlayerFrictionless")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }
        }

        void Update()
        {
            // 输入在 Update 采样、物理在 FixedUpdate 积分：低帧率时也不会漏掉按键。
            bool jumpPressed = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) ||
                               Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.JoystickButton0);
            bool jumpHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) ||
                            Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.JoystickButton0);
            bool dashPressed = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.J) ||
                               Input.GetKeyDown(KeyCode.JoystickButton5);
            bool downHeld = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            float horizontal = Input.GetAxisRaw("Horizontal");

            if (!ControlEnabled)
            {
                // 关闭控制时把输入清零而不是直接 return，计时器与朝向仍按正常流程收尾。
                horizontal = 0f;
                jumpPressed = false;
                jumpHeld = false;
                dashPressed = false;
            }

            if (Mathf.Abs(horizontal) > 0.1f && wallJumpLockTimer <= 0f)
                Facing = horizontal > 0f ? 1 : -1;

            if (jumpPressed)
            {
                // 站在单向平台上按住下再跳：改为下穿而非起跳，同时清掉缓冲，免得穿过后立刻弹起来。
                if (downHeld && Grounded && GroundCollider != null &&
                    GroundCollider.gameObject.layer == GameLayers.OneWay)
                {
                    DropThrough();
                    jumpBufferTimer = 0f;
                }
                else
                {
                    jumpBufferTimer = jumpBufferTime;
                }
            }

            // 跳跃削减：上升途中松键就把速度砍掉一半，实现“按住跳得高、轻点跳得矮”。
            if (jumpHeldLastFrame && !jumpHeld && velocity.y > 0f && dashTimer <= 0f)
                velocity.y *= 0.48f;
            jumpHeldLastFrame = jumpHeld;

            if (dashPressed && CanDash()) StartDash(horizontal);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (ControlEnabled)
            {
                // 计时器只在可控时推进：暂停期间冻结状态，恢复后不会突然触发早已过期的输入。
                jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
                coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);
                wallJumpLockTimer = Mathf.Max(0f, wallJumpLockTimer - dt);
                dashCooldown = Mathf.Max(0f, dashCooldown - dt);
            }

            CheckGround();
            CheckWall();

            if (dashTimer > 0f)
            {
                // 冲刺期间完全接管速度：水平恒定、垂直归零，做出“瞬步”的干脆感。
                dashTimer -= dt;
                velocity = new Vector2(dashDirection * dashSpeed, 0f);
                ghostTimer -= dt;
                if (ghostTimer <= 0f)
                {
                    ghostTimer = 0.035f; // 固定间隔出残影：密到能读出轨迹，又不至于每帧刷一个对象
                    PlayerGhost.Spawn(GetComponent<SpriteRenderer>());
                }
                if (dashTimer <= 0f)
                    velocity.x = dashDirection * moveSpeed * 0.7f; // 出冲刺时降速，避免像撞上一堵空气墙
            }
            else
            {
                if (jumpBufferTimer > 0f && ControlEnabled)
                {
                    // 优先级：地面/土狼时间 > 墙跳 > 二段跳，保证贴着墙按跳时先出墙跳。
                    if (Grounded || coyoteTimer > 0f)
                    {
                        DoJump();
                    }
                    else if (wallDirection != 0)
                    {
                        DoWallJump();
                    }
                    else if (doubleJumpAvailable)
                    {
                        DoDoubleJump();
                    }
                }

                float horizontal = ControlEnabled ? Input.GetAxisRaw("Horizontal") : 0f;
                // 墙跳锁定期间屏蔽水平输入，否则玩家按着墙的方向会立刻被拽回墙面。
                if (wallJumpLockTimer > 0f) horizontal = 0f;

                float target = horizontal * moveSpeed;
                float accel = Grounded ? acceleration : airAcceleration;
                // 用 MoveTowards 按固定加速度逼近目标速度：帧率无关、手感可预测，也方便调参。
                velocity.x = Mathf.MoveTowards(velocity.x, target, accel * dt);
                if (Mathf.Abs(horizontal) < 0.05f && Grounded)
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, friction * dt);

                // 滑墙要同时满足“按向墙面 + 正在下落”，向上蹭墙不算，避免玩家无伤爬墙。
                bool pushingIntoWall = wallDirection != 0 && horizontal * wallDirection > 0.1f;
                WallSliding = !Grounded && pushingIntoWall && velocity.y < 0f && !Dashing;

                float g = gravity * ((!Grounded && velocity.y < 0f) ? fallMultiplier : 1f);
                velocity.y += g * dt;
                if (Grounded && velocity.y < -2f) velocity.y = -2f; // 落地保留一点下压速度，防止离地判定抖动
                if (WallSliding) velocity.y = Mathf.Max(velocity.y, wallSlideSpeed);
                if (velocity.y > 0f && CeilingBlocked()) velocity.y = 0f; // 顶到天花板立刻清速，否则会贴着顶颤
                velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
            }

            // 记录“确实离过地”，落地事件才只在真正从空中落下时触发，而不是被贴地抖动刷屏。
            if (!Grounded && Mathf.Abs(velocity.y) > 0.1f) landingWasAirborne = true;

            rb.velocity = velocity;

            if (Grounded && landingWasAirborne && velocity.y <= 0.1f)
            {
                landingWasAirborne = false;
                Landed?.Invoke();
            }
        }

        void CheckGround()
        {
            // 起点抬到脚底上方一点，向下扫 0.16：既容忍地面接缝，又不会把半空中的重叠算成站地。
            var origin = (Vector2)transform.position + new Vector2(0f, 0.09f);
            var hit = Physics2D.BoxCast(origin, new Vector2(0.66f, 0.12f), 0f, Vector2.down, 0.16f, GameLayers.GroundMask);
            bool wasGrounded = Grounded;
            // 只在下落/静止时算站地，否则刚起跳的残留重叠会立刻把人抓回地面，跳不起来。
            Grounded = hit.collider != null && velocity.y <= 0.25f;
            GroundCollider = hit.collider;

            if (Grounded)
            {
                // 落地时刷新玩家资源：土狼时间、二段跳、空中冲刺都在这里重置（墙跳也会返还后两者）。
                coyoteTimer = coyoteTime;
                doubleJumpAvailable = true;
                airDashAvailable = true;
                if (!wasGrounded && landingWasAirborne)
                {
                    Fx.Dust(transform.position, 5, 1.8f);
                    landingWasAirborne = false;
                    Landed?.Invoke();
                }
            }
        }

        void CheckWall()
        {
            wallDirection = 0;
            if (Grounded) return; // 站地时不做贴墙检测，避免站在墙边被误判成滑墙

            // 用两个瘦长 BoxCast 而不是单条射线：判定有宽度，能容忍角色与墙面之间的细微缝隙。
            var origin = (Vector2)transform.position + new Vector2(0f, 0.95f);
            var size = new Vector2(0.12f, 1.25f);
            var right = Physics2D.BoxCast(origin, size, 0f, Vector2.right, 0.12f, GameLayers.WallMask);
            var left = Physics2D.BoxCast(origin, size, 0f, Vector2.left, 0.12f, GameLayers.WallMask);

            if (right.collider != null && left.collider == null) wallDirection = 1;
            else if (left.collider != null && right.collider == null) wallDirection = -1;
            // 两侧都碰到（窄缝）时按输入方向选墙，否则朝向会每帧来回抖。
            else if (right.collider != null && left.collider != null)
                wallDirection = Input.GetAxisRaw("Horizontal") >= 0f ? 1 : -1;
        }

        bool CeilingBlocked()
        {
            // 扁平的 BoxCast 探头顶一小段，比逐个接触点判断更稳定，也避免把斜坡当成天花板。
            var origin = (Vector2)transform.position + new Vector2(0f, 1.78f);
            return Physics2D.BoxCast(origin, new Vector2(0.62f, 0.1f), 0f, Vector2.up, 0.12f, GameLayers.GroundMask).collider != null;
        }

        bool CanDash()
        {
            if (dashCooldown > 0f || Dashing || !ControlEnabled) return false;
            // 地面冲刺不限次数，空中冲刺只能消耗一次，落地或墙跳后恢复。
            return Grounded || airDashAvailable;
        }

        void StartDash(float horizontal)
        {
            // 有输入就朝输入方向冲，没输入就朝当前朝向冲，保证站着不动也能冲刺。
            dashDirection = Mathf.Abs(horizontal) > 0.1f ? (horizontal > 0f ? 1 : -1) : Facing;
            Facing = dashDirection;
            dashTimer = dashTime;
            ghostTimer = 0f; // 归零让第一帧立刻生成残影，冲刺起手就有视觉反馈
            dashCooldown = dashCooldownTime;
            if (!Grounded) airDashAvailable = false; // 空中冲刺额度在这里扣除，地面冲刺不消耗
            GameManager.Instance?.Audio?.Play(Sfx.Dash);
            Fx.Dust(transform.position, 4, 1.2f);
            Dashed?.Invoke();
        }

        void DoJump()
        {
            jumpBufferTimer = 0f;
            coyoteTimer = 0f; // 立刻清空土狼时间，防止起跳后仍在宽限窗口内被判定为还能再跳一次
            velocity.y = jumpVelocity;
            Grounded = false;
            GameManager.Instance?.Audio?.Play(Sfx.Jump);
            Fx.Dust(transform.position, 5, 1.4f);
            Jumped?.Invoke();
        }

        void DoDoubleJump()
        {
            jumpBufferTimer = 0f;
            doubleJumpAvailable = false;
            velocity.y = doubleJumpVelocity;
            GameManager.Instance?.Audio?.Play(Sfx.DoubleJump);
            Fx.Sparkle(transform.position + Vector3.up * 0.3f, new Color(1f, 0.86f, 0.45f), 8);
            DoubleJumped?.Invoke();
        }

        void DoWallJump()
        {
            jumpBufferTimer = 0f;
            // 水平速度反向弹开、垂直给足上升量；随后锁定水平输入，防止玩家按着墙的方向把自己按回去。
            velocity.x = -wallDirection * wallJumpPush;
            velocity.y = wallJumpUp;
            wallJumpLockTimer = wallJumpLock;
            Facing = -wallDirection;
            doubleJumpAvailable = true;
            airDashAvailable = true;
            GameManager.Instance?.Audio?.Play(Sfx.Jump);
            Fx.Dust(transform.position, 6, 1.8f);
            WallJumped?.Invoke();
        }

        /// <summary>供弹簧与弹跳板调用：直接设定上升速度，并返还二段跳与空中冲刺。</summary>
        public void Bounce(float power)
        {
            velocity.y = power; // 直接覆盖而不是叠加，不同下落速度下弹起高度才一致
            Grounded = false;
            doubleJumpAvailable = true;
            airDashAvailable = true;
            jumpBufferTimer = 0f;
            GameManager.Instance?.Audio?.Play(Sfx.Bounce);
            Fx.Dust(transform.position, 7, 2.2f);
        }

        /// <summary>瞬移（重生、关卡切换）：同时同步 Transform 与 Rigidbody2D 的位置和速度。</summary>
        public void Teleport(Vector2 position)
        {
            transform.position = position;
            if (rb != null) rb.position = position; // 只改 Transform 会被物理引擎在下一帧覆盖回来
            velocity = Vector2.zero;
            Grounded = false;
            WallSliding = false;
            dashTimer = 0f;
            jumpBufferTimer = 0f;
            wallJumpLockTimer = 0f;
            if (rb != null) rb.velocity = Vector2.zero;
        }

        /// <summary>重生后清空速度与冲刺/跳跃相关计时器，并把二段跳、空中冲刺补满。</summary>
        public void ResetForRespawn()
        {
            velocity = Vector2.zero;
            dashTimer = 0f;
            dashCooldown = 0f;
            doubleJumpAvailable = true;
            airDashAvailable = true;
            jumpBufferTimer = 0f;
            wallJumpLockTimer = 0f;
            landingWasAirborne = false;
            if (rb != null) rb.velocity = Vector2.zero;
        }

        /// <summary>叠加一次外部冲量，用于击退、弹射等交互。</summary>
        public void AddVelocity(Vector2 impulse)
        {
            velocity += impulse;
            if (rb != null) rb.velocity = velocity; // 立即写回，避免本帧物理继续使用旧速度
        }

        /// <summary>开关玩家输入；关闭时清掉跳跃缓冲，防止恢复控制的瞬间自动起跳。</summary>
        public void SetControl(bool enabled)
        {
            ControlEnabled = enabled;
            if (!enabled)
            {
                jumpBufferTimer = 0f;
                jumpHeldLastFrame = false;
            }
            else
            {
                landingWasAirborne = false; // 恢复控制时清掉离地标记，避免补发一次落地事件
            }
        }

        /// <summary>立即清空所有动量（胜利、场景切换时使用）。</summary>
        public void Halt()
        {
            velocity = Vector2.zero;
            dashTimer = 0f;
            jumpBufferTimer = 0f;
            if (rb != null) rb.velocity = Vector2.zero;
        }

        /// <summary>跟随移动平台一起位移；直接改 position 而不是施力，平台加速度不会传给玩家。</summary>
        public void CarryBy(Vector2 delta)
        {
            if (rb != null) rb.position += delta;
            transform.position += (Vector3)delta;
        }

        void DropThrough()
        {
            // 重复触发时先停掉上一次协程，避免两条协程各恢复一次碰撞，把该穿透的瞬间又补了回来。
            if (dropRoutine != null) StopCoroutine(dropRoutine);
            dropRoutine = StartCoroutine(DropThroughRoutine());
        }

        System.Collections.IEnumerator DropThroughRoutine()
        {
            if (GroundCollider == null) yield break;
            // 整块平台的所有碰撞体都要忽略：只忽略脚下那一块，可能被平台其余部分卡住。
            var platform = GroundCollider.GetComponentInParent<Platform>();
            var platformColliders = platform != null
                ? platform.GetComponentsInChildren<Collider2D>()
                : new[] { GroundCollider };
            var own = GetComponents<Collider2D>();
            foreach (var platformCollider in platformColliders)
            {
                foreach (var ownCollider in own)
                    Physics2D.IgnoreCollision(platformCollider, ownCollider, true);
            }

            // 0.32 秒足够落回平台下方，又短到玩家察觉不出“卡了一下”；期间玩家仍可转向。
            yield return new WaitForSeconds(0.32f);

            foreach (var platformCollider in platformColliders)
            {
                if (platformCollider == null) continue; // 平台可能已被销毁，恢复碰撞前必须判空
                foreach (var ownCollider in own)
                {
                    if (ownCollider != null) Physics2D.IgnoreCollision(platformCollider, ownCollider, false);
                }
            }
            dropRoutine = null;
        }
    }

    /// <summary>冲刺时生成、随后逐渐淡出的残影。</summary>
    public sealed class PlayerGhost : MonoBehaviour
    {
        SpriteRenderer ghost;
        float life = 0.22f; // 存活时间略长于冲刺时长，拖尾才能自然消失而不突然闪断
        float age;

        /// <summary>按源渲染器复制一份半透明快照，不依赖预制体或额外美术配置。</summary>
        public static void Spawn(SpriteRenderer source)
        {
            if (source == null || source.sprite == null) return;
            var go = new GameObject("DashGhost");
            go.transform.position = source.transform.position;
            go.transform.localScale = source.transform.localScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.flipX = source.flipX;
            renderer.sortingOrder = source.sortingOrder - 1; // 压在本体身后一层，残影不能盖住玩家
            renderer.color = new Color(0.55f, 0.85f, 1f, 0.38f);
            go.AddComponent<PlayerGhost>();
        }

        void Awake() => ghost = GetComponent<SpriteRenderer>(); // 缓存渲染器，避免每帧 GetComponent

        void Update()
        {
            age += Time.deltaTime;
            if (ghost != null)
            {
                // 透明度按已存活比例线性插值，配合 life 归零即销毁，残影不会残留。
                var c = ghost.color;
                c.a = Mathf.Lerp(0.38f, 0f, age / life);
                ghost.color = c;
            }
            if (age >= life) Destroy(gameObject);
        }
    }
}
