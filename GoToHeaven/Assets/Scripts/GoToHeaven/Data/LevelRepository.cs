using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Loads one prefab per level from Resources/GoToHeaven/Levels and exposes
    /// its authored metadata as LevelData for menus/HUD.
    /// </summary>
    public sealed class LevelRepository
    {
        public const string ResourceFolder = "GoToHeaven/Levels";

        readonly List<LevelData> levels = new List<LevelData>();

        public IReadOnlyList<LevelData> Levels => levels;
        public int Count => levels.Count;

        public LevelRepository()
        {
            var prefabs = Resources.LoadAll<GameObject>(ResourceFolder);
            var ordered = new List<GameObject>(prefabs);
            ordered.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            foreach (var prefab in ordered)
            {
                var def = prefab.GetComponent<LevelDefinition>();
                if (def == null)
                {
                    Debug.LogWarning($"[GoToHeaven] Level prefab '{prefab.name}' has no LevelDefinition.");
                    continue;
                }

                var coins = prefab.GetComponentsInChildren<Coin>(true);
                int actualCoins = coins.Length > 0 ? coins.Length : def.coinCount;
                levels.Add(new LevelData
                {
                    id = string.IsNullOrEmpty(def.id) ? prefab.name : def.id,
                    displayName = def.displayName,
                    subtitle = def.subtitle,
                    theme = def.theme,
                    parTime = Mathf.Max(5f, def.parTime),
                    killY = def.killY,
                    coinCount = actualCoins,
                    Prefab = prefab
                });
            }

            if (levels.Count == 0)
                Debug.LogError("[GoToHeaven] No level prefabs found in Resources/GoToHeaven/Levels.");

            Debug.Log($"[GoToHeaven] Loaded {levels.Count} level prefab(s): {string.Join(", ", levels.ConvertAll(l => l.id))}");
        }

        public LevelData Get(int index)
        {
            return levels.Count == 0 ? null : levels[Mathf.Clamp(index, 0, levels.Count - 1)];
        }
    }
}
