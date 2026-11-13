using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Short lived, allocation-light sprite effects (dust, confetti, sparkles).</summary>
    public static class Fx
    {
        public static void Dust(Vector3 position, int count = 6, float strength = 2.5f)
        {
            var art = GameManager.Instance != null ? GameManager.Instance.Art : null;
            var sprite = art != null ? art.Get(ArtPaths.Dust) : null;
            Burst(sprite, position, count, new Color(1f, 0.95f, 0.82f, 0.9f), 0.45f, strength, 4f, 1.1f);
        }

        public static void Confetti(Vector3 position, int count = 26)
        {
            var art = GameManager.Instance != null ? GameManager.Instance.Art : null;
            var frames = art != null ? art.Frames(ArtPaths.Confetti, 16, 16, 16f, SpriteLibrary.CenterPivot) : null;
            var colors = new[]
            {
                new Color(1f, 0.83f, 0.4f), new Color(0.95f, 0.4f, 0.45f),
                new Color(0.45f, 0.82f, 0.72f), new Color(0.62f, 0.65f, 1f), new Color(1f, 0.95f, 0.85f)
            };
            for (int i = 0; i < count; i++)
            {
                var sprite = frames != null && frames.Length > 0 ? frames[Random.Range(0, frames.Length)] : null;
                var color = colors[Random.Range(0, colors.Length)];
                var go = Spawn(sprite, position + (Vector3)(Random.insideUnitCircle * 0.7f), color, Random.Range(0.8f, 1.6f));
                var particle = go.AddComponent<FxParticle>();
                particle.Init(new Vector2(Random.Range(-4f, 4f), Random.Range(5f, 11f)), 0.9f, 12f, 0.5f);
            }
        }

        public static void Sparkle(Vector3 position, Color color, int count = 8)
        {
            var sprite = ProceduralArt.Circle(6, color);
            Burst(sprite, position, count, color, 0.5f, 4f, 0f, 1.4f);
        }

        public static void Burst(Sprite sprite, Vector3 position, int count, Color color, float life, float speed, float gravity, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                var go = Spawn(sprite, position, color, scale * Random.Range(0.7f, 1.3f));
                var particle = go.AddComponent<FxParticle>();
                var direction = Random.insideUnitCircle.normalized * speed * Random.Range(0.4f, 1f);
                particle.Init(new Vector2(direction.x, Mathf.Abs(direction.y) * 0.8f + speed * 0.25f), life * Random.Range(0.75f, 1.2f), gravity, 0.25f);
            }
        }

        public static void OneShot(Sprite[] frames, Vector3 position, float fps, float scale, Color tint, int sortingOrder = 25)
        {
            if (frames == null || frames.Length == 0) return;
            var go = new GameObject("FxOneShot");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
            var animator = go.AddComponent<SpriteAnimator>();
            animator.PlayOnce(frames, fps);
            Object.Destroy(go, frames.Length / Mathf.Max(1f, fps) + 0.1f);
        }

        static GameObject Spawn(Sprite sprite, Vector3 position, Color color, float scale)
        {
            var go = new GameObject("Fx");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite ?? ProceduralArt.White;
            renderer.color = color;
            renderer.sortingOrder = 30;
            return go;
        }

        sealed class FxParticle : MonoBehaviour
        {
            Vector2 velocity;
            float life;
            float gravity;
            float fadeSpeed;
            float age;
            SpriteRenderer spriteRenderer;
            Color startColor;

            public void Init(Vector2 initialVelocity, float lifetime, float gravityValue, float fade)
            {
                velocity = initialVelocity;
                life = Mathf.Max(0.05f, lifetime);
                gravity = gravityValue;
                fadeSpeed = fade;
                spriteRenderer = GetComponent<SpriteRenderer>();
                startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            }

            void Update()
            {
                age += Time.deltaTime;
                velocity.y -= gravity * Time.deltaTime;
                transform.position += (Vector3)(velocity * Time.deltaTime);
                transform.Rotate(0f, 0f, velocity.x * 60f * Time.deltaTime);
                if (spriteRenderer != null)
                {
                    var c = startColor;
                    c.a = Mathf.Clamp01(startColor.a * (1f - age / life) * fadeSpeed);
                    spriteRenderer.color = c;
                }
                if (age >= life) Destroy(gameObject);
            }
        }
    }
}
