using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Shared palette for the runtime built UI.</summary>
    public static class UITheme
    {
        public static readonly Color Ink = Hex("#121420");
        public static readonly Color Panel = Hex("#1D2236");
        public static readonly Color PanelLight = Hex("#2A3350");
        public static readonly Color Cream = Hex("#FFF4D6");
        public static readonly Color Muted = Hex("#A8BCD4");
        public static readonly Color Gold = Hex("#FFD166");
        public static readonly Color Orange = Hex("#F4A259");
        public static readonly Color Danger = Hex("#E45757");
        public static readonly Color Green = Hex("#7BC96F");
        public static readonly Color Sky = Hex("#8FE3FF");
        public static readonly Color Overlay = new Color(0.035f, 0.04f, 0.08f, 0.72f);

        public static Color Hex(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.magenta;
        }
    }
}
