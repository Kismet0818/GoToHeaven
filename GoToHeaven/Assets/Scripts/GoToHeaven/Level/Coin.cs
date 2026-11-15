using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 可收集金币（水果图集）：玩家碰到即计数并销毁；平时上下浮动并循环播放动画。
    /// 浮动相位由关卡按序号错开，避免整排金币同步起伏，视觉上更像波浪。
    /// </summary>
    public sealed class Coin : MonoBehaviour
    {
        LevelController level;
        SpriteAnimator animator;
        Vector3 startPosition;
        float phase;

        /// <summary>由 LevelController 注入所属关卡；animationPhase 用于错开各金币的浮动节奏。</summary>
        public void Bind(LevelController owner, float animationPhase)
        {
            level = owner;
            startPosition = transform.position;
            phase = animationPhase;
            var art = GameManager.Instance != null ? GameManager.Instance.Art : null;
            if (art == null) return;
            var frames = art.Frames(ArtPaths.Fruit, 32, 32, 24f, SpriteLibrary.CenterPivot);
            animator = GetComponent<SpriteAnimator>();
            if (animator != null && frames.Length > 1) animator.Play(frames, 18f, true);
        }

        // 用正弦做轻微上下浮动：±0.12 单位、约 0.5 Hz；纯表现用，触发器会随之轻微移动。
        void Update()
        {
            transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * 3.2f + phase) * 0.12f);
        }

        // 只有玩家会收集：先上报关卡再销毁自身，确保计数、音效与粒子只结算一次。
        void OnTriggerEnter2D(Collider2D other)
        {
            if (level == null || other.GetComponent<PlayerController>() == null) return;
            level.CollectCoin(this);
            var art = GameManager.Instance?.Art;
            if (art != null)
            {
                var collected = art.Frames(ArtPaths.FruitCollected, 32, 32, 24f, SpriteLibrary.CenterPivot);
                // 收集特效 18 帧/秒、原尺寸、排序 22，确保盖在金币与地形之上。
                Fx.OneShot(collected, transform.position, 18f, 1f, Color.white, 22);
            }
            Destroy(gameObject);
        }
    }
}
