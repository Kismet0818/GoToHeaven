using System.Collections;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Marker for a static platform (also used by the one-way drop-through logic).</summary>
    public sealed class Platform : MonoBehaviour
    {
        public bool OneWay;
    }

    /// <summary>Kinematic sine-driven platform that carries the player standing on it.</summary>
    public sealed class MovingPlatform : MonoBehaviour
    {
        public Vector2 Velocity { get; private set; }

        Vector2 origin;
        Vector2 offset;
        float speed;
        float phase;
        float startTime;
        Rigidbody2D body;
        Collider2D surface;
        PlayerController player;

        public void Setup(Vector2 startPosition, Vector2 travelOffset, float travelSpeed, float startingPhase)
        {
            origin = startPosition;
            offset = travelOffset;
            speed = Mathf.Max(0.05f, travelSpeed);
            phase = startingPhase;
            startTime = Time.time;
            body = GetComponent<Rigidbody2D>();
            surface = GetComponent<Collider2D>();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        public void SetPlayer(PlayerController target) => player = target;

        void FixedUpdate()
        {
            if (body == null) return;

            float t = (Time.time - startTime) * speed + phase;
            Vector2 target = origin + offset * (0.5f - 0.5f * Mathf.Cos(t));
            Vector2 delta = target - body.position;
            body.MovePosition(target);
            Velocity = delta / Mathf.Max(0.0001f, Time.fixedDeltaTime);

            if (player == null || !player.Grounded || surface == null) return;

            var bounds = surface.bounds;
            var feet = player.transform.position;
            bool standing = feet.x >= bounds.min.x - 0.12f && feet.x <= bounds.max.x + 0.12f &&
                            feet.y >= bounds.max.y - 0.18f && feet.y <= bounds.max.y + 0.28f;
            if (standing) player.CarryBy(delta);
        }
    }

    /// <summary>Anything that hurts the player on touch. Attach next to a trigger collider.</summary>
    public sealed class Hazard : MonoBehaviour
    {
        public int damage = 1;

        void OnTriggerEnter2D(Collider2D other)
        {
            var health = other.GetComponent<PlayerHealth>();
            if (health != null) health.TakeDamage(damage);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var health = other.GetComponent<PlayerHealth>();
            if (health != null && !health.IsInvulnerable && !health.IsRespawning && !health.Depleted)
                health.TakeDamage(damage);
        }
    }

    /// <summary>Spinning saw blade that ping-pongs along an offset path.</summary>
    public sealed class SawTrap : MonoBehaviour
    {
        Vector2 origin;
        Vector2 offset;
        float speed;
        float phase;
        float startTime;

        public void Setup(Vector2 startPosition, Vector2 travelOffset, float travelSpeed, float startingPhase)
        {
            origin = startPosition;
            offset = travelOffset;
            speed = Mathf.Max(0.05f, travelSpeed);
            phase = startingPhase;
            startTime = Time.time;
        }

        void Update()
        {
            float t = (Time.time - startTime) * speed + phase;
            transform.position = origin + offset * (0.5f - 0.5f * Mathf.Cos(t));
            transform.Rotate(0f, 0f, 220f * Time.deltaTime);
        }
    }

    public sealed class Trampoline : MonoBehaviour
    {
        LevelController level;
        SpriteAnimator animator;
        Sprite[] jumpFrames;
        Sprite idleSprite;
        float power = 27f;
        float cooldown;

        public void Initialize(LevelController owner, Sprite idle, Sprite[] jump, float bouncePower)
        {
            level = owner;
            idleSprite = idle;
            jumpFrames = jump;
            power = bouncePower;
            animator = GetComponent<SpriteAnimator>();
            if (animator != null && idle != null) animator.Stop(idle);
        }

        void Update()
        {
            if (cooldown > 0f) cooldown = Mathf.Max(0f, cooldown - Time.deltaTime);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (cooldown > 0f) return;
            var player = other.GetComponent<PlayerController>();
            if (player == null) return;
            if (player.SpeedY > 1f) return;

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

    public sealed class Checkpoint : MonoBehaviour
    {
        public bool Active { get; private set; }
        public Vector2 SpawnPoint => (Vector2)transform.position + new Vector2(0f, 0.05f);

        LevelController level;
        SpriteAnimator animator;
        Sprite[] flagFrames;

        public void Initialize(LevelController owner, Sprite offSprite, Sprite[] flag)
        {
            level = owner;
            flagFrames = flag;
            animator = GetComponent<SpriteAnimator>();
            if (animator != null) animator.Stop(offSprite);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (Active) return;
            if (other.GetComponent<PlayerController>() == null) return;
            Activate();
        }

        public void Activate()
        {
            if (Active) return;
            Active = true;
            if (animator != null && flagFrames != null)
                animator.Play(flagFrames, 12f, true, true);
            level?.ActivateCheckpoint(this);
            Fx.Sparkle(transform.position + Vector3.up * 1.2f, new Color(1f, 0.86f, 0.4f), 10);
            GameManager.Instance?.Audio?.Play(Sfx.Checkpoint);
        }
    }

    public sealed class Goal : MonoBehaviour
    {
        LevelController level;
        SpriteAnimator animator;
        Sprite[] pressedFrames;
        bool reached;

        public void Initialize(LevelController owner, Sprite[] pressed)
        {
            level = owner;
            pressedFrames = pressed;
            animator = GetComponent<SpriteAnimator>();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (reached) return;
            if (other.GetComponent<PlayerController>() == null) return;
            reached = true;
            if (animator != null && pressedFrames != null)
                animator.PlayOnce(pressedFrames, 12f);
            Fx.Confetti(transform.position + Vector3.up * 1.5f, 32);
            level?.CompleteLevel();
        }
    }

    public sealed class Coin : MonoBehaviour
    {
        LevelController level;
        Vector3 startPosition;
        float phase;

        public void Initialize(LevelController owner, float animationPhase)
        {
            level = owner;
            startPosition = transform.position;
            phase = animationPhase;
        }

        void Update()
        {
            transform.position = startPosition + Vector3.up * (Mathf.Sin(Time.time * 3.2f + phase) * 0.12f);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (level == null) return;
            if (other.GetComponent<PlayerController>() == null) return;

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
