using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GoToHeaven
{
    /// <summary>prefab 按钮的悬停 / 按下缩放反馈；只改 localScale，不动布局。</summary>
    public sealed class UIButtonFX : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        // 悬停时的回调钩子（通常用来播音效），由界面代码在外部挂接
        public Action Hover;

        Vector3 target = Vector3.one;
        // 悬停放大 3.5%：幅度必须很小，否则相邻按钮边缘会互相穿插
        float hoverScale = 1.035f;
        // 按下缩到 97%：制造“被按进去”的手感
        float pressScale = 0.97f;
        // 记录是否仍悬停：鼠标在按钮上按下再松开时，要回到悬停尺寸而不是原始尺寸
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
            // 界面被隐藏时复位，否则下次显示会带着上次的缩放值“弹”一下
            transform.localScale = Vector3.one;
            target = Vector3.one;
            hovering = false;
        }

        void Update()
        {
            // 指数平滑：t = 1 - e^(-k*dt)，与帧率无关，约 0.1 秒内跟到位
            float t = 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, target, t);
        }
    }
}
