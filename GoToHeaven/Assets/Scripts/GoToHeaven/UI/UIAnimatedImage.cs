using UnityEngine;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>把一张 UI Image 按序列帧循环播放（用于主菜单的立绘动画）。</summary>
    public sealed class UIAnimatedImage : MonoBehaviour
    {
        public Image Target;
        // 序列帧：按顺序循环，最后一帧之后回到第 0 帧
        public Sprite[] Frames;
        // 帧率；实际取 Mathf.Max(1f, Fps) 兜底，避免填 0 时除零导致每帧狂刷
        public float Fps = 10f;
        int index;
        float timer;

        void Update()
        {
            // 未配置目标或帧序列时直接跳过，允许 prefab 上先挂脚本后填数据
            if (Target == null || Frames == null || Frames.Length == 0) return;
            // 用不受 timeScale 影响的时间：暂停（timeScale = 0）时菜单立绘仍要动
            timer += Time.unscaledDeltaTime;
            float frameTime = 1f / Mathf.Max(1f, Fps);
            // while 而非 if：掉帧时一次补齐多帧，保证播放速度与真实时间一致
            while (timer >= frameTime)
            {
                timer -= frameTime;
                index = (index + 1) % Frames.Length;
                Target.sprite = Frames[index];
            }
        }
    }
}
