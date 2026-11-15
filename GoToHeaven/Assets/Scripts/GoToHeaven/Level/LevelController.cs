using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 每个关卡 prefab 上都挂载的运行时控制器。世界内容（Tilemap 与各类道具）
    /// 都在 prefab 里手工摆放；本类只负责生成玩家、绑定场景实体、
    /// 计算相机边界，并掌管存档点 / 通关 / 死亡流程。
    /// </summary>
    public sealed class LevelController : MonoBehaviour
    {
        /// <summary>本关的数据配置（出生点、主题、死亡线等）。</summary>
        public LevelData Data { get; private set; }
        /// <summary>运行时生成的玩家控制器。</summary>
        public PlayerController Player { get; private set; }
        /// <summary>玩家生命组件，坠落判定与复活流程都要查它的状态。</summary>
        public PlayerHealth Health { get; private set; }
        /// <summary>当前复活点，激活存档点或复活后会被更新。</summary>
        public Vector2 SpawnPoint { get; private set; }
        /// <summary>关卡初始化时统计到的金币总数。</summary>
        public int TotalCoins { get; private set; }
        /// <summary>本局已拾取的金币数量。</summary>
        public int CollectedCoins { get; private set; }
        /// <summary>相机可活动的世界范围，由 BuildBounds 计算得出。</summary>
        public Rect Bounds { get; private set; }
        /// <summary>是否已通关，用来拦住重复触发的通关流程。</summary>
        public bool Completed { get; private set; }

        GameManager game;
        SpriteLibrary art;
        CameraFollow cameraFollow;

        /// <summary>
        /// 由 GameManager 在关卡实例化后调用：生成玩家、绑定场景内所有实体、算边界并架好相机。
        /// </summary>
        public void Initialize(LevelData data, GameManager owner)
        {
            Data = data;
            game = owner;
            art = owner.Art;

            // 出生点优先取 prefab 里手摆的 PlayerSpawn，缺失时才退回关卡数据中的坐标。
            var spawn = GetComponentInChildren<PlayerSpawn>(true);
            SpawnPoint = spawn != null ? (Vector2)spawn.transform.position : data.spawn.V;

            // 金币按遍历顺序编号，序号乘 0.7 作为浮动动画相位，让整排金币像波浪般起伏而非齐步上下。
            var coins = GetComponentsInChildren<Coin>(true);
            TotalCoins = coins.Length;
            for (int i = 0; i < coins.Length; i++) coins[i].Bind(this, i * 0.7f);

            // 交互实体统一在这里反向绑定到本控制器，省去各自 Find 的开销。
            foreach (var checkpoint in GetComponentsInChildren<Checkpoint>(true)) checkpoint.Bind(this);
            foreach (var goal in GetComponentsInChildren<Goal>(true)) goal.Bind(this);
            foreach (var trampoline in GetComponentsInChildren<Trampoline>(true)) trampoline.Bind(this);
            foreach (var saw in GetComponentsInChildren<SawTrap>(true)) saw.Bind();

            BuildPlayer();

            // 移动平台要拿玩家引用才能做跟随与摩擦传递，必须等玩家生成之后再绑定。
            foreach (var mover in GetComponentsInChildren<MovingPlatform>(true)) mover.Bind(Player);

            BuildBounds();
            SetupCamera();
            Backdrop.Create(owner.MainCamera, Data.theme, transform);
        }

        /// <summary>用代码搭出玩家角色：贴图、物理体、碰撞盒与脚下的影子。</summary>
        void BuildPlayer()
        {
            var go = new GameObject("Player");
            go.layer = GameLayers.Player;
            // 抬高 0.03 个单位，避免出生瞬间与地面严丝合缝而卡进碰撞体。
            go.transform.position = SpawnPoint + new Vector2(0f, 0.03f);

            var renderer = go.AddComponent<SpriteRenderer>();
            // 16f 是 PPU：每世界单位 16 像素，于是 32px 的格子正好占 2x2 个世界单位。
            var idleFrames = art.Frames(ArtPaths.PlayerIdle, 32, 32, 16f, SpriteLibrary.PlayerPivot);
            // 贴图缺失时退回纯白方块，保证关卡仍然可试玩。
            renderer.sprite = idleFrames.Length > 0 ? idleFrames[0] : ProceduralArt.White;
            renderer.sortingOrder = 10;

            var animator = go.AddComponent<SpriteAnimator>();
            animator.Renderer = renderer;

            // 位移完全由代码驱动，因此关掉重力并冻结旋转，防止撞墙后翻倒。
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            // 连续碰撞检测：高速下落时也不至于穿透薄地板。
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 碰撞盒 0.72 x 1.72，整体上移 0.88，让脚底大致落在物体原点处，贴地判定才好算。
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.72f, 1.72f);
            box.offset = new Vector2(0f, 0.88f);
            // 摩擦与弹性都为 0：贴墙时不会被摩擦力粘住，弹跳手感全部交给代码。
            box.sharedMaterial = new PhysicsMaterial2D("Player") { friction = 0f, bounciness = 0f };

            Player = go.AddComponent<PlayerController>();
            Health = go.AddComponent<PlayerHealth>();
            go.AddComponent<PlayerAnimator>();
            Health.Initialize(this);

            // 影子是玩家的子物体，排序层 9 让它垫在玩家（10）下方。
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            shadow.transform.localScale = new Vector3(0.8f, 0.7f, 1f);
            var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = ProceduralArt.Shadow();
            shadowRenderer.sortingOrder = 9;
        }

        /// <summary>
        /// 统计场景内所有 Renderer 与 Collider2D 的包围盒，得出相机活动范围。
        /// 注意：任何误放在远处的物体会被一起算进来，从而把相机范围撑得过大。
        /// </summary>
        void BuildBounds()
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            bool any = false;

            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                // 粒子渲染器的包围盒随特效飘动且不可控，会把边界拉歪，直接跳过。
                if (renderer is ParticleSystemRenderer) continue;
                var b = renderer.bounds;
                minX = Mathf.Min(minX, b.min.x); maxX = Mathf.Max(maxX, b.max.x);
                minY = Mathf.Min(minY, b.min.y); maxY = Mathf.Max(maxY, b.max.y);
                any = true;
            }
            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            {
                var b = collider.bounds;
                minX = Mathf.Min(minX, b.min.x); maxX = Mathf.Max(maxX, b.max.x);
                minY = Mathf.Min(minY, b.min.y); maxY = Mathf.Max(maxY, b.max.y);
                any = true;
            }

            // 空场景（既无可视物也无碰撞体）时给一个围绕出生点的兜底范围，防止 Rect 变成无穷大。
            if (!any)
            {
                minX = SpawnPoint.x - 10f; maxX = SpawnPoint.x + 10f;
                minY = SpawnPoint.y - 4f; maxY = SpawnPoint.y + 8f;
            }

            // 四周留余量：左右各外扩 3 个单位，下方 4、上方 6，镜头贴到场景边缘时仍有回旋余地。
            Bounds = new Rect(minX - 3f, minY - 4f, (maxX - minX) + 6f, (maxY - minY) + 10f);
        }

        /// <summary>切换到正交相机（可视半高 8 个世界单位），并把玩家交给 CameraFollow 跟随。</summary>
        void SetupCamera()
        {
            var camera = game.MainCamera;
            if (camera == null) return;
            cameraFollow = camera.GetComponent<CameraFollow>();
            // 主相机上不一定预挂跟随脚本，缺了就动态补一个。
            if (cameraFollow == null) cameraFollow = camera.gameObject.AddComponent<CameraFollow>();
            cameraFollow.Configure(8f);
            cameraFollow.SetTarget(Player.transform, Bounds, true);
        }

        void Update()
        {
            // 通关后不再判定坠落，避免结算期间重复触发死亡流程。
            if (Player == null || Completed) return;
            // 低于关卡死亡线 Data.killY 即视为掉坑；已在死亡或复活流程中则跳过。
            if (Player.transform.position.y < Data.killY && Health != null && !Health.Depleted && !Health.IsRespawning)
                Health.FallIntoPit();
        }

        /// <summary>踩到存档点时把复活点挪过去，并弹一条提示。</summary>
        public void ActivateCheckpoint(Checkpoint checkpoint)
        {
            SpawnPoint = checkpoint.SpawnPoint;
            game?.UI?.Toast("CHECKPOINT");
        }

        /// <summary>玩家拾取金币：计数、累计本局金币并播音效与火花。</summary>
        public void CollectCoin(Coin coin)
        {
            CollectedCoins++;
            game?.AddRunCoin();
            GameManager.Instance?.Audio?.Play(Sfx.Coin);
            Fx.Sparkle(coin.transform.position, new Color(1f, 0.86f, 0.35f), 6);
        }

        /// <summary>把玩家瞬移回复活点，并让相机立刻对齐，避免复活时镜头从远处追过来。</summary>
        public void RespawnPlayer()
        {
            if (Player == null) return;
            Player.Teleport(SpawnPoint);
            cameraFollow?.SnapToTarget();
        }

        /// <summary>通关只执行一次：锁定操作、停住角色、播胜利音效并通知 GameManager。</summary>
        public void CompleteLevel()
        {
            if (Completed) return;
            Completed = true;
            Player?.SetControl(false);
            Player?.Halt();
            GameManager.Instance?.Audio?.Play(Sfx.Victory);
            game?.OnLevelCompleted();
        }

        /// <summary>玩家被击败后转交 GameManager：记录死亡次数并进入结算（GameOver）流程。</summary>
        public void OnPlayerDefeated()
        {
            game?.OnPlayerDefeated();
        }

        /// <summary>暂停或恢复玩家操作，用于关卡过渡与结算界面；冻住时同时把速度清零。</summary>
        public void FreezePlayer(bool freeze)
        {
            if (Player == null) return;
            Player.SetControl(!freeze);
            if (freeze) Player.Halt();
        }

        /// <summary>只切换玩家是否可操作，不改动角色当前速度。</summary>
        public void SetPlayerControl(bool enabled)
        {
            Player?.SetControl(enabled);
        }
    }
}
