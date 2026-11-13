using UnityEngine;

namespace GoToHeaven
{
    public sealed class PlayerSpawn : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1f);
        }
    }
}
