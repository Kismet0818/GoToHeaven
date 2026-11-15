using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 从 Resources/GoToHeaven/Levels 为每个关卡加载一个 prefab，
    /// 并把预制体上策划的元数据包装成 LevelData 供菜单/HUD 使用。
    /// </summary>
    public sealed class LevelRepository
    {
        /// <summary>关卡 prefab 所在的 Resources 子目录。</summary>
        public const string ResourceFolder = "GoToHeaven/Levels";

        // 按 prefab 名称排序后的关卡列表，下标即关卡序号。
        readonly List<LevelData> levels = new List<LevelData>();

        /// <summary>只读关卡列表。</summary>
        public IReadOnlyList<LevelData> Levels => levels;
        /// <summary>关卡总数。</summary>
        public int Count => levels.Count;

        /// <summary>扫描资源目录，建立关卡索引。</summary>
        public LevelRepository()
        {
            var prefabs = Resources.LoadAll<GameObject>(ResourceFolder);
            var ordered = new List<GameObject>(prefabs);
            // 按名称做序数排序，让关卡顺序稳定，不依赖 Resources 的返回顺序。
            ordered.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            foreach (var prefab in ordered)
            {
                var def = prefab.GetComponent<LevelDefinition>();
                if (def == null)
                {
                    // 缺元数据就跳过，避免半成品 prefab 混进关卡列表。
                    Debug.LogWarning($"[GoToHeaven] Level prefab '{prefab.name}' has no LevelDefinition.");
                    continue;
                }

                var coins = prefab.GetComponentsInChildren<Coin>(true);
                // 以预制体内的实际金币数为准，没有 Coin 时才退回元数据里的估值。
                int actualCoins = coins.Length > 0 ? coins.Length : def.coinCount;
                levels.Add(new LevelData
                {
                    // id 留空时退回 prefab 名称，保证存档记录始终有稳定的键。
                    id = string.IsNullOrEmpty(def.id) ? prefab.name : def.id,
                    displayName = def.displayName,
                    subtitle = def.subtitle,
                    theme = def.theme,
                    // 目标时间至少 5 秒，防止误填 0 或负数把星级评定带偏。
                    parTime = Mathf.Max(5f, def.parTime),
                    killY = def.killY,
                    coinCount = actualCoins,
                    Prefab = prefab
                });
            }

            // 空关卡列表会让主菜单直接不可玩，用错误日志提醒检查资源目录。
            if (levels.Count == 0)
                Debug.LogError("[GoToHeaven] No level prefabs found in Resources/GoToHeaven/Levels.");

            Debug.Log($"[GoToHeaven] Loaded {levels.Count} level prefab(s): {string.Join(", ", levels.ConvertAll(l => l.id))}");
        }

        /// <summary>按序号取关卡；越界时钳制到有效范围，没有关卡则返回 null。</summary>
        public LevelData Get(int index)
        {
            return levels.Count == 0 ? null : levels[Mathf.Clamp(index, 0, levels.Count - 1)];
        }
    }
}
