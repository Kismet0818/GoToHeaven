using UnityEngine;

namespace GoToHeaven
{
    public sealed class SawTrap : MonoBehaviour
    {
        public float moveX;
        public float moveY = 3f;
        public float speed = 1.5f;
        public float phase;

        Vector2 origin;
        Vector2 offset;
        float startTime;

        public void Bind()
        {
            origin = transform.position;
            offset = new Vector2(moveX, moveY);
            startTime = Time.time;
        }

        void Update()
        {
            float t = (Time.time - startTime) * Mathf.Max(0.05f, speed) + phase;
            transform.position = origin + offset * (0.5f - 0.5f * Mathf.Cos(t));
            transform.Rotate(0f, 0f, 220f * Time.deltaTime);
        }
    }
}
