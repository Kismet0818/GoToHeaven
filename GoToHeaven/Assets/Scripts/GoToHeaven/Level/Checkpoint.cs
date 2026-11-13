using UnityEngine;

namespace GoToHeaven
{
    public sealed class Checkpoint : MonoBehaviour
    {
        public bool Active { get; private set; }
        public Vector2 SpawnPoint => (Vector2)transform.position + new Vector2(0f, 0.05f);

        LevelController level;
        SpriteAnimator animator;
        Sprite[] flagFrames;

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

        void OnTriggerEnter2D(Collider2D other)
        {
            if (Active || other.GetComponent<PlayerController>() == null) return;
            Activate();
        }

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
