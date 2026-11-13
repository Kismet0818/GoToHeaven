using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GoToHeaven
{
    /// <summary>Hover / press scaling for prefab buttons.</summary>
    public sealed class UIButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Action Hover;

        Vector3 target = Vector3.one;
        float hoverScale = 1.035f;
        float pressScale = 0.97f;
        bool hovering;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            target = Vector3.one * hoverScale;
            Hover?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            target = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData) => target = Vector3.one * pressScale;

        public void OnPointerUp(PointerEventData eventData) => target = Vector3.one * (hovering ? hoverScale : 1f);

        void OnDisable()
        {
            transform.localScale = Vector3.one;
            target = Vector3.one;
            hovering = false;
        }

        void Update()
        {
            float t = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, target, t);
        }
    }
}
