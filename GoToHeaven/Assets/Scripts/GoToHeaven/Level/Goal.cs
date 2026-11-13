using UnityEngine;

namespace GoToHeaven
{
    public sealed class Goal : MonoBehaviour
    {
        LevelController level;
        SpriteAnimator animator;
        Sprite[] pressedFrames;
        bool reached;

        public void Bind(LevelController owner)
        {
            level = owner;
            var art = GameManager.Instance != null ? GameManager.Instance.Art : null;
            if (art == null) return;
            pressedFrames = art.Frames(ArtPaths.GoalPressed, 64, 64, 28f, SpriteLibrary.PropPivot);
            animator = GetComponent<SpriteAnimator>();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (reached || other.GetComponent<PlayerController>() == null) return;
            reached = true;
            if (animator != null && pressedFrames != null) animator.PlayOnce(pressedFrames, 12f);
            Fx.Confetti(transform.position + Vector3.up * 1.5f, 32);
            level?.CompleteLevel();
        }
    }
}
