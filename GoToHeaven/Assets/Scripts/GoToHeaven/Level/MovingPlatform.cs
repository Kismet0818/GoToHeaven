using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 往返移动平台：以起点为原点，按余弦曲线在 offset 范围内平滑往返。
    /// 平台设为 Kinematic 并用 MovePosition 推进，同时把自身位移直接传给站在上面的玩家。
    /// </summary>
    public sealed class MovingPlatform : MonoBehaviour
    {
        /// <summary>最近一次物理步进算出的平台速度（单位/秒），供外部读取或调试。</summary>
        public Vector2 Velocity { get; private set; }
        /// <summary>水平位移量（单位）：平台在起点与「起点 + moveX」之间往返。</summary>
        public float moveX;
        /// <summary>垂直位移量（单位）：默认 3，即从起点向上往返 3 个单位。</summary>
        public float moveY = 3f;
        /// <summary>余弦相位推进速度（弧度/秒），1 时一个往返约 6.3 秒；越大越快。</summary>
        public float speed = 1f;
        /// <summary>相位偏移（弧度）：让多块平台错开节奏，1.57 表示从行程中点出发。</summary>
        public float phase;

        Vector2 origin;
        Vector2 offset;
        float startTime;
        Rigidbody2D body;
        Collider2D surface;
        PlayerController player;

        /// <summary>
        /// 由 LevelController 在玩家生成后调用：记录起点与位移范围，并缓存刚体与碰撞体。
        /// 平台强制为 Kinematic 并开启插值：不受重力与玩家推力影响，低速移动也不会抖动。
        /// </summary>
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

            // 相位推进：speed 越大往返越快；0.05 下限避免 Inspector 误填 0 后平台卡死。
            float t = (Time.time - startTime) * Mathf.Max(0.05f, speed) + phase;
            // 0.5 - 0.5*cos(t) 把余弦压到 0..1，两端速度为零，起步和到点都不突兀。
            Vector2 target = origin + offset * (0.5f - 0.5f * Mathf.Cos(t));
            Vector2 delta = target - body.position;
            body.MovePosition(target);
            // 位移按固定步长换算成速度；0.0001 只是防御 fixedDeltaTime 为 0 的除零。
            Velocity = delta / Mathf.Max(0.0001f, Time.fixedDeltaTime);

            if (player == null || !player.Grounded || surface == null) return;

            var bounds = surface.bounds;
            var feet = player.transform.position;
            // 用玩家脚点与台面 bounds 比较：水平容差 0.12，垂直允许低 0.18 / 高 0.28，
            // 覆盖贴边站立与刚落地时的插值误差。
            bool standing = feet.x >= bounds.min.x - 0.12f && feet.x <= bounds.max.x + 0.12f &&
                            feet.y >= bounds.max.y - 0.18f && feet.y <= bounds.max.y + 0.28f;
            // Kinematic 平台不会靠摩擦带动玩家，这里直接把平台位移加到玩家身上。
            if (standing) player.CarryBy(delta);
        }
    }
}
