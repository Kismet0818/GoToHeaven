using UnityEngine;

namespace GoToHeaven
{
    /// <summary>基于帧数组的精灵动画（不需要 Animator 资源，避免为几个动作额外建状态机）。</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        /// <summary>目标渲染器；未显式赋值时取自同一物体上的 SpriteRenderer。</summary>
        public SpriteRenderer Renderer { get; set; }

        // 当前帧序列、帧率、累计计时、当前帧索引、是否循环、是否正在播放。
        Sprite[] frames;
        float fps = 12f;
        float timer;
        int index;
        bool loop = true;
        bool playing;

        /// <summary>是否正在播放；非循环动画播完后会自动变回 false。</summary>
        public bool IsPlaying => playing;
        /// <summary>当前帧在序列中的索引，可用于判断某个动作是否播到关键帧。</summary>
        public int FrameIndex => index;

        void Awake()
        {
            // 脚本由 AddComponent 动态挂上时 Renderer 尚未赋值，这里兜底取同物体组件。
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>播放一段序列帧；序列与循环设置都没变时直接返回，避免每帧重复触发。</summary>
        public void Play(Sprite[] newFrames, float framesPerSecond, bool shouldLoop = true, bool restart = false)
        {
            if (newFrames == null || newFrames.Length == 0) return;
            // 非 restart 请求下，同一序列已在播放就复用，防止切换状态时动画被打回第一帧。
            if (!restart && playing && frames == newFrames && loop == shouldLoop) return;

            frames = newFrames;
            // 帧率夹到正区间，避免下面计算 frameTime 时出现除零。
            fps = Mathf.Max(0.01f, framesPerSecond);
            loop = shouldLoop;
            timer = 0f;
            index = 0;
            playing = true;
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
            if (Renderer != null) Renderer.sprite = frames[0];
        }

        /// <summary>只播放一次，播完停在最后一帧（用于受击、拾取等一次性动作）。</summary>
        public void PlayOnce(Sprite[] newFrames, float framesPerSecond)
        {
            Play(newFrames, framesPerSecond, false, true);
        }

        /// <summary>停止播放并停在指定帧；传 null 则保持当前画面不动。</summary>
        public void Stop(Sprite hold)
        {
            playing = false;
            if (Renderer != null && hold != null) Renderer.sprite = hold;
        }

        void Update()
        {
            if (!playing || frames == null || frames.Length == 0) return;

            timer += Time.deltaTime;
            float frameTime = 1f / fps;
            // 用 while 消费累计时间：低帧率掉帧时可一次补多帧，动画不会整体变慢。
            while (timer >= frameTime)
            {
                timer -= frameTime;
                index++;
                if (index >= frames.Length)
                {
                    if (loop)
                    {
                        index = 0;
                    }
                    else
                    {
                        // 非循环：停在末帧并结束播放，break 防止剩余时间继续推进索引。
                        index = frames.Length - 1;
                        playing = false;
                        break;
                    }
                }
                if (Renderer != null) Renderer.sprite = frames[index];
            }
        }
    }
}
