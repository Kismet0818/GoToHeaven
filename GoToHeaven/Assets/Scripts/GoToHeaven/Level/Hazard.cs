using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 伤害触发区：挂在尖刺、锯片等 Trigger 碰撞体上，玩家接触即扣血。
    /// 只认 PlayerHealth，其他碰撞体进入不会掉血；单次伤害量由 Inspector 的 damage 决定。
    /// </summary>
    public sealed class Hazard : MonoBehaviour
    {
        /// <summary>单次扣除的心数，1 表示一颗心；调高可让该陷阱更致命。</summary>
        public int damage = 1;

        // 进入瞬间先扣一次：PlayerHealth 自带无敌帧与重生保护，重复接触不会叠加伤害。
        void OnTriggerEnter2D(Collider2D other)
        {
            var health = other.GetComponent<PlayerHealth>();
            if (health != null) health.TakeDamage(damage);
        }

        // 持续接触时逐物理帧补伤害；触发频率由 PlayerHealth 的无敌帧控制，这里只排除
        // 重生与死亡状态，避免在复活动画里被同一陷阱连续打死。
        void OnTriggerStay2D(Collider2D other)
        {
            var health = other.GetComponent<PlayerHealth>();
            if (health != null && !health.IsInvulnerable && !health.IsRespawning && !health.Depleted)
                health.TakeDamage(damage);
        }
    }
}
