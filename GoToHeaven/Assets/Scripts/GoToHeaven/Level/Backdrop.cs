using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    public struct ThemePalette
    {
        public Color skyTop;
        public Color skyBottom;
        public Color farHill;
        public Color nearHill;
        public Color cloud;
        public Color star;
        public bool stars;

        public static ThemePalette Get(string theme)
        {
            switch ((theme ?? "day").ToLowerInvariant())
            {
                case "dusk":
                    return new ThemePalette
                    {
                        skyTop = Hex("#FF9A76"),
                        skyBottom = Hex("#FFE3B3"),
                        farHill = Hex("#D98C8C"),
                        nearHill = Hex("#A76078"),
                        cloud = Hex("#FFF1DC"),
                        star = Hex("#FFF3D0"),
                        stars = false
                    };
                case "night":
                    return new ThemePalette
                    {
                        skyTop = Hex("#10142F"),
                        skyBottom = Hex("#45326B"),
                        farHill = Hex("#2A2450"),
                        nearHill = Hex("#1A1938"),
                        cloud = Hex("#8E86C7"),
                        star = Hex("#FFF7D6"),
                        stars = true
                    };
                case "menu":
                    return new ThemePalette
                    {
                        skyTop = Hex("#26194A"),
                        skyBottom = Hex("#8B5FA8"),
                        farHill = Hex("#4A3268"),
                        nearHill = Hex("#2A1E45"),
                        cloud = Hex("#E8C9F0"),
                        star = Hex("#FFF4CE"),
                        stars = true
                    };
                default:
                    return new ThemePalette
                    {
                        skyTop = Hex("#5FB8E8"),
                        skyBottom = Hex("#D6F3FF"),
                        farHill = Hex("#9BCFB0"),
                        nearHill = Hex("#6FB487"),
                        cloud = Hex("#FFFFFF"),
                        star = Hex("#FFFFFF"),
                        stars = false
                    };
            }
        }

        static Color Hex(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.magenta;
        }
    }

    /// <summary>
    /// Layered, code generated parallax scenery so every level looks finished
    /// without spending art assets on backgrounds.
    /// </summary>
    public sealed class Backdrop : MonoBehaviour
    {
        Camera cam;
        Transform sky;
        Transform farRoot;
        Transform nearRoot;
        Transform cloudRoot;
        Transform starRoot;
        readonly List<Transform> clouds = new List<Transform>();
        readonly List<float> cloudSpeeds = new List<float>();
        float drift;

        public static Backdrop Create(Camera camera, string theme, Transform parent)
        {
            var go = new GameObject($"Backdrop ({theme})");
            if (parent != null) go.transform.SetParent(parent, false);
            var backdrop = go.AddComponent<Backdrop>();
            backdrop.Build(camera, ThemePalette.Get(theme));
            return backdrop;
        }

        void Build(Camera camera, ThemePalette palette)
        {
            cam = camera;
            var skySprite = ProceduralArt.Gradient($"sky_{palette.skyTop}_{palette.skyBottom}", palette.skyTop, palette.skyBottom, 128);
            sky = MakeSprite("Sky", skySprite, new Color(1f, 1f, 1f, 1f), -400).transform;
            sky.localScale = new Vector3(24f, 24f / 128f, 1f);

            if (palette.stars)
            {
                starRoot = new GameObject("Stars").transform;
                starRoot.SetParent(transform, false);
                var starSprite = ProceduralArt.Circle(2, palette.star);
                var rng = new System.Random(7);
                for (int i = 0; i < 34; i++)
                {
                    var star = MakeSprite($"Star{i}", starSprite, new Color(1f, 1f, 1f, 0.25f + (float)rng.NextDouble() * 0.6f), -390, starRoot);
                    star.transform.localPosition = new Vector3((float)rng.NextDouble() * 50f - 25f, (float)rng.NextDouble() * 16f + 1f, 0f);
                    float scale = 0.05f + (float)rng.NextDouble() * 0.12f;
                    star.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }

            farRoot = new GameObject("FarHills").transform;
            farRoot.SetParent(transform, false);
            var far = ProceduralArt.Hills("far", palette.farHill, 11);
            for (int i = -1; i <= 1; i++) MakeSprite($"Far{i}", far, Color.white, -350, farRoot).transform.localPosition = new Vector3(i * 64f, 0f, 0f);

            nearRoot = new GameObject("NearHills").transform;
            nearRoot.SetParent(transform, false);
            var near = ProceduralArt.Hills("near", palette.nearHill, 37);
            for (int i = -1; i <= 1; i++) MakeSprite($"Near{i}", near, Color.white, -300, nearRoot).transform.localPosition = new Vector3(i * 64f, 0f, 0f);

            cloudRoot = new GameObject("Clouds").transform;
            cloudRoot.SetParent(transform, false);
            var cloudSprite = ProceduralArt.Cloud();
            var cloudRng = new System.Random(21);
            for (int i = 0; i < 7; i++)
            {
                var cloud = MakeSprite($"Cloud{i}", cloudSprite, new Color(1f, 1f, 1f, 0.75f), -250, cloudRoot);
                cloud.transform.localPosition = new Vector3((float)cloudRng.NextDouble() * 56f - 28f, 3f + (float)cloudRng.NextDouble() * 12f, 0f);
                float scale = 0.7f + (float)cloudRng.NextDouble() * 1.3f;
                cloud.transform.localScale = new Vector3(scale, scale, 1f);
                clouds.Add(cloud.transform);
                cloudSpeeds.Add(0.08f + (float)cloudRng.NextDouble() * 0.18f);
            }
        }

        SpriteRenderer MakeSprite(string name, Sprite sprite, Color color, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        void LateUpdate()
        {
            if (cam == null) return;
            var position = cam.transform.position;
            float height = cam.orthographicSize * 2f;
            float width = height * cam.aspect;

            if (sky != null)
                sky.position = new Vector3(position.x, position.y, 0f);
            if (starRoot != null)
                starRoot.position = new Vector3(position.x * 0.92f, position.y * 0.92f, 0f);

            float farX = position.x * 0.75f;
            float nearX = position.x * 0.58f;
            if (farRoot != null)
                farRoot.position = new Vector3(farX, position.y * 0.92f - 3f, 0f);
            if (nearRoot != null)
                nearRoot.position = new Vector3(nearX, position.y * 0.95f - 4f, 0f);

            drift += Time.deltaTime;
            for (int i = 0; i < clouds.Count; i++)
            {
                if (clouds[i] == null) continue;
                var p = clouds[i].localPosition;
                p.x += cloudSpeeds[i] * Time.deltaTime;
                if (p.x > 34f) p.x -= 68f;
                if (p.x < -34f) p.x += 68f;
                p.y += Mathf.Sin(drift * 0.6f + i) * 0.003f;
                clouds[i].localPosition = p;
            }
            if (cloudRoot != null)
                cloudRoot.position = new Vector3(position.x * 0.3f, position.y * 0.9f + height * 0.1f, 0f);
        }
    }
}
