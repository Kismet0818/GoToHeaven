using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 锯齿陷阱：沿 offset 做余弦往返并持续自转；伤害由同一物体上的 Hazard 触发器负责。
    /// 本类只负责移动与旋转，因此可以和任意 Hazard 组合复用。
    /// </summary>
    public sealed class SawTrap : MonoBehaviour
    {
        /// <summary>水平位移量（单位）：锯片在起点与「起点 + moveX」之间往返。</summary>
        public float moveX;
        /// <summary>垂直位移量（单位）：默认 3，即从起点向上往返 3 个单位。</summary>
        public float moveY = 3f;
        /// <summary>余弦相位推进速度（弧度/秒）：默认 1.5，越大往返越频繁、越难躲。</summary>
        public float speed = 1.5f;
        /// <summary>相位偏移（弧度）：让多个锯片错开，1.57 ≈ 从行程中点开始。</summary>
        public float phase;

        Vector2 origin;
        Vector2 offset;
        float startTime;

        /// <summary>由 LevelController 在初始化时调用：记录起点、位移范围与起始时间，之后按时间自行往返。</summary>
        public void Bind()
        {
            origin = transform.position;
            offset = new Vector2(moveX, moveY);
            startTime = Time.time;
        }

        void Update()
        {
            // 余弦往返：0.5 - 0.5*cos(t) 把相位映射到 0..1，两端自然减速；0.05 下限防 speed 被填 0 后卡死。
            float t = (Time.time - startTime) * Mathf.Max(0.05f, speed) + phase;
            transform.position = origin + offset * (0.5f - 0.5f * Mathf.Cos(t));
            // 220 度/秒的自转只是视觉表现，不影响 Hazard 的伤害判定。
            transform.Rotate(0f, 0f, 220f * Time.deltaTime);
        }
    }
}
