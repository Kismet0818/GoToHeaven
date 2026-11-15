using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 从 Resources 加载精灵，并在运行时切分原始的 Aseprite 长条图。
    /// 美术资源按横向图集打包（角色帧 32x32、道具帧 64x64），
    /// 因此项目不依赖导入器切出的 Sprite 矩形。
    /// </summary>
    public sealed class SpriteLibrary
    {
        // 三条缓存分别对应单图、序列帧与地形瓦片；missing 记录已报过警告的路径，避免每帧刷屏。
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Sprite[]> frameSets = new Dictionary<string, Sprite[]>();
        readonly Dictionary<int, Sprite> terrainTiles = new Dictionary<int, Sprite>();
        readonly HashSet<string> missing = new HashSet<string>();

        Texture2D terrainTexture;

        /// <summary>角色轴心贴脚底：y=0.04 让角色看起来站在地面上而不是悬空或陷进去。</summary>
        public static readonly Vector2 PlayerPivot = new Vector2(0.5f, 0.04f);
        /// <summary>道具轴心贴底边，摆放时按落地点对齐。</summary>
        public static readonly Vector2 PropPivot = new Vector2(0.5f, 0f);
        /// <summary>居中轴心，用于粒子、彩纸等需要围绕中心旋转/缩放的对象。</summary>
        public static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);

        /// <summary>按路径取单张精灵，结果缓存；缺失时只警告一次并返回 null。</summary>
        public Sprite Get(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (sprites.TryGetValue(path, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                // HashSet.Add 返回 false 说明之前已警告过同一路径，不再重复输出日志。
                if (missing.Add(path))
                    Debug.LogWarning($"[GoToHeaven] Missing sprite '{path}'. Check Resources/GoToHeaven/Art.");
            }
            else
            {
                // 统一点采样，保证像素画放大后依然锐利，不受导入设置影响。
                if (sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
            }

            sprites[path] = sprite;
            return sprite;
        }

        /// <summary>把横向长条图按等宽等高的单元切成序列帧。</summary>
        public Sprite[] Frames(string path, int cellWidth, int cellHeight, float pixelsPerUnit, Vector2 pivot)
        {
            // 缓存键带上单元尺寸与 PPU，同一张图用不同规格切分时互不覆盖。
            string key = $"{path}|{cellWidth}x{cellHeight}@{pixelsPerUnit}";
            if (frameSets.TryGetValue(key, out var cached)) return cached;

            var sheet = Get(path);
            if (sheet == null || sheet.texture == null)
            {
                // 缺图时缓存空数组，让调用方不必反复尝试加载。
                frameSets[key] = System.Array.Empty<Sprite>();
                return frameSets[key];
            }

            var texture = sheet.texture;
            texture.filterMode = FilterMode.Point;
            // 帧数由贴图宽度除以单元宽度得出，至少 1 帧，避免除零或空序列。
            int count = Mathf.Max(1, texture.width / cellWidth);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                // 长条图只有一行，且 Unity 纹理原点在左下角，故取顶部一行单元的高度偏移。
                var rect = new Rect(i * cellWidth, texture.height - cellHeight, cellWidth, cellHeight);
                frames[i] = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                frames[i].name = $"{path}_{i}";
            }

            frameSets[key] = frames;
            return frames;
        }

        /// <summary>地形图集是 22 x 11 的网格，每格 16x16 像素。</summary>
        public Sprite TerrainTile(int index)
        {
            if (terrainTiles.TryGetValue(index, out var cached)) return cached;

            if (terrainTexture == null)
            {
                // 图集按需解析：首次用到瓦片才加载，其余情况不占内存。
                var terrain = Get(ArtPaths.Terrain);
                if (terrain == null) return null;
                terrainTexture = terrain.texture;
                terrainTexture.filterMode = FilterMode.Point;
            }

            // 单元边长固定 16 像素，行列数从贴图实际尺寸推出，换图集尺寸时无需改代码。
            const int cell = 16;
            int columns = terrainTexture.width / cell;
            int rows = terrainTexture.height / cell;
            int total = columns * rows;
            if (index < 0 || index >= total)
            {
                Debug.LogWarning($"[GoToHeaven] Terrain tile index {index} is out of range (0..{total - 1}).");
                return null;
            }

            // 索引按行优先排列，行号需翻转成 Unity 左下角原点坐标。
            int column = index % columns;
            int row = index / columns;
            var rect = new Rect(column * cell, terrainTexture.height - (row + 1) * cell, cell, cell);
            var sprite = Sprite.Create(terrainTexture, rect, CenterPivot, cell, 0, SpriteMeshType.FullRect);
            sprite.name = $"terrain_{index}";
            terrainTiles[index] = sprite;
            return sprite;
        }
    }
}
