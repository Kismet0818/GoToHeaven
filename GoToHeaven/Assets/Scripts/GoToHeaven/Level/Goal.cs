using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 关卡终点：玩家进入触发器即判定通关，播放按下动画与彩纸，然后交给 LevelController。
    /// reached 保证一局只结算一次，动画、音效与通关流程都不会重复。
    /// </summary>
    public sealed class Goal : MonoBehaviour
    {
        LevelController level;
        SpriteAnimator animator;
        Sprite[] pressedFrames;
        bool reached;

        /// <summary>由 LevelController 注入所属关卡，并预加载终点按下后的动画帧。</summary>
        public void Bind(LevelController owner)
        {
            level = owner;
            var art = GameManager.Instance != null ? GameManager.Instance.Art : null;
            if (art == null) return;
            pressedFrames = art.Frames(ArtPaths.GoalPressed, 64, 64, 28f, SpriteLibrary.PropPivot);
            animator = GetComponent<SpriteAnimator>();
        }

        // 只有玩家进入才通关；reached 短路确保撒花、音效与结算流程只发生一次。
        void OnTriggerEnter2D(Collider2D other)
        {
            if (reached || other.GetComponent<PlayerController>() == null) return;
            reached = true;
            // 按下动画 12 帧/秒只播一次，不循环，避免终点持续闪烁。
            if (animator != null && pressedFrames != null) animator.PlayOnce(pressedFrames, 12f);
            // 在门上方 1.5 处撒 32 片彩纸：够铺满屏幕，又不至于让低端机掉帧。
            Fx.Confetti(transform.position + Vector3.up * 1.5f, 32);
            level?.CompleteLevel();
        }
    }
}
