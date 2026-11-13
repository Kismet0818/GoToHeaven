using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Smooth follow camera with level bounds, look-ahead and trauma shake.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        public static CameraFollow Instance { get; private set; }

        public Transform Target { get; private set; }
        public Rect Bounds { get; private set; }
        public bool HasBounds { get; private set; }

        Camera cam;
        Vector3 velocity;
        Vector3 shakeOffset;
        float shakeTime;
        float shakeDuration;
        float shakeStrength;
        float lookAhead = 1.1f;
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

        public void Configure(float orthographicSize)
        {
            if (cam == null) cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        public void SetTarget(Transform target, Rect bounds, bool snap = true)
        {
            Target = target;
            Bounds = bounds;
            HasBounds = true;
            if (snap) SnapToTarget();
        }

        public void ClearTarget()
        {
            Target = null;
            HasBounds = false;
        }

        public void SnapToTarget()
        {
            if (Target == null) return;
            transform.position = ComputeDesired(instant: true);
            velocity = Vector3.zero;
        }

        public static void Shake(float strength, float duration)
        {
            if (Instance != null) Instance.StartShake(strength, duration);
        }

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
            float x = Mathf.SmoothDamp(current.x, desired.x, ref velocity.x, smoothX);
            float y = Mathf.SmoothDamp(current.y, desired.y, ref velocity.y, smoothY);

            if (shakeTime > 0f)
            {
                shakeTime = Mathf.Max(0f, shakeTime - Time.unscaledDeltaTime);
                float falloff = shakeDuration > 0f ? shakeTime / shakeDuration : 0f;
                shakeOffset = (Vector3)(Random.insideUnitCircle * (shakeStrength * falloff));
            }
            else
            {
                shakeOffset = Vector3.zero;
                shakeStrength = 0f;
                shakeDuration = 0f;
            }

            transform.position = new Vector3(x, y, -10f) + shakeOffset;
        }

        Vector3 ComputeDesired(bool instant)
        {
            if (Target == null) return transform.position;

            float facing = 0f;
            var player = Target.GetComponent<PlayerController>();
            if (player != null) facing = player.Facing * lookAhead;

            float x = Target.position.x + facing;
            float y = Target.position.y + 1.5f;

            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            if (HasBounds)
            {
                if (Bounds.width > halfWidth * 2f)
                    x = Mathf.Clamp(x, Bounds.xMin + halfWidth, Bounds.xMax - halfWidth);
                else
                    x = Bounds.center.x;

                if (Bounds.height > halfHeight * 2f)
                    y = Mathf.Clamp(y, Bounds.yMin + halfHeight, Bounds.yMax - halfHeight);
                else
                    y = Bounds.center.y;
            }

            return new Vector3(x, y, -10f);
        }
    }
}
