using System.Collections;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 弹床：玩家落到触发器上时给一个向上的初速度（power），并播放弹跳动画。
    /// 只对下落或接近静止的玩家生效，并带 0.15 秒冷却，防止一次接触被多次触发。
    /// </summary>
    public sealed class Trampoline : MonoBehaviour
    {
        /// <summary>弹起初速度（单位/秒）：27 约把玩家送到普通跳跃（18.4）2.1 倍的高度。</summary>
        public float power = 27f;

        LevelController level;
        SpriteAnimator animator;
        Sprite[] jumpFrames;
        Sprite idleSprite;
        float cooldown;

        /// <summary>由 LevelController 注入所属关卡，并加载待机帧与弹跳帧；缺美术时静默跳过。</summary>
        public void Bind(LevelController owner)
        {
            level = owner;
            var art = GameManager.Instance != null ? GameManager.Instance.Art : null;
            if (art == null) return;
            var idle = art.Frames(ArtPaths.TrampolineIdle, 28, 28, 16f, SpriteLibrary.PropPivot);
            jumpFrames = art.Frames(ArtPaths.TrampolineJump, 28, 28, 16f, SpriteLibrary.PropPivot);
            idleSprite = idle.Length > 0 ? idle[0] : null;
            animator = GetComponent<SpriteAnimator>();
            if (animator != null && idleSprite != null) animator.Stop(idleSprite);
        }

        void Update()
        {
            // 冷却按真实时间递减，并用 Max 夹到 0，防止 deltaTime 抖动后出现负值。
            if (cooldown > 0f) cooldown = Mathf.Max(0f, cooldown - Time.deltaTime);
        }

        // 只弹玩家，且要求其正在下落或接近静止：上升途中（SpeedY > 1）不再弹，避免速度叠加。
        void OnTriggerEnter2D(Collider2D other)
        {
            if (cooldown > 0f) return;
            var player = other.GetComponent<PlayerController>();
            if (player == null || player.SpeedY > 1f) return;

            // 0.15 秒冷却防止短时间内重复触发导致连弹、叠速。
            cooldown = 0.15f;
            player.Bounce(power);
            if (animator != null && jumpFrames != null && jumpFrames.Length > 0)
            {
                // 弹跳动画 18 帧/秒播一次，0.4 秒后切回待机帧。
                animator.PlayOnce(jumpFrames, 18f);
                StartCoroutine(ReturnToIdle(0.4f));
            }
        }

        /// <summary>延迟后切回待机帧，避免弹跳动画停在最后一帧。</summary>
        IEnumerator ReturnToIdle(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (animator != null && idleSprite != null) animator.Stop(idleSprite);
        }
    }
}
