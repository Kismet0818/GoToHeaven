using UnityEngine;

namespace GoToHeaven
{
    /// <summary>关卡预制体里的出生点标记，挂载位置即玩家脚底坐标。</summary>
    public sealed class PlayerSpawn : MonoBehaviour
    {
        /// <summary>只在编辑器里画线框提示，运行时不产生任何逻辑。</summary>
        void OnDrawGizmos()
        {
            // 半透明绿色线框：小球代表角色身体，竖线连回脚底原点，方便在 Scene 里对齐。
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, 0.5f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1f);
        }
    }
}
