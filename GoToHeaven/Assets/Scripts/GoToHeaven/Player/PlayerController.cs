using System;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Hand tuned 2D platformer controller: acceleration curves, coyote time,
    /// jump buffering, variable jump height, double jump, wall slide/jump and dash.
    /// Uses the legacy Input Manager, which is what the project is configured for.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Run")]
        public float moveSpeed = 7.2f;
        public float acceleration = 78f;
        public float airAcceleration = 52f;
        public float friction = 82f;

        [Header("Jump")]
        public float gravity = -48f;
        public float fallMultiplier = 1.35f;
        public float maxFallSpeed = 27f;
        public float jumpVelocity = 18.4f;
        public float doubleJumpVelocity = 16.2f;
        public float coyoteTime = 0.11f;
        public float jumpBufferTime = 0.12f;

        [Header("Wall")]
        public float wallSlideSpeed = -4.2f;
        public float wallJumpPush = 11.5f;
        public float wallJumpUp = 17f;
        public float wallJumpLock = 0.16f;

        [Header("Dash")]
        public float dashSpeed = 21f;
        public float dashTime = 0.14f;
        public float dashCooldownTime = 0.55f;

        public bool ControlEnabled { get; set; } = true;
        public int Facing { get; private set; } = 1;
        public bool Grounded { get; private set; }
        public bool WallSliding { get; private set; }
        public bool Dashing => dashTimer > 0f;
        public float SpeedX => rb != null ? rb.velocity.x : 0f;
        public float SpeedY => rb != null ? rb.velocity.y : 0f;
        public Collider2D GroundCollider { get; private set; }

        public event Action Jumped;
        public event Action DoubleJumped;
        public event Action WallJumped;
        public event Action Landed;
        public event Action Dashed;

        Rigidbody2D rb;
        BoxCollider2D box;
        Vector2 velocity;
        int wallDirection;
        float coyoteTimer;
        float jumpBufferTimer;
        float wallJumpLockTimer;
        float dashTimer;
        float dashCooldown;
        int dashDirection = 1;
        bool doubleJumpAvailable = true;
        bool airDashAvailable = true;
        bool jumpHeldLastFrame;
        bool landingWasAirborne;
        float ghostTimer;
        Coroutine dropRoutine;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            box = GetComponent<BoxCollider2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (box.sharedMaterial == null)
            {
                box.sharedMaterial = new PhysicsMaterial2D("PlayerFrictionless")
                {
                    friction = 0f,
                    bounciness = 0f
                };
            }
        }

        void Update()
        {
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
                horizontal = 0f;
                jumpPressed = false;
                jumpHeld = false;
                dashPressed = false;
            }

            if (Mathf.Abs(horizontal) > 0.1f && wallJumpLockTimer <= 0f)
                Facing = horizontal > 0f ? 1 : -1;

            if (jumpPressed)
            {
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
                jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
                coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);
                wallJumpLockTimer = Mathf.Max(0f, wallJumpLockTimer - dt);
                dashCooldown = Mathf.Max(0f, dashCooldown - dt);
            }

            CheckGround();
            CheckWall();

            if (dashTimer > 0f)
            {
                dashTimer -= dt;
                velocity = new Vector2(dashDirection * dashSpeed, 0f);
                ghostTimer -= dt;
                if (ghostTimer <= 0f)
                {
                    ghostTimer = 0.035f;
                    PlayerGhost.Spawn(GetComponent<SpriteRenderer>());
                }
                if (dashTimer <= 0f)
                    velocity.x = dashDirection * moveSpeed * 0.7f;
            }
            else
            {
                if (jumpBufferTimer > 0f && ControlEnabled)
                {
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
                if (wallJumpLockTimer > 0f) horizontal = 0f;

                float target = horizontal * moveSpeed;
                float accel = Grounded ? acceleration : airAcceleration;
                velocity.x = Mathf.MoveTowards(velocity.x, target, accel * dt);
                if (Mathf.Abs(horizontal) < 0.05f && Grounded)
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, friction * dt);

                bool pushingIntoWall = wallDirection != 0 && horizontal * wallDirection > 0.1f;
                WallSliding = !Grounded && pushingIntoWall && velocity.y < 0f && !Dashing;

                float g = gravity * ((!Grounded && velocity.y < 0f) ? fallMultiplier : 1f);
                velocity.y += g * dt;
                if (Grounded && velocity.y < -2f) velocity.y = -2f;
                if (WallSliding) velocity.y = Mathf.Max(velocity.y, wallSlideSpeed);
                if (velocity.y > 0f && CeilingBlocked()) velocity.y = 0f;
                velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
            }

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
            var origin = (Vector2)transform.position + new Vector2(0f, 0.09f);
            var hit = Physics2D.BoxCast(origin, new Vector2(0.66f, 0.12f), 0f, Vector2.down, 0.16f, GameLayers.GroundMask);
            bool wasGrounded = Grounded;
            Grounded = hit.collider != null && velocity.y <= 0.25f;
            GroundCollider = hit.collider;

            if (Grounded)
            {
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
            if (Grounded) return;

            var origin = (Vector2)transform.position + new Vector2(0f, 0.95f);
            var size = new Vector2(0.12f, 1.25f);
            var right = Physics2D.BoxCast(origin, size, 0f, Vector2.right, 0.12f, GameLayers.WallMask);
            var left = Physics2D.BoxCast(origin, size, 0f, Vector2.left, 0.12f, GameLayers.WallMask);

            if (right.collider != null && left.collider == null) wallDirection = 1;
            else if (left.collider != null && right.collider == null) wallDirection = -1;
            else if (right.collider != null && left.collider != null)
                wallDirection = Input.GetAxisRaw("Horizontal") >= 0f ? 1 : -1;
        }

        bool CeilingBlocked()
        {
            var origin = (Vector2)transform.position + new Vector2(0f, 1.78f);
            return Physics2D.BoxCast(origin, new Vector2(0.62f, 0.1f), 0f, Vector2.up, 0.12f, GameLayers.GroundMask).collider != null;
        }

        bool CanDash()
        {
            if (dashCooldown > 0f || Dashing || !ControlEnabled) return false;
            return Grounded || airDashAvailable;
        }

        void StartDash(float horizontal)
        {
            dashDirection = Mathf.Abs(horizontal) > 0.1f ? (horizontal > 0f ? 1 : -1) : Facing;
            Facing = dashDirection;
            dashTimer = dashTime;
            ghostTimer = 0f;
            dashCooldown = dashCooldownTime;
            if (!Grounded) airDashAvailable = false;
            GameManager.Instance?.Audio?.Play(Sfx.Dash);
            Fx.Dust(transform.position, 4, 1.2f);
            Dashed?.Invoke();
        }

        void DoJump()
        {
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
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

        /// <summary>Used by trampolines and bounce pads.</summary>
        public void Bounce(float power)
        {
            velocity.y = power;
            Grounded = false;
            doubleJumpAvailable = true;
            airDashAvailable = true;
            jumpBufferTimer = 0f;
            GameManager.Instance?.Audio?.Play(Sfx.Bounce);
            Fx.Dust(transform.position, 7, 2.2f);
        }

        public void Teleport(Vector2 position)
        {
            transform.position = position;
            if (rb != null) rb.position = position;
            velocity = Vector2.zero;
            Grounded = false;
            WallSliding = false;
            dashTimer = 0f;
            jumpBufferTimer = 0f;
            wallJumpLockTimer = 0f;
            if (rb != null) rb.velocity = Vector2.zero;
        }

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

        public void AddVelocity(Vector2 impulse)
        {
            velocity += impulse;
            if (rb != null) rb.velocity = velocity;
        }

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
                landingWasAirborne = false;
            }
        }

        /// <summary>Immediately stops all momentum (used on victory / screen transitions).</summary>
        public void Halt()
        {
            velocity = Vector2.zero;
            dashTimer = 0f;
            jumpBufferTimer = 0f;
            if (rb != null) rb.velocity = Vector2.zero;
        }

        public void CarryBy(Vector2 delta)
        {
            if (rb != null) rb.position += delta;
            transform.position += (Vector3)delta;
        }

        void DropThrough()
        {
            if (dropRoutine != null) StopCoroutine(dropRoutine);
            dropRoutine = StartCoroutine(DropThroughRoutine());
        }

        System.Collections.IEnumerator DropThroughRoutine()
        {
            if (GroundCollider == null) yield break;
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

            yield return new WaitForSeconds(0.32f);

            foreach (var platformCollider in platformColliders)
            {
                if (platformCollider == null) continue;
                foreach (var ownCollider in own)
                {
                    if (ownCollider != null) Physics2D.IgnoreCollision(platformCollider, ownCollider, false);
                }
            }
            dropRoutine = null;
        }
    }

    /// <summary>Fading afterimage used by the dash.</summary>
    public sealed class PlayerGhost : MonoBehaviour
    {
        SpriteRenderer ghost;
        float life = 0.22f;
        float age;

        public static void Spawn(SpriteRenderer source)
        {
            if (source == null || source.sprite == null) return;
            var go = new GameObject("DashGhost");
            go.transform.position = source.transform.position;
            go.transform.localScale = source.transform.localScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = source.sprite;
            renderer.flipX = source.flipX;
            renderer.sortingOrder = source.sortingOrder - 1;
            renderer.color = new Color(0.55f, 0.85f, 1f, 0.38f);
            go.AddComponent<PlayerGhost>();
        }

        void Awake() => ghost = GetComponent<SpriteRenderer>();

        void Update()
        {
            age += Time.deltaTime;
            if (ghost != null)
            {
                var c = ghost.color;
                c.a = Mathf.Lerp(0.38f, 0f, age / life);
                ghost.color = c;
            }
            if (age >= life) Destroy(gameObject);
        }
    }
}
