using UnityEngine;

namespace GoToHeaven
{
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
}
