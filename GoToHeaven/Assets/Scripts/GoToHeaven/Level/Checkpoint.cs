using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 存档点旗帜：玩家首次碰到后点亮，并把关卡重生点改到旗子处。
    /// 触发器只认玩家，靠 Active 短路重复触发，避免重复播放特效和音效。
    /// </summary>
    public sealed class Checkpoint : MonoBehaviour
    {
        /// <summary>是否已激活；点亮后保持状态，之后经过的玩家不会再次改写重生点。</summary>
        public bool Active { get; private set; }
        /// <summary>重生坐标：旗子上方 0.05，避免玩家脚底卡进旗座或地面。</summary>
        public Vector2 SpawnPoint => (Vector2)transform.position + new Vector2(0f, 0.05f);

        LevelController level;
        SpriteAnimator animator;
        Sprite[] flagFrames;

        /// <summary>由 LevelController 在关卡初始化时注入所属关卡，并加载熄灭/点亮两套旗帜帧。</summary>
        public void Bind(LevelController owner)
        {
            level = owner;
            var art = GameManager.Instance != null ? GameManager.Instance.Art : null;
            if (art == null) return;
            var off = art.Frames(ArtPaths.CheckpointOff, 64, 64, 32f, SpriteLibrary.PropPivot);
            flagFrames = art.Frames(ArtPaths.CheckpointOn, 64, 64, 32f, SpriteLibrary.PropPivot);
            animator = GetComponent<SpriteAnimator>();
            if (animator != null) animator.Stop(off.Length > 0 ? off[0] : null);
        }

        // 只有玩家进入才激活：金币、道具等其它触发体一律忽略，避免提前点亮。
        void OnTriggerEnter2D(Collider2D other)
        {
            if (Active || other.GetComponent<PlayerController>() == null) return;
            Activate();
        }

        /// <summary>
        /// 点亮旗帜、上报关卡更新重生点；12 帧/秒循环播放点亮动画。
        /// 同时在上方 1.2 处撒 10 颗金色粒子并播放存档音效；重复调用直接返回。
        /// </summary>
        public void Activate()
        {
            if (Active) return;
            Active = true;
            if (animator != null && flagFrames != null) animator.Play(flagFrames, 12f, true, true);
            level?.ActivateCheckpoint(this);
            Fx.Sparkle(transform.position + Vector3.up * 1.2f, new Color(1f, 0.86f, 0.4f), 10);
            GameManager.Instance?.Audio?.Play(Sfx.Checkpoint);
        }
    }
}
