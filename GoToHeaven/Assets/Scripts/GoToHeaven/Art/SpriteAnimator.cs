using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Frame based sprite animation (no Animator assets required).</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        public SpriteRenderer Renderer { get; set; }

        Sprite[] frames;
        float fps = 12f;
        float timer;
        int index;
        bool loop = true;
        bool playing;

        public bool IsPlaying => playing;
        public int FrameIndex => index;

        void Awake()
        {
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
        }

        public void Play(Sprite[] newFrames, float framesPerSecond, bool shouldLoop = true, bool restart = false)
        {
            if (newFrames == null || newFrames.Length == 0) return;
            if (!restart && playing && frames == newFrames && loop == shouldLoop) return;

            frames = newFrames;
            fps = Mathf.Max(0.01f, framesPerSecond);
            loop = shouldLoop;
            timer = 0f;
            index = 0;
            playing = true;
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
            if (Renderer != null) Renderer.sprite = frames[0];
        }

        public void PlayOnce(Sprite[] newFrames, float framesPerSecond)
        {
            Play(newFrames, framesPerSecond, false, true);
        }

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
