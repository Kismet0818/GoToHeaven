using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Loads sprites from Resources and slices the raw Aseprite strips at runtime.
    /// The art is packed as horizontal sheets (32x32 character frames, 64x64 props),
    /// so the project never depends on importer-generated sprite rectangles.
    /// </summary>
    public sealed class SpriteLibrary
    {
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Sprite[]> frameSets = new Dictionary<string, Sprite[]>();
        readonly Dictionary<int, Sprite> terrainTiles = new Dictionary<int, Sprite>();
        readonly HashSet<string> missing = new HashSet<string>();

        Texture2D terrainTexture;

        public static readonly Vector2 PlayerPivot = new Vector2(0.5f, 0.04f);
        public static readonly Vector2 PropPivot = new Vector2(0.5f, 0f);
        public static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);

        public Sprite Get(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (sprites.TryGetValue(path, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                if (missing.Add(path))
                    Debug.LogWarning($"[GoToHeaven] Missing sprite '{path}'. Check Resources/GoToHeaven/Art.");
            }
            else
            {
                if (sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
            }

            sprites[path] = sprite;
            return sprite;
        }

        /// <summary>Slices a horizontal sheet into evenly sized frames.</summary>
        public Sprite[] Frames(string path, int cellWidth, int cellHeight, float pixelsPerUnit, Vector2 pivot)
        {
            string key = $"{path}|{cellWidth}x{cellHeight}@{pixelsPerUnit}";
            if (frameSets.TryGetValue(key, out var cached)) return cached;

            var sheet = Get(path);
            if (sheet == null || sheet.texture == null)
            {
                frameSets[key] = System.Array.Empty<Sprite>();
                return frameSets[key];
            }

            var texture = sheet.texture;
            texture.filterMode = FilterMode.Point;
            int count = Mathf.Max(1, texture.width / cellWidth);
            var frames = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect(i * cellWidth, texture.height - cellHeight, cellWidth, cellHeight);
                frames[i] = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
                frames[i].name = $"{path}_{i}";
            }

            frameSets[key] = frames;
            return frames;
        }

        /// <summary>The terrain atlas is a 22 x 11 grid of 16x16 tiles.</summary>
        public Sprite TerrainTile(int index)
        {
            if (terrainTiles.TryGetValue(index, out var cached)) return cached;

            if (terrainTexture == null)
            {
                var terrain = Get(ArtPaths.Terrain);
                if (terrain == null) return null;
                terrainTexture = terrain.texture;
                terrainTexture.filterMode = FilterMode.Point;
            }

            const int cell = 16;
            int columns = terrainTexture.width / cell;
            int rows = terrainTexture.height / cell;
            int total = columns * rows;
            if (index < 0 || index >= total)
            {
                Debug.LogWarning($"[GoToHeaven] Terrain tile index {index} is out of range (0..{total - 1}).");
                return null;
            }

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
