using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 平台标记组件：本身没有运行逻辑，只把一块平台的多个 Collider 归到同一个根节点。
    /// 玩家下穿时 PlayerController 通过 GetComponentInParent&lt;Platform&gt;() 找到整块平台，
    /// 再统一忽略其全部碰撞体，避免多段拼接的平台漏掉某一段。
    /// </summary>
    public sealed class Platform : MonoBehaviour
    {
        /// <summary>编辑器标记：勾选表示单向平台；实际单向效果由 OneWay 层与 PlatformEffector2D 决定。</summary>
        public bool OneWay;
    }
}
