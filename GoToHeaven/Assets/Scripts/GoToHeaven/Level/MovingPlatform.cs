using UnityEngine;

namespace GoToHeaven
{
    public sealed class MovingPlatform : MonoBehaviour
    {
        public Vector2 Velocity { get; private set; }
        public float moveX;
        public float moveY = 3f;
        public float speed = 1f;
        public float phase;

        Vector2 origin;
        Vector2 offset;
        float startTime;
        Rigidbody2D body;
        Collider2D surface;
        PlayerController player;

        public void Bind(PlayerController target)
        {
            player = target;
            origin = transform.position;
            offset = new Vector2(moveX, moveY);
            startTime = Time.time;
            body = GetComponent<Rigidbody2D>();
            surface = GetComponent<Collider2D>();
            if (body != null)
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        void FixedUpdate()
        {
            if (body == null) return;

            float t = (Time.time - startTime) * Mathf.Max(0.05f, speed) + phase;
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
}
