using UnityEngine;

namespace GoToHeaven
{
    /// <summary>短生命周期、尽量少分配内存的精灵特效（尘土、彩纸、闪光）。</summary>
    public static class Fx
    {
        /// <summary>在指定位置扬起一圈尘土，用于落地、起跳等撞击反馈。</summary>
        public static void Dust(Vector3 position, int count = 6, float strength = 2.5f)
        {
            // 美术可能尚未初始化、贴图也可能缺失，逐级判空；缺图时由 Spawn 回退成白色方块。
            var art = GameManager.Instance != null ? GameManager.Instance.Art : null;
            var sprite = art != null ? art.Get(ArtPaths.Dust) : null;
            Burst(sprite, position, count, new Color(1f, 0.95f, 0.82f, 0.9f), 0.45f, strength, 4f, 1.1f);
        }

        /// <summary>过关庆祝用的彩纸：每片随机取帧、颜色、初速与缩放，避免整齐划一。</summary>
        public static void Confetti(Vector3 position, int count = 26)
        {
            // 彩纸是横向序列帧：单元 16x16、16 PPU、居中轴心。
            var art = GameManager.Instance != null ? GameManager.Instance.Art : null;
            var frames = art != null ? art.Frames(ArtPaths.Confetti, 16, 16, 16f, SpriteLibrary.CenterPivot) : null;
            // 五种喜庆色，与场景主色调（暖黄、朱红、青绿、蓝紫、米白）呼应。
            var colors = new[]
            {
                new Color(1f, 0.83f, 0.4f), new Color(0.95f, 0.4f, 0.45f),
                new Color(0.45f, 0.82f, 0.72f), new Color(0.62f, 0.65f, 1f), new Color(1f, 0.95f, 0.85f)
            };
            for (int i = 0; i < count; i++)
            {
                // 每片独立随机，保证没有两片完全同步地翻转或淡出。
                var sprite = frames != null && frames.Length > 0 ? frames[Random.Range(0, frames.Length)] : null;
                var color = colors[Random.Range(0, colors.Length)];
                var go = Spawn(sprite, position + (Vector3)(Random.insideUnitCircle * 0.7f), color, Random.Range(0.8f, 1.6f));
                var particle = go.AddComponent<FxParticle>();
                // 初速偏上、寿命 0.9 秒、重力 12 拉回地面，淡出 0.5 让纸片尾部更快消失。
                particle.Init(new Vector2(Random.Range(-4f, 4f), Random.Range(5f, 11f)), 0.9f, 12f, 0.5f);
            }
        }

        /// <summary>闪光粒子：贴图直接用程序化圆形生成，不依赖美术资源，适合任意强调点。</summary>
        public static void Sparkle(Vector3 position, Color color, int count = 8)
        {
            var sprite = ProceduralArt.Circle(6, color);
            Burst(sprite, position, count, color, 0.5f, 4f, 0f, 1.4f);
        }

        /// <summary>通用爆发：随机方向抛射粒子，竖直分量取绝对值，保证尘土只向上飞不往下沉。</summary>
        public static void Burst(Sprite sprite, Vector3 position, int count, Color color, float life, float speed, float gravity, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                // 位置固定，靠初速与随机缩放拉开层次；粒子挂 FxParticle 自行消亡。
                var go = Spawn(sprite, position, color, scale * Random.Range(0.7f, 1.3f));
                var particle = go.AddComponent<FxParticle>();
                // 水平方向完全随机，竖直方向抬升到正区间，避免粒子扎进地面。
                var direction = Random.insideUnitCircle.normalized * speed * Random.Range(0.4f, 1f);
                particle.Init(new Vector2(direction.x, Mathf.Abs(direction.y) * 0.8f + speed * 0.25f), life * Random.Range(0.75f, 1.2f), gravity, 0.25f);
            }
        }

        /// <summary>播放一次序列帧特效，播完自动销毁物体，不残留空节点。</summary>
        public static void OneShot(Sprite[] frames, Vector3 position, float fps, float scale, Color tint, int sortingOrder = 25)
        {
            if (frames == null || frames.Length == 0) return;
            var go = new GameObject("FxOneShot");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = tint;
            renderer.sortingOrder = sortingOrder;
            var animator = go.AddComponent<SpriteAnimator>();
            animator.PlayOnce(frames, fps);
            // 按帧数与帧率算出总时长，额外加 0.1 秒余量再销毁，防止最后一帧被提前砍掉。
            Object.Destroy(go, frames.Length / Mathf.Max(1f, fps) + 0.1f);
        }

        // 创建单个粒子物体；SortingOrder 30 高于角色与场景，保证特效始终压在最上层。
        static GameObject Spawn(Sprite sprite, Vector3 position, Color color, float scale)
        {
            var go = new GameObject("Fx");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite ?? ProceduralArt.White;
            renderer.color = color;
            renderer.sortingOrder = 30;
            return go;
        }

        // 粒子本体：只做位置积分、自转与透明度衰减，由 Update 自行销毁，不参与任何物理。
        sealed class FxParticle : MonoBehaviour
        {
            Vector2 velocity;
            float life;
            float gravity;
            float fadeSpeed;
            float age;
            SpriteRenderer spriteRenderer;
            Color startColor;

            // 由外部一次性注入运动参数；life 有下限保护，避免除零和瞬间消失。
            public void Init(Vector2 initialVelocity, float lifetime, float gravityValue, float fade)
            {
                velocity = initialVelocity;
                life = Mathf.Max(0.05f, lifetime);
                gravity = gravityValue;
                fadeSpeed = fade;
                spriteRenderer = GetComponent<SpriteRenderer>();
                startColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
            }

            void Update()
            {
                age += Time.deltaTime;
                velocity.y -= gravity * Time.deltaTime;
                transform.position += (Vector3)(velocity * Time.deltaTime);
                // 用水平速度驱动自转，模拟纸片/碎屑翻转的观感。
                transform.Rotate(0f, 0f, velocity.x * 60f * Time.deltaTime);
                if (spriteRenderer != null)
                {
                    // 透明度按剩余寿命线性衰减，再乘 fadeSpeed（越大淡出越快）。
                    var c = startColor;
                    c.a = Mathf.Clamp01(startColor.a * (1f - age / life) * fadeSpeed);
                    spriteRenderer.color = c;
                }
                if (age >= life) Destroy(gameObject);
            }
        }
    }
}
