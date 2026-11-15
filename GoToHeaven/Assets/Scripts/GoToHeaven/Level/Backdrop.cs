using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 一套关卡主题的配色：天空渐变、远景与近景山体、云、星星，
    /// 以及是否绘制星空层的开关。
    /// </summary>
    public struct ThemePalette
    {
        public Color skyTop;
        public Color skyBottom;
        public Color farHill;
        public Color nearHill;
        public Color cloud;
        public Color star;
        // 是否绘制星空层：白天主题关掉，夜晚与菜单主题打开。
        public bool stars;

        /// <summary>按主题名取配色，未知或空主题回退到白天方案。</summary>
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

        /// <summary>解析 #RRGGBB 颜色；解析失败返回洋红，便于在场景里一眼看出配色写错。</summary>
        static Color Hex(string html)
        {
            return ColorUtility.TryParseHtmlString(html, out var color) ? color : Color.magenta;
        }
    }

    /// <summary>
    /// 纯代码生成的分层视差背景，让每个关卡看起来都是完成品，
    /// 而不必为背景额外投入美术资源。
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
        // 与 clouds 一一对应的水平漂移速度（世界单位 / 秒）。
        readonly List<float> cloudSpeeds = new List<float>();
        // 累加真实时间，作为云上下浮动的相位，避免所有云同步摆动。
        float drift;

        /// <summary>按主题生成一整套背景对象，并挂到 parent 下（随关卡一起销毁）。</summary>
        public static Backdrop Create(Camera camera, string theme, Transform parent)
        {
            var go = new GameObject($"Backdrop ({theme})");
            if (parent != null) go.transform.SetParent(parent, false);
            var backdrop = go.AddComponent<Backdrop>();
            backdrop.Build(camera, ThemePalette.Get(theme));
            return backdrop;
        }

        /// <summary>逐层生成天空、星星、远近山体与云，并缓存需要每帧驱动的引用。</summary>
        void Build(Camera camera, ThemePalette palette)
        {
            cam = camera;
            // 128 既是渐变贴图的像素高度，也参与缓存 key，相同配色可复用同一张纹理。
            var skySprite = ProceduralArt.Gradient($"sky_{palette.skyTop}_{palette.skyBottom}", palette.skyTop, palette.skyBottom, 128);
            // 排序层用负值保证背景永远画在 Tilemap 与角色之后；-400 是最靠底的一层。
            sky = MakeSprite("Sky", skySprite, new Color(1f, 1f, 1f, 1f), -400).transform;
            // 贴图原始尺寸是 2x128 像素（PPU 为 1），缩放后约 48x24 个世界单位，足够盖满整个视口。
            sky.localScale = new Vector3(24f, 24f / 128f, 1f);

            // 白天主题不画星空，省下三十多个绘制对象。
            if (palette.stars)
            {
                starRoot = new GameObject("Stars").transform;
                starRoot.SetParent(transform, false);
                var starSprite = ProceduralArt.Circle(2, palette.star);
                // 固定种子 7：同一关卡每次进入星空布局都一致，不会闪烁跳变。
                var rng = new System.Random(7);
                // 34 颗星足以铺满夜空又不显密；透明度与大小各自随机，避免排列得过于整齐。
                for (int i = 0; i < 34; i++)
                {
                    var star = MakeSprite($"Star{i}", starSprite, new Color(1f, 1f, 1f, 0.25f + (float)rng.NextDouble() * 0.6f), -390, starRoot);
                    // 横向 -25..25、纵向 1..17，覆盖相机可能看到的天空区域。
                    star.transform.localPosition = new Vector3((float)rng.NextDouble() * 50f - 25f, (float)rng.NextDouble() * 16f + 1f, 0f);
                    float scale = 0.05f + (float)rng.NextDouble() * 0.12f;
                    star.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }

            farRoot = new GameObject("FarHills").transform;
            farRoot.SetParent(transform, false);
            // 11 是这层山脊的随机种子，决定起伏轮廓。
            var far = ProceduralArt.Hills("far", palette.farHill, 11);
            // 只铺 3 张（-64 / 0 / +64），配合视差偏移足以覆盖超宽屏，不浪费绘制。
            for (int i = -1; i <= 1; i++) MakeSprite($"Far{i}", far, Color.white, -350, farRoot).transform.localPosition = new Vector3(i * 64f, 0f, 0f);

            nearRoot = new GameObject("NearHills").transform;
            nearRoot.SetParent(transform, false);
            // 近山换一个种子（37），避免与远山出现一样的山形节奏。
            var near = ProceduralArt.Hills("near", palette.nearHill, 37);
            for (int i = -1; i <= 1; i++) MakeSprite($"Near{i}", near, Color.white, -300, nearRoot).transform.localPosition = new Vector3(i * 64f, 0f, 0f);

            cloudRoot = new GameObject("Clouds").transform;
            cloudRoot.SetParent(transform, false);
            var cloudSprite = ProceduralArt.Cloud();
            // 云的随机序列与山体分开，免得两层图案出现相同的随机节拍。
            var cloudRng = new System.Random(21);
            // 7 朵云：横向 -28..28、高度 3..15，缩放与速度随机，看不出重复感。
            for (int i = 0; i < 7; i++)
            {
                var cloud = MakeSprite($"Cloud{i}", cloudSprite, new Color(1f, 1f, 1f, 0.75f), -250, cloudRoot);
                cloud.transform.localPosition = new Vector3((float)cloudRng.NextDouble() * 56f - 28f, 3f + (float)cloudRng.NextDouble() * 12f, 0f);
                float scale = 0.7f + (float)cloudRng.NextDouble() * 1.3f;
                cloud.transform.localScale = new Vector3(scale, scale, 1f);
                clouds.Add(cloud.transform);
                // 0.08..0.26 单位每秒：慢到不抢戏，又能让人感觉到空气在流动。
                cloudSpeeds.Add(0.08f + (float)cloudRng.NextDouble() * 0.18f);
            }
        }

        /// <summary>生成一个带 SpriteRenderer 的子物体，统一设置贴图、颜色与排序层。</summary>
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

            // 天空层直接钉在相机坐标上（视差系数 1），画面里不会出现背景边界。
            if (sky != null)
                sky.position = new Vector3(position.x, position.y, 0f);
            // 星星以 0.92 的系数跟随，制造“很远但仍在移动”的深度暗示。
            if (starRoot != null)
                starRoot.position = new Vector3(position.x * 0.92f, position.y * 0.92f, 0f);

            // 视差系数越小代表越远：远山 0.75、近山 0.58。
            float farX = position.x * 0.75f;
            float nearX = position.x * 0.58f;
            // 纵向再额外下移，让山脊始终压在画面下缘，不会因相机升高而露出空洞。
            if (farRoot != null)
                farRoot.position = new Vector3(farX, position.y * 0.92f - 3f, 0f);
            if (nearRoot != null)
                nearRoot.position = new Vector3(nearX, position.y * 0.95f - 4f, 0f);

            // 用累加时间作为相位，再按云的索引错开，各朵云不会同步起伏。
            drift += Time.deltaTime;
            for (int i = 0; i < clouds.Count; i++)
            {
                if (clouds[i] == null) continue;
                var p = clouds[i].localPosition;
                p.x += cloudSpeeds[i] * Time.deltaTime;
                // 超出 ±34 就从另一侧绕回（区间总宽 68），形成无限横向漂移。
                if (p.x > 34f) p.x -= 68f;
                if (p.x < -34f) p.x += 68f;
                // 0.003 的极小正弦扰动只做呼吸式起伏，幅度小到不干扰视线。
                p.y += Mathf.Sin(drift * 0.6f + i) * 0.003f;
                clouds[i].localPosition = p;
            }
            // 云层视差最强（0.3），并随正交尺寸微微上移，保证大视野时云不会被画面裁掉。
            if (cloudRoot != null)
                cloudRoot.position = new Vector3(position.x * 0.3f, position.y * 0.9f + height * 0.1f, 0f);
        }
    }
}
