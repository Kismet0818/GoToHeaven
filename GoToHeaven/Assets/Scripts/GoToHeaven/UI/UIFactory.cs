using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>Helpers that keep the runtime UI code short and consistent.</summary>
    public static class UIFactory
    {
        public static TMP_FontAsset Font;
        public static Sprite PanelSprite => ProceduralArt.Panel(UITheme.Panel, UITheme.Gold);
        public static Sprite CardSprite => ProceduralArt.Panel(UITheme.PanelLight, new Color(1f, 1f, 1f, 0.12f));
        public static Sprite ButtonSprite => ProceduralArt.Button(UITheme.PanelLight, new Color(1f, 0.95f, 0.8f, 0.55f));

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(100f, 100f);
            return rt;
        }

        public static RectTransform FullScreen(string name, Transform parent)
        {
            var rt = Rect(name, parent);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            image.preserveAspect = false;
            if (sprite != null && sprite.border != Vector4.zero)
                image.type = UnityEngine.UI.Image.Type.Sliced;
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string content, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var rt = Rect(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            return text;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string content, float size, Color color,
            Vector2 anchor, Vector2 position, Vector2 boxSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = Text(parent, name, content, size, color, alignment);
            Place(text.rectTransform, anchor, new Vector2(0.5f, 0.5f), position, boxSize);
            return text;
        }

        public static Button Button(Transform parent, string name, string label, Sprite icon, Vector2 size,
            Action onClick, bool primary = false, Action onHover = null)
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = size;

            var background = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.sprite = primary ? ProceduralArt.Button(UITheme.Gold, UITheme.Cream) : ButtonSprite;
            background.type = UnityEngine.UI.Image.Type.Sliced;
            background.color = Color.white;
            background.raycastTarget = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.93f, 0.78f, 1f),
                pressedColor = new Color(0.86f, 0.78f, 0.62f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.5f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var labelColor = primary ? UITheme.Ink : UITheme.Cream;
            float labelOffset = 0f;
            if (icon != null)
            {
                var iconImage = Image(rt, "Icon", icon, primary ? UITheme.Ink : UITheme.Cream);
                iconImage.preserveAspect = true;
                iconImage.rectTransform.anchorMin = iconImage.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                iconImage.rectTransform.pivot = new Vector2(0f, 0.5f);
                iconImage.rectTransform.anchoredPosition = new Vector2(22f, 0f);
                iconImage.rectTransform.sizeDelta = new Vector2(36f, 36f);
                labelOffset = 60f;
            }

            var text = Text(rt, "Label", label, primary ? 32f : 30f, labelColor,
                icon != null ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(labelOffset + 16f, 0f);
            text.rectTransform.offsetMax = new Vector2(-18f, 0f);

            var fx = rt.gameObject.AddComponent<UIButtonFX>();
            fx.Hover = onHover;
            button.onClick.AddListener(() =>
            {
                GameManager.Instance?.Audio?.Play(Sfx.Click);
                onClick?.Invoke();
            });
            return button;
        }

        public static Slider Slider(Transform parent, string name, float value, Action<float> onChange,
            Vector2 anchor, Vector2 position, float width)
        {
            var rt = Rect(name, parent);
            Place(rt, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(width, 28f));

            var background = Image(rt, "Background", ProceduralArt.Panel(UITheme.Ink, new Color(1f, 1f, 1f, 0.08f)), Color.white);
            background.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            background.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            background.rectTransform.sizeDelta = new Vector2(0f, 12f);
            background.rectTransform.anchoredPosition = Vector2.zero;
            background.type = UnityEngine.UI.Image.Type.Sliced;

            var fillArea = Rect("Fill Area", rt);
            fillArea.anchorMin = new Vector2(0f, 0.5f);
            fillArea.anchorMax = new Vector2(1f, 0.5f);
            fillArea.sizeDelta = new Vector2(-18f, 12f);
            fillArea.anchoredPosition = new Vector2(-9f, 0f);

            var fill = Image(fillArea, "Fill", ProceduralArt.Button(UITheme.Gold, UITheme.Gold), Color.white);
            fill.type = UnityEngine.UI.Image.Type.Sliced;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            var handleArea = Rect("Handle Slide Area", rt);
            handleArea.anchorMin = Vector2.zero;
            handleArea.anchorMax = Vector2.one;
            handleArea.offsetMin = new Vector2(10f, 0f);
            handleArea.offsetMax = new Vector2(-10f, 0f);

            var handle = Image(handleArea, "Handle", ProceduralArt.Circle(20, UITheme.Cream), Color.white, true);
            handle.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            handle.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(24f, 24f);
            handle.rectTransform.anchoredPosition = Vector2.zero;

            var slider = rt.gameObject.AddComponent<UnityEngine.UI.Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(value);
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.onValueChanged.AddListener(v => onChange?.Invoke(v));
            return slider;
        }

        public static Button Toggle(Transform parent, string name, bool initial, Action<bool> onChange, out Image check,
            Vector2 anchor, Vector2 position)
        {
            var rt = Rect(name, parent);
            Place(rt, anchor, new Vector2(0.5f, 0.5f), position, new Vector2(46f, 46f));

            var box = rt.gameObject.AddComponent<UnityEngine.UI.Image>();
            box.sprite = ProceduralArt.Button(UITheme.Ink, new Color(1f, 1f, 1f, 0.14f));
            box.type = UnityEngine.UI.Image.Type.Sliced;
            box.color = Color.white;
            box.raycastTarget = true;

            check = Image(rt, "Check", ProceduralArt.Panel(UITheme.Green, UITheme.Green), UITheme.Green, false);
            check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            check.rectTransform.sizeDelta = new Vector2(24f, 24f);
            check.rectTransform.anchoredPosition = Vector2.zero;
            check.type = UnityEngine.UI.Image.Type.Sliced;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = box;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            bool value = initial;
            check.gameObject.SetActive(value);
            var checkImage = check;
            button.onClick.AddListener(() =>
            {
                value = !value;
                checkImage.gameObject.SetActive(value);
                GameManager.Instance?.Audio?.Play(Sfx.Click);
                onChange?.Invoke(value);
            });
            return button;
        }

        public static CanvasGroup Group(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null) group = go.AddComponent<CanvasGroup>();
            return group;
        }
    }

    /// <summary>Hover / press scaling and optional hover callback.</summary>
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

    /// <summary>Cycles a UI Image through a sprite strip (used for the menu hero).</summary>
    public sealed class UIAnimatedImage : MonoBehaviour
    {
        public Image Target;
        public Sprite[] Frames;
        public float Fps = 10f;
        int index;
        float timer;

        void Update()
        {
            if (Target == null || Frames == null || Frames.Length == 0) return;
            timer += Time.unscaledDeltaTime;
            float frameTime = 1f / Mathf.Max(1f, Fps);
            while (timer >= frameTime)
            {
                timer -= frameTime;
                index = (index + 1) % Frames.Length;
                Target.sprite = Frames[index];
            }
        }
    }
}
