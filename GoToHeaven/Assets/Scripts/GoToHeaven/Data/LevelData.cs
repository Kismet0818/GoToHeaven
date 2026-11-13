using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Serializable 2D vector (JsonUtility cannot serialize UnityEngine.Vector2).</summary>
    [Serializable]
    public struct Vec2
    {
        public float x;
        public float y;

        public Vec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2 V => new Vector2(x, y);

        public static implicit operator Vector2(Vec2 v) => new Vector2(v.x, v.y);
        public static implicit operator Vec2(Vector2 v) => new Vec2(v.x, v.y);

        public override string ToString() => $"({x:0.##}, {y:0.##})";
    }

    [Serializable]
    public class PlatformDef
    {
        public float x;
        public float y;
        public float width = 4f;
        public float height = 1f;
        /// <summary>grass | stone | wood</summary>
        public string type = "grass";
        public bool oneWay;
    }

    [Serializable]
    public class CoinDef
    {
        public float x;
        public float y;
    }

    [Serializable]
    public class SpikeDef
    {
        public float x;
        public float y;
        public float width = 1f;
    }

    [Serializable]
    public class SawDef
    {
        public float x;
        public float y;
        public float moveX;
        public float moveY;
        public float speed = 1f;
        public float phase;
    }

    [Serializable]
    public class MoverDef
    {
        public float x;
        public float y;
        public float width = 3f;
        public float height = 1f;
        public float moveX;
        public float moveY;
        public float speed = 1f;
        public float phase;
        public string type = "stone";
    }

    [Serializable]
    public class TrampolineDef
    {
        public float x;
        public float y;
        public float power = 27f;
    }

    [Serializable]
    public class CheckpointDef
    {
        public float x;
        public float y;
    }

    [Serializable]
    public class GoalDef
    {
        public float x;
        public float y;
    }

    /// <summary>
    /// A level is plain data loaded from Assets/Resources/GoToHeaven/Levels/*.json.
    /// Build the level in the editor or by hand, no scene editing required.
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public string id = "level";
        public string displayName = "Untitled";
        public string subtitle = "";
        /// <summary>day | dusk | night</summary>
        public string theme = "day";
        public float parTime = 60f;
        public float killY = -16f;
        public Vec2 spawn = new Vec2(0f, 0.2f);
        public GoalDef goal = new GoalDef();
        public PlatformDef[] platforms = Array.Empty<PlatformDef>();
        public CoinDef[] coins = Array.Empty<CoinDef>();
        public SpikeDef[] spikes = Array.Empty<SpikeDef>();
        public SawDef[] saws = Array.Empty<SawDef>();
        public MoverDef[] movers = Array.Empty<MoverDef>();
        public TrampolineDef[] trampolines = Array.Empty<TrampolineDef>();
        public CheckpointDef[] checkpoints = Array.Empty<CheckpointDef>();

        /// <summary>Set by LevelRepository for prefab-driven levels; not serialized in JSON.</summary>
        [System.NonSerialized] public GameObject Prefab;
        /// <summary>Authoring metadata count used before a level prefab is instantiated.</summary>
        public int coinCount;

        public int CoinCount => coinCount > 0 ? coinCount : (coins != null ? coins.Length : 0);

        /// <summary>Fills in defaults and removes degenerate platform sizes after parsing.</summary>
        public LevelData Sanitize()
        {
            platforms ??= Array.Empty<PlatformDef>();
            coins ??= Array.Empty<CoinDef>();
            spikes ??= Array.Empty<SpikeDef>();
            saws ??= Array.Empty<SawDef>();
            movers ??= Array.Empty<MoverDef>();
            trampolines ??= Array.Empty<TrampolineDef>();
            checkpoints ??= Array.Empty<CheckpointDef>();

            foreach (var p in platforms)
            {
                if (p == null) continue;
                p.width = Mathf.Max(0.5f, p.width);
                p.height = Mathf.Max(0.5f, p.height);
                if (string.IsNullOrEmpty(p.type)) p.type = "grass";
            }

            if (platforms.Length == 0)
            {
                platforms = new[] { new PlatformDef { x = -12f, y = -3f, width = 24f, height = 3f } };
                spawn = new Vec2(-6f, 0.2f);
                goal = new GoalDef { x = 8f, y = 0f };
            }

            id = string.IsNullOrEmpty(id) ? "level" : id;
            displayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            parTime = Mathf.Max(5f, parTime);
            return this;
        }

        public static LevelData CreateFallback()
        {
            return new LevelData
            {
                id = "fallback",
                displayName = "Test Chamber",
                subtitle = "A generated safety level",
                theme = "day",
                spawn = new Vec2(-7f, 0.2f),
                goal = new GoalDef { x = 9f, y = 0f },
                platforms = new[]
                {
                    new PlatformDef { x = -12f, y = -3f, width = 16f, height = 3f },
                    new PlatformDef { x = 6f, y = -3f, width = 10f, height = 3f },
                    new PlatformDef { x = -2f, y = 1f, width = 4f, height = 1f, oneWay = true },
                    new PlatformDef { x = 0f, y = 4.5f, width = 4f, height = 1f, oneWay = true }
                },
                coins = new[]
                {
                    new CoinDef { x = 0f, y = 3f },
                    new CoinDef { x = 2f, y = 4f },
                    new CoinDef { x = 2f, y = 6.2f }
                },
                spikes = Array.Empty<SpikeDef>(),
                checkpoints = new[] { new CheckpointDef { x = 7f, y = 0f } }
            }.Sanitize();
        }
    }
}
