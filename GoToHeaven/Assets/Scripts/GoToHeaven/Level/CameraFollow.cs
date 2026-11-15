using UnityEngine;

namespace GoToHeaven
{
    /// <summary>带关卡边界约束、朝向前瞻与震屏的平滑跟随相机。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        /// <summary>当前场景中的唯一实例，供静态震屏入口使用。</summary>
        public static CameraFollow Instance { get; private set; }

        /// <summary>跟随目标，通常是玩家。</summary>
        public Transform Target { get; private set; }
        /// <summary>相机中心允许活动的世界矩形。</summary>
        public Rect Bounds { get; private set; }
        /// <summary>是否已设置边界；为 false 时相机不做裁剪，自由跟随。</summary>
        public bool HasBounds { get; private set; }

        Camera cam;
        Vector3 velocity;
        Vector3 shakeOffset;
        float shakeTime;
        float shakeDuration;
        float shakeStrength;
        // 朝向前瞻距离 1.1 个单位：视线略微超前于角色，让玩家看到将要走去的方向。
        float lookAhead = 1.1f;
        // 水平 0.16、垂直 0.24 的平滑时间：横向跟得紧，纵向更软，跳跃时镜头不会上下抖。
        float smoothX = 0.16f;
        float smoothY = 0.24f;

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>切换为正交相机并设置可视半高（世界单位）。</summary>
        public void Configure(float orthographicSize)
        {
            if (cam == null) cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            // 纯色清屏：背景画面由 Backdrop 负责，不用天空盒。
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        /// <summary>设置跟随目标与边界；snap 为 true 时立即对齐，不做平滑过渡。</summary>
        public void SetTarget(Transform target, Rect bounds, bool snap = true)
        {
            Target = target;
            Bounds = bounds;
            HasBounds = true;
            if (snap) SnapToTarget();
        }

        /// <summary>清空跟随目标，相机停在原地不再移动。</summary>
        public void ClearTarget()
        {
            Target = null;
            HasBounds = false;
        }

        /// <summary>忽略平滑，直接把相机放到目标位置（复活、切关后使用）。</summary>
        public void SnapToTarget()
        {
            if (Target == null) return;
            transform.position = ComputeDesired(instant: true);
            velocity = Vector3.zero;
        }

        /// <summary>静态震屏入口，供手上没有 CameraFollow 引用的调用方使用（如受击、爆炸）。</summary>
        public static void Shake(float strength, float duration)
        {
            if (Instance != null) Instance.StartShake(strength, duration);
        }

        /// <summary>触发一次震屏；强度与时长取当前值和传入值的较大者，重叠触发不会互相削弱。</summary>
        public void StartShake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(shakeStrength, strength);
            shakeDuration = Mathf.Max(shakeDuration, duration);
            shakeTime = shakeDuration;
        }

        void LateUpdate()
        {
            if (Target == null) return;

            var desired = ComputeDesired(instant: false);
            var current = transform.position;
            // x 与 y 分别做 SmoothDamp，两轴用各自的平滑时间，互不牵连。
            float x = Mathf.SmoothDamp(current.x, desired.x, ref velocity.x, smoothX);
            float y = Mathf.SmoothDamp(current.y, desired.y, ref velocity.y, smoothY);

            if (shakeTime > 0f)
            {
                // 用 unscaledDeltaTime 计时，暂停（timeScale = 0）时震屏依然能抖完。
                shakeTime = Mathf.Max(0f, shakeTime - Time.unscaledDeltaTime);
                // 按剩余时间线性衰减到 0，收尾自然，不会突然停在偏移位置上。
                float falloff = shakeDuration > 0f ? shakeTime / shakeDuration : 0f;
                shakeOffset = (Vector3)(Random.insideUnitCircle * (shakeStrength * falloff));
            }
            else
            {
                // 抖完立刻清零参数，避免下一次震屏沿用上一轮的残留强度或时长。
                shakeOffset = Vector3.zero;
                shakeStrength = 0f;
                shakeDuration = 0f;
            }

            // Z 固定 -10：2D 相机要站在所有物件之前，才能拍到排序层为负的背景。
            transform.position = new Vector3(x, y, -10f) + shakeOffset;
        }

        /// <summary>算出相机“想去”的位置：先做朝向前瞻与高度偏移，再按关卡边界裁剪。</summary>
        Vector3 ComputeDesired(bool instant)
        {
            if (Target == null) return transform.position;

            float facing = 0f;
            var player = Target.GetComponent<PlayerController>();
            if (player != null) facing = player.Facing * lookAhead;

            float x = Target.position.x + facing;
            // 抬高 1.5 个单位：镜头略偏上，角色落在画面下三分之一处，露出更多前方地形。
            float y = Target.position.y + 1.5f;

            // 正交半高就是 orthographicSize，半宽再用宽高比换算。
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            if (HasBounds)
            {
                // 关卡比视口更宽时才允许横向移动，否则直接锁在水平中心。
                if (Bounds.width > halfWidth * 2f)
                    x = Mathf.Clamp(x, Bounds.xMin + halfWidth, Bounds.xMax - halfWidth);
                else
                    x = Bounds.center.x;

                // 纵向同理：关卡不够高时把相机锁死在垂直中心。
                if (Bounds.height > halfHeight * 2f)
                    y = Mathf.Clamp(y, Bounds.yMin + halfHeight, Bounds.yMax - halfHeight);
                else
                    y = Bounds.center.y;
            }

            return new Vector3(x, y, -10f);
        }
    }
}
