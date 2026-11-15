using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>单个关卡的存档记录。</summary>
    [Serializable]
    public class LevelRecord
    {
        /// <summary>关卡 id，作为这条记录的主键。</summary>
        public string id;
        public bool completed;
        public float bestTime;
        public int bestCoins;
    }

    /// <summary>整份存档数据，由 JsonUtility 序列化成 JSON 后写进 PlayerPrefs。</summary>
    [Serializable]
    public class SaveData
    {
        // 存档结构版本号，留给以后做数据迁移时判断。
        public int version = 1;
        /// <summary>已解锁的最高关卡序号（序号 0 始终解锁）。</summary>
        public int unlocked;
        public int totalCoins;
        // 三档音量均取 0~1，AudioManager 直接拿它设音量。
        public float masterVolume = 0.85f;
        public float musicVolume = 0.35f;
        public float sfxVolume = 0.8f;
        public bool screenShake = true;
        public bool showHints = true;
        public List<LevelRecord> records = new List<LevelRecord>();
    }

    /// <summary>极简存档层：PlayerPrefs + JsonUtility，不引用场景对象，打包后也能用。</summary>
    public sealed class SaveSystem
    {
        // 键名带版本后缀，与 SaveData.version 一起为将来迁移留余地。
        const string PrefsKey = "GoToHeaven.Save.v1";

        /// <summary>当前内存中的存档数据。</summary>
        public SaveData Data { get; private set; }

        /// <summary>启动时读档；读不出来就用一份全新存档。</summary>
        public SaveSystem()
        {
            Data = Load();
        }

        /// <summary>从 PlayerPrefs 读取并校正存档，解析失败时重置为默认值。</summary>
        static SaveData Load()
        {
            if (!PlayerPrefs.HasKey(PrefsKey))
                return new SaveData();

            try
            {
                var json = PlayerPrefs.GetString(PrefsKey);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return new SaveData();
                // 旧存档可能缺字段，这里统一补空并把数值钳制回合法区间。
                data.records ??= new List<LevelRecord>();
                data.unlocked = Mathf.Max(0, data.unlocked);
                data.masterVolume = Mathf.Clamp01(data.masterVolume);
                data.musicVolume = Mathf.Clamp01(data.musicVolume);
                data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
                return data;
            }
            catch (Exception e)
            {
                // 存档损坏不能让游戏起不来：记录警告后丢弃旧档，重新开始。
                Debug.LogWarning($"[GoToHeaven] Save data was corrupt and has been reset: {e.Message}");
                return new SaveData();
            }
        }

        /// <summary>把当前数据序列化写回 PlayerPrefs。</summary>
        public void Save()
        {
            // 打开 prettyPrint，方便在编辑器里直接查看存档 JSON。
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Data, true));
            PlayerPrefs.Save();
        }

        /// <summary>判断关卡是否已解锁：第 0 关恒解锁，其余看已解锁进度。</summary>
        public bool IsUnlocked(int levelIndex)
        {
            return levelIndex <= 0 || levelIndex <= Data.unlocked;
        }

        /// <summary>按关卡 id 查存档记录，找不到返回 null。</summary>
        public LevelRecord GetRecord(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return null;
            // 记录数量与关卡数同级，线性 Find 足够，不必另建字典索引。
            return Data.records.Find(r => r != null && r.id == levelId);
        }

        /// <summary>记录一次通关结果：刷新最佳成绩、解锁下一关并立即落盘。</summary>
        public void RecordLevel(string levelId, int levelIndex, int levelCount, float time, int coins)
        {
            var record = GetRecord(levelId);
            if (record == null)
            {
                // 首次通关才建记录，之后只在成绩更好时覆盖。
                record = new LevelRecord { id = levelId };
                Data.records.Add(record);
            }

            record.completed = true;
            // bestTime 初始为 0，表示还没成绩；首次通关直接写入本次用时。
            if (record.bestTime <= 0f || time < record.bestTime) record.bestTime = time;
            if (coins > record.bestCoins) record.bestCoins = coins;

            // 总金币是跨关卡累计的展示值，顺手挡掉负数的异常输入。
            Data.totalCoins += Mathf.Max(0, coins);
            if (levelIndex + 1 < levelCount)
                Data.unlocked = Mathf.Max(Data.unlocked, Mathf.Min(levelIndex + 1, levelCount - 1));

            Save();
        }

        /// <summary>清空所有进度并立即写盘。</summary>
        public void ResetAll()
        {
            Data = new SaveData();
            Save();
        }
    }
}
