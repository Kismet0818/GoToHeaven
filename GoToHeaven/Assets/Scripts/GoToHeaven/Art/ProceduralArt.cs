using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 运行时的小型美术工厂：生成 UI 底框、阴影、爱心、星星以及视差山峦等
    /// 不属于像素美术包的贴图。
    /// 所有贴图只在首次用到时绘制一次，随后走缓存。
    /// </summary>
    public static class ProceduralArt
    {
        // 缓存键包含尺寸、颜色等全部参数，因此相同参数的贴图只会被绘制一次。
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>4x4 纯白精灵：缺图时的兜底贴图与粒子基础图，4 PPU 让 1 像素对应 0.25 单位。</summary>
        public static Sprite White => GetOrCreate("white", () =>
        {
            var tex = NewTexture(4, 4, Color.white);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
        });

        /// <summary>竖直渐变背景：宽度只要 2 像素，横向拉伸后不会出现接缝。</summary>
        public static Sprite Gradient(string key, Color top, Color bottom, int height = 128)
        {
            // 键里带上高度与调用方给的 key，不同色调、不同高度的背景各自缓存一份。
            string id = $"gradient|{key}|{height}";
            return GetOrCreate(id, () =>
            {
                var pixels = new Color32[2 * height];
                for (int y = 0; y < height; y++)
                {
                    // t 自底向上从 0 到 1，逐行插值出整条渐变；同一行两像素同色。
                    float t = y / (float)(height - 1);
                    var color = Color.Lerp(bottom, top, t);
                    pixels[y * 2] = color;
                    pixels[y * 2 + 1] = color;
                }
                var tex = new Texture2D(2, height, TextureFormat.RGBA32, false);
                // 点采样 + Clamp：像素风不糊，拉伸时边缘也不会重复采样。
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.SetPixels32(pixels);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>UI 面板底：64x64 圆角矩形，120 PPU，九宫格边距 18 像素供 Image 拉伸。</summary>
        public static Sprite Panel(Color fill, Color border)
        {
            // 颜色相同的面板共用一张贴图，键里只放填充色与描边色的十六进制值。
            string id = $"panel|{ColorUtility.ToHtmlStringRGBA(fill)}|{ColorUtility.ToHtmlStringRGBA(border)}";
            return GetOrCreate(id, () =>
            {
                // 圆角半径 10 像素、描边 3 像素。
                var tex = RoundedRectTexture(64, 64, 10, fill, border, 3);
                var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 120f,
                    0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
                sprite.name = "panel";
                return sprite;
            });
        }

        /// <summary>UI 按钮底：48x48 圆角矩形，圆角与描边都比面板更小，九宫格边距 12。</summary>
        public static Sprite Button(Color fill, Color border)
        {
            string id = $"button|{ColorUtility.ToHtmlStringRGBA(fill)}|{ColorUtility.ToHtmlStringRGBA(border)}";
            return GetOrCreate(id, () =>
            {
                var tex = RoundedRectTexture(48, 48, 8, fill, border, 2);
                var sprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f), 100f,
                    0, SpriteMeshType.FullRect, new Vector4(12, 12, 12, 12));
                sprite.name = "button";
                return sprite;
            });
        }

        /// <summary>爱心：用隐式方程 (x²+y²-1)³ - x²y³ ≤ 0 逐像素填充，左上角点几笔高光。</summary>
        public static Sprite Heart(Color fill)
        {
            string id = $"heart|{ColorUtility.ToHtmlStringRGBA(fill)}";
            return GetOrCreate(id, () =>
            {
                const int w = 20, h = 18;
                var tex = NewTexture(w, h, Color.clear);
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // 归一化到 [-1, 1] 后把 nx 再放宽 15%，避免爱心看起来太瘦。
                        float nx = (x / (w - 1f)) * 2f - 1f;
                        float ny = (y / (h - 1f)) * 2f - 1f;
                        nx *= 1.15f;
                        float a = nx * nx + ny * ny - 1f;
                        if (a * a * a - nx * nx * ny * ny * ny <= 0f)
                        {
                            // 左上角按每 3 像素一道斜纹提亮，模拟果冻质感的反光。
                            bool highlight = x < w * 0.36f && y > h * 0.56f && ((x + y) % 3 == 0);
                            tex.SetPixel(x, y, highlight ? Color.Lerp(fill, Color.white, 0.55f) : fill);
                        }
                    }
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 20f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>五角星：先算 10 个长短半径交替的顶点，再用多边形填充逐像素判定。</summary>
        public static Sprite Star(Color fill)
        {
            string id = $"star|{ColorUtility.ToHtmlStringRGBA(fill)}";
            return GetOrCreate(id, () =>
            {
                const int size = 32;
                var tex = NewTexture(size, size, Color.clear);
                var points = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    // 半径在外尖 1 与内凹 0.45 之间交替，角度从正上方起每 36° 取一个顶点。
                    float r = i % 2 == 0 ? 1f : 0.45f;
                    float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    points[i] = new Vector2(0.5f + Mathf.Cos(angle) * r * 0.48f,
                                            0.5f + Mathf.Sin(angle) * r * 0.48f);
                }
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        // 下半部分（y 较小）提亮一档，制造上暗下亮的体积感。
                        if (PointInPolygon(new Vector2((x + 0.5f) / size, (y + 0.5f) / size), points))
                            tex.SetPixel(x, y, y < size * 0.35f ? Color.Lerp(fill, Color.white, 0.25f) : fill);
                    }
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>小锁图标：下方 60% 高度是锁体，上方用圆环表示锁梁。</summary>
        public static Sprite Lock(Color fill)
        {
            return GetOrCreate("lock", () =>
            {
                const int w = 18, h = 22;
                var tex = NewTexture(w, h, Color.clear);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 锁梁以 (中心x, 0.66h) 为圆心、半径在 0.24w~0.36w 之间，形成空心圆环。
                    bool body = y <= h * 0.6f;
                    float cx = x - (w - 1) * 0.5f;
                    float ring = Mathf.Sqrt(cx * cx + (y - h * 0.66f) * (y - h * 0.66f));
                    bool loop = y > h * 0.6f && ring > w * 0.24f && ring < w * 0.36f;
                    if (body || loop) tex.SetPixel(x, y, fill);
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 18f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>实心圆：按像素中心到圆心的距离判定；PPU 等于直径，故精灵正好 1 单位见方。</summary>
        public static Sprite Circle(int size, Color fill)
        {
            return GetOrCreate($"circle|{size}|{ColorUtility.ToHtmlStringRGBA(fill)}", () =>
            {
                var tex = NewTexture(size, size, Color.clear);
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // 用像素中心 (+0.5) 计算距离，边缘才不会偏向一侧。
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    if (dx * dx + dy * dy <= r * r) tex.SetPixel(x, y, fill);
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>椭圆软阴影：中心最深、向边缘线性透明，垫在角色脚下压住画面。</summary>
        public static Sprite Shadow()
        {
            return GetOrCreate("shadow", () =>
            {
                const int w = 32, h = 12;
                var tex = NewTexture(w, h, Color.clear);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // 归一化距离 d 同时控制范围（≤1 才绘制）与透明度，边缘自然淡出。
                    float nx = (x + 0.5f) / w * 2f - 1f;
                    float ny = (y + 0.5f) / h * 2f - 1f;
                    float d = nx * nx + ny * ny;
                    if (d <= 1f) tex.SetPixel(x, y, new Color(0.05f, 0.05f, 0.12f, 0.42f * (1f - d)));
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>视差云朵：四个圆的并集，底部 10 像素偏冷色，当作背光面。</summary>
        public static Sprite Cloud()
        {
            return GetOrCreate("cloud", () =>
            {
                const int w = 64, h = 32;
                var tex = NewTexture(w, h, Color.clear);
                // 圆心与半径写死，保证同一种云在各场景里形状完全一致。
                var circles = new[]
                {
                    new Vector3(14, 14, 10), new Vector3(28, 18, 13),
                    new Vector3(43, 15, 11), new Vector3(54, 13, 8)
                };
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    foreach (var c in circles)
                    {
                        // 命中任一圆即填色并跳出，避免同一像素被重复处理。
                        float dx = x - c.x, dy = y - c.y;
                        if (dx * dx + dy * dy <= c.z * c.z)
                        {
                            tex.SetPixel(x, y, y < 10 ? new Color(0.88f, 0.93f, 1f, 1f) : Color.white);
                            break;
                        }
                    }
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>宽幅剪影，供视差山峦条带使用。</summary>
        public static Sprite Hills(string key, Color color, int seed)
        {
            return GetOrCreate($"hills|{key}|{seed}|{ColorUtility.ToHtmlStringRGBA(color)}", () =>
            {
                const int w = 512, h = 160;
                var tex = NewTexture(w, h, Color.clear);
                // 用种子决定初相位：同一 seed 的山脊可复现，换 seed 得到不同轮廓。
                var rng = new System.Random(seed);
                float phase = (float)rng.NextDouble() * 10f;
                for (int x = 0; x < w; x++)
                {
                    // 三层正弦叠加（1、3、6 个周期）分别做大起伏、中景细节与细碎抖动。
                    float t = x / (float)w;
                    float ridge = 0.34f
                        + Mathf.Sin(t * Mathf.PI * 2f + phase) * 0.10f
                        + Mathf.Sin(t * Mathf.PI * 6f + phase * 1.7f) * 0.05f
                        + Mathf.Sin(t * Mathf.PI * 12f + phase * 0.4f) * 0.02f;
                    int top = Mathf.Clamp(Mathf.RoundToInt(ridge * h), 2, h - 2);
                    for (int y = 0; y < top; y++)
                    {
                        // 越往山脚（y 越小）颜色越沉，模拟谷底的阴影。
                        float depth = 1f - y / (float)top;
                        var c = Color.Lerp(color, color * 0.72f, depth);
                        c.a = 1f;
                        tex.SetPixel(x, y, c);
                    }
                    // 山脊那一行提亮 28%，作为轮廓光把山与天空分开。
                    tex.SetPixel(x, top, Color.Lerp(color, Color.white, 0.28f));
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 8f, 0, SpriteMeshType.FullRect);
            });
        }

        /// <summary>暂停图标：16x16 内两条 3 像素宽的竖条，上下各留 3 像素内边距。</summary>
        public static Sprite PauseIcon(Color color)
        {
            return GetOrCreate($"pause|{ColorUtility.ToHtmlStringRGBA(color)}", () =>
            {
                const int w = 16, h = 16;
                var tex = NewTexture(w, h, Color.clear);
                for (int y = 3; y < h - 3; y++)
                {
                    for (int x = 3; x < 6; x++) tex.SetPixel(x, y, color);
                    for (int x = 10; x < 13; x++) tex.SetPixel(x, y, color);
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            });
        }

        // 统一创建点采样、Clamp 的 RGBA32 贴图：先一次填满像素再 Apply，避免逐像素 SetPixel 的开销。
        static Texture2D NewTexture(int width, int height, Color fill)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        // 先按外圆角矩形整块铺描边色，再把内缩 borderWidth 的圆角区域盖成填充色，留出的边缘即描边。
        static Texture2D RoundedRectTexture(int w, int h, int radius, Color fill, Color border, int borderWidth)
        {
            var tex = NewTexture(w, h, Color.clear);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inside = InsideRoundedRect(x, y, w, h, radius);
                bool inner = InsideRoundedRect(x + borderWidth, y + borderWidth, w - borderWidth * 2, h - borderWidth * 2, Mathf.Max(1, radius - borderWidth));
                if (inside) tex.SetPixel(x, y, inner ? fill : border);
            }
            tex.Apply();
            return tex;
        }

        // 把点夹进「四角圆心围成的矩形」再比较到圆心的距离；宽高不足时直接判否，避免无效计算。
        static bool InsideRoundedRect(int x, int y, int w, int h, int r)
        {
            if (w <= 0 || h <= 0) return false;
            int cx = Mathf.Clamp(x, r, w - r - 1);
            int cy = Mathf.Clamp(y, r, h - r - 1);
            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        // 射线法：统计测试点向右的水平线与多边形各边的交点数量，奇数即在内部。
        static bool PointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
                    point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
                    (polygon[j].y - polygon[i].y) + polygon[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        // 命中缓存直接返回；未命中才执行工厂并写回，保证同一参数只真正绘制一次。
        static Sprite GetOrCreate(string key, System.Func<Sprite> factory)
        {
            if (cache.TryGetValue(key, out var sprite)) return sprite;
            sprite = factory();
            cache[key] = sprite;
            return sprite;
        }
    }
}
