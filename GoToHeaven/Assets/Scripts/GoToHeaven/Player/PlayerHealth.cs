using System.Collections;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Hearts, invulnerability frames, hit feedback and respawn hand-off.</summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        public const int MaxHearts = 3;

        public int Current { get; private set; } = MaxHearts;
        public bool Depleted { get; private set; }
        public bool IsRespawning { get; private set; }

        LevelController level;
        PlayerController controller;
        PlayerAnimator animator;
        SpriteRenderer[] renderers;
        float invulnerableTimer;
        Coroutine blinkRoutine;

        public bool IsInvulnerable => invulnerableTimer > 0f;

        public void Initialize(LevelController owner)
        {
            level = owner;
            controller = GetComponent<PlayerController>();
            animator = GetComponent<PlayerAnimator>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            Current = MaxHearts;
            Depleted = false;
            IsRespawning = false;
            invulnerableTimer = 0f;
        }

        public void TakeDamage(int amount = 1, bool knockedBack = true, float respawnDelay = 0.38f)
        {
            if (Depleted || IsRespawning || invulnerableTimer > 0f) return;

            IsRespawning = true;
            Current = Mathf.Max(0, Current - amount);
            GameManager.Instance?.Audio?.Play(Sfx.Hurt);
            CameraFollow.Shake(0.3f, 0.25f);

            if (controller != null)
            {
                controller.SetControl(false);
                if (knockedBack)
                {
                    controller.Teleport(transform.position);
                    controller.AddVelocity(new Vector2(-controller.Facing * 5f, 9f));
                }
            }

            if (Current <= 0)
            {
                Depleted = true;
                StartCoroutine(DeathRoutine());
            }
            else
            {
                StartCoroutine(HitRoutine(respawnDelay));
            }
        }

        public void FallIntoPit()
        {
            if (Depleted || IsRespawning) return;
            TakeDamage(1, knockedBack: false, respawnDelay: 0.12f);
        }

        IEnumerator HitRoutine(float delay)
        {
            animator?.PlayHit();
            if (delay > 0f) yield return new WaitForSeconds(delay);

            level?.RespawnPlayer();
            if (controller != null)
            {
                controller.ResetForRespawn();
                controller.SetControl(true);
            }

            SetInvulnerable(1.2f);
            IsRespawning = false;
        }

        IEnumerator DeathRoutine()
        {
            animator?.PlayHit();
            yield return new WaitForSeconds(0.85f);
            level?.OnPlayerDefeated();
        }

        public void SetInvulnerable(float duration)
        {
            invulnerableTimer = Mathf.Max(invulnerableTimer, duration);
            if (blinkRoutine != null) StopCoroutine(blinkRoutine);
            blinkRoutine = StartCoroutine(BlinkRoutine());
        }

        IEnumerator BlinkRoutine()
        {
            float elapsed = 0f;
            while (elapsed < invulnerableTimer + 0.01f)
            {
                bool visible = Mathf.Repeat(elapsed, 0.16f) < 0.09f;
                SetRenderersVisible(visible);
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetRenderersVisible(true);
        }

        void SetRenderersVisible(bool visible)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
                if (renderer != null) renderer.enabled = visible;
        }

        void Update()
        {
            if (invulnerableTimer > 0f) invulnerableTimer = Mathf.Max(0f, invulnerableTimer - Time.deltaTime);
        }

        public void RestoreAll()
        {
            Current = MaxHearts;
            Depleted = false;
            IsRespawning = false;
            invulnerableTimer = 0f;
            SetRenderersVisible(true);
        }
    }
}
