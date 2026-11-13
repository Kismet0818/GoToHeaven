using UnityEngine;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>Cycles a UI Image through a sprite strip (menu hero).</summary>
    public sealed class UIAnimatedImage : MonoBehaviour
    {
        public Image Target;
        public Sprite[] Frames;
        public float Fps = 10f;
        int index;
        float timer;

        void Update()
        {
            if (Target == null || Frames == null || Frames.Length == 0) return;
            timer += Time.unscaledDeltaTime;
            float frameTime = 1f / Mathf.Max(1f, Fps);
            while (timer >= frameTime)
            {
                timer -= frameTime;
                index = (index + 1) % Frames.Length;
                Target.sprite = Frames[index];
            }
        }
    }
}
