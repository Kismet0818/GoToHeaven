using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Small runtime art factory for the UI chrome, shadows, hearts, stars and
    /// parallax shapes that are not part of the pixel art pack.
    /// Everything is generated once and cached.
    /// </summary>
    public static class ProceduralArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite White => GetOrCreate("white", () =>
        {
            var tex = NewTexture(4, 4, Color.white);
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f, 0, SpriteMeshType.FullRect);
        });

        public static Sprite Gradient(string key, Color top, Color bottom, int height = 128)
        {
            string id = $"gradient|{key}|{height}";
            return GetOrCreate(id, () =>
            {
                var pixels = new Color32[2 * height];
                for (int y = 0; y < height; y++)
                {
                    float t = y / (float)(height - 1);
                    var color = Color.Lerp(bottom, top, t);
                    pixels[y * 2] = color;
                    pixels[y * 2 + 1] = color;
                }
                var tex = new Texture2D(2, height, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.SetPixels32(pixels);
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            });
        }

        public static Sprite Panel(Color fill, Color border)
        {
            string id = $"panel|{ColorUtility.ToHtmlStringRGBA(fill)}|{ColorUtility.ToHtmlStringRGBA(border)}";
            return GetOrCreate(id, () =>
            {
                var tex = RoundedRectTexture(64, 64, 10, fill, border, 3);
                var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 120f,
                    0, SpriteMeshType.FullRect, new Vector4(18, 18, 18, 18));
                sprite.name = "panel";
                return sprite;
            });
        }

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
                        float nx = (x / (w - 1f)) * 2f - 1f;
                        float ny = (y / (h - 1f)) * 2f - 1f;
                        nx *= 1.15f;
                        float a = nx * nx + ny * ny - 1f;
                        if (a * a * a - nx * nx * ny * ny * ny <= 0f)
                        {
                            bool highlight = x < w * 0.36f && y > h * 0.56f && ((x + y) % 3 == 0);
                            tex.SetPixel(x, y, highlight ? Color.Lerp(fill, Color.white, 0.55f) : fill);
                        }
                    }
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 20f, 0, SpriteMeshType.FullRect);
            });
        }

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
                    float r = i % 2 == 0 ? 1f : 0.45f;
                    float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                    points[i] = new Vector2(0.5f + Mathf.Cos(angle) * r * 0.48f,
                                            0.5f + Mathf.Sin(angle) * r * 0.48f);
                }
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (PointInPolygon(new Vector2((x + 0.5f) / size, (y + 0.5f) / size), points))
                            tex.SetPixel(x, y, y < size * 0.35f ? Color.Lerp(fill, Color.white, 0.25f) : fill);
                    }
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            });
        }

        public static Sprite Lock(Color fill)
        {
            return GetOrCreate("lock", () =>
            {
                const int w = 18, h = 22;
                var tex = NewTexture(w, h, Color.clear);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
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

        public static Sprite Circle(int size, Color fill)
        {
            return GetOrCreate($"circle|{size}|{ColorUtility.ToHtmlStringRGBA(fill)}", () =>
            {
                var tex = NewTexture(size, size, Color.clear);
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - r;
                    float dy = y + 0.5f - r;
                    if (dx * dx + dy * dy <= r * r) tex.SetPixel(x, y, fill);
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
            });
        }

        public static Sprite Shadow()
        {
            return GetOrCreate("shadow", () =>
            {
                const int w = 32, h = 12;
                var tex = NewTexture(w, h, Color.clear);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float nx = (x + 0.5f) / w * 2f - 1f;
                    float ny = (y + 0.5f) / h * 2f - 1f;
                    float d = nx * nx + ny * ny;
                    if (d <= 1f) tex.SetPixel(x, y, new Color(0.05f, 0.05f, 0.12f, 0.42f * (1f - d)));
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            });
        }

        public static Sprite Cloud()
        {
            return GetOrCreate("cloud", () =>
            {
                const int w = 64, h = 32;
                var tex = NewTexture(w, h, Color.clear);
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

        /// <summary>Wide silhouette used for the parallax mountain strips.</summary>
        public static Sprite Hills(string key, Color color, int seed)
        {
            return GetOrCreate($"hills|{key}|{seed}|{ColorUtility.ToHtmlStringRGBA(color)}", () =>
            {
                const int w = 512, h = 160;
                var tex = NewTexture(w, h, Color.clear);
                var rng = new System.Random(seed);
                float phase = (float)rng.NextDouble() * 10f;
                for (int x = 0; x < w; x++)
                {
                    float t = x / (float)w;
                    float ridge = 0.34f
                        + Mathf.Sin(t * Mathf.PI * 2f + phase) * 0.10f
                        + Mathf.Sin(t * Mathf.PI * 6f + phase * 1.7f) * 0.05f
                        + Mathf.Sin(t * Mathf.PI * 12f + phase * 0.4f) * 0.02f;
                    int top = Mathf.Clamp(Mathf.RoundToInt(ridge * h), 2, h - 2);
                    for (int y = 0; y < top; y++)
                    {
                        float depth = 1f - y / (float)top;
                        var c = Color.Lerp(color, color * 0.72f, depth);
                        c.a = 1f;
                        tex.SetPixel(x, y, c);
                    }
                    tex.SetPixel(x, top, Color.Lerp(color, Color.white, 0.28f));
                }
                tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 8f, 0, SpriteMeshType.FullRect);
            });
        }

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

        static bool InsideRoundedRect(int x, int y, int w, int h, int r)
        {
            if (w <= 0 || h <= 0) return false;
            int cx = Mathf.Clamp(x, r, w - r - 1);
            int cy = Mathf.Clamp(y, r, h - r - 1);
            float dx = x - cx;
            float dy = y - cy;
            return dx * dx + dy * dy <= r * r;
        }

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

        static Sprite GetOrCreate(string key, System.Func<Sprite> factory)
        {
            if (cache.TryGetValue(key, out var sprite)) return sprite;
            sprite = factory();
            cache[key] = sprite;
            return sprite;
        }
    }
}
