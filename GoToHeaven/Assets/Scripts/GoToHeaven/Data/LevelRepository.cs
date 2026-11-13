using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Loads every JSON level placed in Resources/GoToHeaven/Levels and keeps them
    /// ordered by asset name (level01, level02, ...). Adding a level is a drop-in.
    /// </summary>
    public sealed class LevelRepository
    {
        public const string ResourceFolder = "GoToHeaven/Levels";

        readonly List<LevelData> levels = new List<LevelData>();

        public IReadOnlyList<LevelData> Levels => levels;
        public int Count => levels.Count;

        public LevelRepository()
        {
            var assets = Resources.LoadAll<TextAsset>(ResourceFolder);
            var ordered = new List<TextAsset>(assets);
            ordered.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            foreach (var asset in ordered)
            {
                try
                {
                    var data = JsonUtility.FromJson<LevelData>(asset.text);
                    if (data == null)
                    {
                        Debug.LogWarning($"[GoToHeaven] Empty level file: {asset.name}");
                        continue;
                    }

                    if (string.IsNullOrEmpty(data.id)) data.id = asset.name;
                    levels.Add(data.Sanitize());
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[GoToHeaven] Failed to parse level '{asset.name}': {e.Message}");
                }
            }

            if (levels.Count == 0)
            {
                Debug.LogWarning("[GoToHeaven] No level JSON found. Using the generated fallback level.");
                levels.Add(LevelData.CreateFallback());
            }

            Debug.Log($"[GoToHeaven] Loaded {levels.Count} level(s): {string.Join(", ", levels.ConvertAll(l => l.id))}");
        }

        public LevelData Get(int index)
        {
            if (levels.Count == 0) return LevelData.CreateFallback();
            return levels[Mathf.Clamp(index, 0, levels.Count - 1)];
        }
    }
}
