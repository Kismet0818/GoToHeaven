using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    [Serializable]
    public class LevelRecord
    {
        public string id;
        public bool completed;
        public float bestTime;
        public int bestCoins;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        /// <summary>Highest unlocked level index (index 0 is always unlocked).</summary>
        public int unlocked;
        public int totalCoins;
        public float masterVolume = 0.85f;
        public float musicVolume = 0.35f;
        public float sfxVolume = 0.8f;
        public bool screenShake = true;
        public bool showHints = true;
        public List<LevelRecord> records = new List<LevelRecord>();
    }

    /// <summary>Tiny PlayerPrefs + JsonUtility save layer. No scene references, build safe.</summary>
    public sealed class SaveSystem
    {
        const string PrefsKey = "GoToHeaven.Save.v1";

        public SaveData Data { get; private set; }

        public SaveSystem()
        {
            Data = Load();
        }

        static SaveData Load()
        {
            if (!PlayerPrefs.HasKey(PrefsKey))
                return new SaveData();

            try
            {
                var json = PlayerPrefs.GetString(PrefsKey);
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) return new SaveData();
                data.records ??= new List<LevelRecord>();
                data.unlocked = Mathf.Max(0, data.unlocked);
                data.masterVolume = Mathf.Clamp01(data.masterVolume);
                data.musicVolume = Mathf.Clamp01(data.musicVolume);
                data.sfxVolume = Mathf.Clamp01(data.sfxVolume);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GoToHeaven] Save data was corrupt and has been reset: {e.Message}");
                return new SaveData();
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Data, true));
            PlayerPrefs.Save();
        }

        public bool IsUnlocked(int levelIndex)
        {
            return levelIndex <= 0 || levelIndex <= Data.unlocked;
        }

        public LevelRecord GetRecord(string levelId)
        {
            if (string.IsNullOrEmpty(levelId)) return null;
            return Data.records.Find(r => r != null && r.id == levelId);
        }

        /// <summary>Stores the result of a completed run, unlocks the next level and persists.</summary>
        public void RecordLevel(string levelId, int levelIndex, int levelCount, float time, int coins)
        {
            var record = GetRecord(levelId);
            if (record == null)
            {
                record = new LevelRecord { id = levelId };
                Data.records.Add(record);
            }

            record.completed = true;
            if (record.bestTime <= 0f || time < record.bestTime) record.bestTime = time;
            if (coins > record.bestCoins) record.bestCoins = coins;

            Data.totalCoins += Mathf.Max(0, coins);
            if (levelIndex + 1 < levelCount)
                Data.unlocked = Mathf.Max(Data.unlocked, Mathf.Min(levelIndex + 1, levelCount - 1));

            Save();
        }

        public void ResetAll()
        {
            Data = new SaveData();
            Save();
        }
    }
}
