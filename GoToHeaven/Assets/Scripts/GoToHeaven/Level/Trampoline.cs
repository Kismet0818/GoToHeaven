using System.Collections;
using UnityEngine;

namespace GoToHeaven
{
    public sealed class Trampoline : MonoBehaviour
    {
        public float power = 27f;

        LevelController level;
        SpriteAnimator animator;
        Sprite[] jumpFrames;
        Sprite idleSprite;
        float cooldown;

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
            if (cooldown > 0f) cooldown = Mathf.Max(0f, cooldown - Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (cooldown > 0f) return;
            var player = other.GetComponent<PlayerController>();
            if (player == null || player.SpeedY > 1f) return;

            cooldown = 0.15f;
            player.Bounce(power);
            if (animator != null && jumpFrames != null && jumpFrames.Length > 0)
            {
                animator.PlayOnce(jumpFrames, 18f);
                StartCoroutine(ReturnToIdle(0.4f));
            }
        }

        IEnumerator ReturnToIdle(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (animator != null && idleSprite != null) animator.Stop(idleSprite);
        }
    }
}
