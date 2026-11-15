using UnityEngine;

namespace GoToHeaven
{
    /// <summary>运行时构建 UI 的共享调色板：界面代码只从这里取色，避免各界面写死颜色而不一致。</summary>
    public static class UITheme
    {
        // 深色底：用于遮罩、面板压暗等场景
        public static readonly Color Ink = Hex("#121420");
        // 面板底色：普通弹窗的主背景
        public static readonly Color Panel = Hex("#1D2236");
        // 浅一档的面板色：用于面板内的次级区块或分隔条
        public static readonly Color PanelLight = Hex("#2A3350");
        // 米白：界面主文字色，比纯白更柔和，深色底上不易刺眼
        public static readonly Color Cream = Hex("#FFF4D6");
        // 弱化文字色：未解锁、次要信息等不希望抢视觉焦点的文本
        public static readonly Color Muted = Hex("#A8BCD4");
        // 金色：通关、最高纪录等正向强调
        public static readonly Color Gold = Hex("#FFD166");
        // 橙色：次一级强调，如关卡序号高亮
        public static readonly Color Orange = Hex("#F4A259");
        // 危险红：失败标题、生命值告警
        public static readonly Color Danger = Hex("#E45757");
        // 通过/成功绿：用于正向状态标记
        public static readonly Color Green = Hex("#7BC96F");
        // 天空蓝：天堂主题的点缀色
        public static readonly Color Sky = Hex("#8FE3FF");
        // 半透明压暗层：alpha 0.72，既能压住背后画面又保留隐约轮廓
        public static readonly Color Overlay = new Color(0.035f, 0.04f, 0.08f, 0.72f);

        // 解析 #RRGGBB 颜色串；解析失败回落品红，便于在编辑器里一眼发现配错的色值
        public static Color Hex(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.magenta;
        }
    }
}
