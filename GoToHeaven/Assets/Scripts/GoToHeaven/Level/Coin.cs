using UnityEngine;

namespace GoToHeaven
{
    public sealed class Coin : MonoBehaviour
    {
        LevelController level;
        SpriteAnimator animator;
        Vector3 startPosition;
        float phase;

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

        void Update()
        {
            transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * 3.2f + phase) * 0.12f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (level == null || other.GetComponent<PlayerController>() == null) return;
            level.CollectCoin(this);
            var art = GameManager.Instance?.Art;
            if (art != null)
            {
                var collected = art.Frames(ArtPaths.FruitCollected, 32, 32, 24f, SpriteLibrary.CenterPivot);
                Fx.OneShot(collected, transform.position, 18f, 1f, Color.white, 22);
            }
            Destroy(gameObject);
        }
    }
}
