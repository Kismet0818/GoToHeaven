using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Picks the right Mask Dude animation from the controller state.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(SpriteAnimator))]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        PlayerController controller;
        SpriteAnimator animator;
        SpriteRenderer spriteRenderer;

        Sprite[] idle, run, jump, fall, doubleJump, wallJump, hit;

        float hitTimer;
        float doubleJumpTimer;
        float wallJumpTimer;

        void Start()
        {
            var art = GameManager.Instance != null ? GameManager.Instance.Art : new SpriteLibrary();
            idle = art.Frames(ArtPaths.PlayerIdle, 32, 32, 16f, SpriteLibrary.PlayerPivot);
            run = art.Frames(ArtPaths.PlayerRun, 32, 32, 16f, SpriteLibrary.PlayerPivot);
            jump = art.Frames(ArtPaths.PlayerJump, 32, 32, 16f, SpriteLibrary.PlayerPivot);
            fall = art.Frames(ArtPaths.PlayerFall, 32, 32, 16f, SpriteLibrary.PlayerPivot);
            doubleJump = art.Frames(ArtPaths.PlayerDoubleJump, 32, 32, 16f, SpriteLibrary.PlayerPivot);
            wallJump = art.Frames(ArtPaths.PlayerWallJump, 32, 32, 16f, SpriteLibrary.PlayerPivot);
            hit = art.Frames(ArtPaths.PlayerHit, 32, 32, 16f, SpriteLibrary.PlayerPivot);

            controller = GetComponent<PlayerController>();
            animator = GetComponent<SpriteAnimator>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (controller != null)
            {
                controller.DoubleJumped += OnDoubleJump;
                controller.WallJumped += OnWallJump;
            }

            if (idle.Length > 0) spriteRenderer.sprite = idle[0];
        }

        void OnDestroy()
        {
            if (controller != null)
            {
                controller.DoubleJumped -= OnDoubleJump;
                controller.WallJumped -= OnWallJump;
            }
        }

        void OnDoubleJump()
        {
            doubleJumpTimer = 0.55f;
            if (doubleJump.Length > 0) animator.PlayOnce(doubleJump, 14f);
        }

        void OnWallJump()
        {
            wallJumpTimer = 0.4f;
            if (wallJump.Length > 0) animator.PlayOnce(wallJump, 14f);
        }

        public void PlayHit()
        {
            hitTimer = 0.5f;
            if (hit != null && hit.Length > 0) animator.PlayOnce(hit, 14f);
        }

        void Update()
        {
            if (controller == null) return;

            hitTimer = Mathf.Max(0f, hitTimer - Time.deltaTime);
            doubleJumpTimer = Mathf.Max(0f, doubleJumpTimer - Time.deltaTime);
            wallJumpTimer = Mathf.Max(0f, wallJumpTimer - Time.deltaTime);

            spriteRenderer.flipX = controller.Facing < 0;

            if (hitTimer > 0f) return;
            if (doubleJumpTimer > 0f) return;
            if (wallJumpTimer > 0f) return;

            if (!controller.Grounded)
            {
                if (controller.WallSliding)
                {
                    animator.Play(wallJump.Length > 0 ? wallJump : fall, 9f, true);
                }
                else if (controller.SpeedY > 0.6f)
                {
                    animator.Play(jump, 1f, true);
                }
                else
                {
                    animator.Play(fall, 1f, true);
                }
                return;
            }

            if (Mathf.Abs(controller.SpeedX) > 0.35f)
                animator.Play(run, 15f, true);
            else
                animator.Play(idle, 10f, true);
        }
    }
}
