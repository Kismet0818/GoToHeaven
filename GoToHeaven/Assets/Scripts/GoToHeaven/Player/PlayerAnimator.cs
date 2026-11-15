using UnityEngine;

namespace GoToHeaven
{
    /// <summary>根据控制器的状态挑选对应的 Mask Dude 动画。</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(SpriteAnimator))]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        PlayerController controller;
        SpriteAnimator animator;
        SpriteRenderer spriteRenderer;

        Sprite[] idle, run, jump, fall, doubleJump, wallJump, hit;

        float hitTimer;       // 受击动画期间压制其它状态切换，避免被立刻覆盖
        float doubleJumpTimer;
        float wallJumpTimer;

        void Start()
        {
            // GameManager 可能尚未初始化（例如单独打开场景调动作），退回临时实例以免空引用崩溃。
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
            // 必须成对退订：控制器可能比本组件活得久，不退订会在切换场景后留下悬空委托。
            if (controller != null)
            {
                controller.DoubleJumped -= OnDoubleJump;
                controller.WallJumped -= OnWallJump;
            }
        }

        void OnDoubleJump()
        {
            doubleJumpTimer = 0.55f; // 与一次性动画时长对齐，播完之前不许状态机抢回控制权
            if (doubleJump.Length > 0) animator.PlayOnce(doubleJump, 14f);
        }

        void OnWallJump()
        {
            wallJumpTimer = 0.4f;
            if (wallJump.Length > 0) animator.PlayOnce(wallJump, 14f);
        }

        /// <summary>播放受击动作；由 PlayerHealth 在扣血时调用。</summary>
        public void PlayHit()
        {
            hitTimer = 0.5f;
            if (hit != null && hit.Length > 0) animator.PlayOnce(hit, 14f);
        }

        void Update()
        {
            if (controller == null) return;

            hitTimer = Mathf.Max(0f, hitTimer - Time.deltaTime); // 三个一次性动作计时器在这里统一递减
            doubleJumpTimer = Mathf.Max(0f, doubleJumpTimer - Time.deltaTime);
            wallJumpTimer = Mathf.Max(0f, wallJumpTimer - Time.deltaTime);

            spriteRenderer.flipX = controller.Facing < 0;

            // 一次性动作优先：受击 > 二段跳 > 墙跳，计时未结束就不让常驻状态机插队。
            if (hitTimer > 0f) return;
            if (doubleJumpTimer > 0f) return;
            if (wallJumpTimer > 0f) return;

            if (!controller.Grounded)
            {
                if (controller.WallSliding)
                {
                    // 项目没有单独的滑墙美术，借用墙跳/下落帧并以 9fps 慢放，做出“蹭墙”的感觉。
                    animator.Play(wallJump.Length > 0 ? wallJump : fall, 9f, true);
                }
                else if (controller.SpeedY > 0.6f)
                {
                    animator.Play(jump, 1f, true); // 1fps 近乎定格首帧：上升途中不播帧动画，避免画面闪动
                }
                else
                {
                    animator.Play(fall, 1f, true);
                }
                return;
            }

            // 0.35 的阈值高于摩擦收速后的残值，停下时能干净地切回待机，不会在跑/站之间闪烁。
            if (Mathf.Abs(controller.SpeedX) > 0.35f)
                animator.Play(run, 15f, true);
            else
                animator.Play(idle, 10f, true);
        }
    }
}
