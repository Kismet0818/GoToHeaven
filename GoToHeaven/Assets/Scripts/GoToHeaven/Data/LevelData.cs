using System;
using System.Collections.Generic;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>可序列化的二维向量（JsonUtility 无法序列化 Vector2，故用它顶替）。</summary>
    [Serializable]
    public struct Vec2
    {
        public float x;
        public float y;

        /// <summary>按世界坐标分量构造。</summary>
        public Vec2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>转成 Unity 的 Vector2，供物理与渲染计算使用。</summary>
        public Vector2 V => new Vector2(x, y);

        /// <summary>隐式转成 Vector2。</summary>
        public static implicit operator Vector2(Vec2 v) => new Vector2(v.x, v.y);
        /// <summary>隐式转回 Vec2，便于把场景坐标写进存档或关卡数据。</summary>
        public static implicit operator Vec2(Vector2 v) => new Vec2(v.x, v.y);

        /// <summary>日志用格式，最多保留两位小数。</summary>
        public override string ToString() => $"({x:0.##}, {y:0.##})";
    }

    /// <summary>平台定义：位置、尺寸、皮肤与单向碰撞开关。</summary>
    [Serializable]
    public class PlatformDef
    {
        public float x;
        public float y;
        public float width = 4f;
        public float height = 1f;
        /// <summary>平台皮肤：grass | stone | wood</summary>
        public string type = "grass";
        /// <summary>单向平台：可从下方穿过，往下按跳跃还能直接穿下去。</summary>
        public bool oneWay;
    }

    /// <summary>金币的摆放位置。</summary>
    [Serializable]
    public class CoinDef
    {
        public float x;
        public float y;
    }

    /// <summary>尖刺的位置与覆盖宽度。</summary>
    [Serializable]
    public class SpikeDef
    {
        public float x;
        public float y;
        public float width = 1f;
    }

    /// <summary>旋转锯齿的摆放位置与往复运动参数。</summary>
    [Serializable]
    public class SawDef
    {
        public float x;
        public float y;
        public float moveX;
        public float moveY;
        public float speed = 1f;
        /// <summary>往复运动的初始相位偏移，用来错开同一场景里多把锯齿的节奏。</summary>
        public float phase;
    }

    /// <summary>移动平台的摆放位置与往复运动参数。</summary>
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
        /// <summary>往复运动的初始相位偏移，用来错开多块移动平台的节奏。</summary>
        public float phase;
        /// <summary>平台皮肤名，含义与 PlatformDef.type 一致。</summary>
        public string type = "stone";
    }

    /// <summary>蹦床的位置与弹射力度。</summary>
    [Serializable]
    public class TrampolineDef
    {
        public float x;
        public float y;
        /// <summary>弹射初速度，值越大弹得越高。</summary>
        public float power = 27f;
    }

    /// <summary>检查点的摆放位置。</summary>
    [Serializable]
    public class CheckpointDef
    {
        public float x;
        public float y;
    }

    /// <summary>终点旗的摆放位置。</summary>
    [Serializable]
    public class GoalDef
    {
        public float x;
        public float y;
    }

    /// <summary>
    /// 关卡数据。运行时由 LevelRepository 加载 Levels 目录下的 prefab；
    /// 这里的数据结构保留用于兜底与 JSON 兼容，不再直接参与关卡构建。
    /// </summary>
    [Serializable]
    public class LevelData
    {
        public string id = "level";
        public string displayName = "Untitled";
        public string subtitle = "";
        /// <summary>主题皮肤：day | dusk | night</summary>
        public string theme = "day";
        /// <summary>目标通关时间（秒），星级评定与 HUD 的目标时间都看它。</summary>
        public float parTime = 60f;
        /// <summary>低于该纵坐标即判定掉入深渊。</summary>
        public float killY = -16f;
        /// <summary>兜底出生点；预制体里存在 PlayerSpawn 时优先用它的位置。</summary>
        public Vec2 spawn = new Vec2(0f, 0.2f);
        /// <summary>兜底终点位置；预制体里存在 Goal 时以场景物体为准。</summary>
        public GoalDef goal = new GoalDef();
        // 以下数组只服务于兜底数据与 JSON 兼容；prefab 关卡以场景内的物体为准。
        public PlatformDef[] platforms = Array.Empty<PlatformDef>();
        public CoinDef[] coins = Array.Empty<CoinDef>();
        public SpikeDef[] spikes = Array.Empty<SpikeDef>();
        public SawDef[] saws = Array.Empty<SawDef>();
        public MoverDef[] movers = Array.Empty<MoverDef>();
        public TrampolineDef[] trampolines = Array.Empty<TrampolineDef>();
        public CheckpointDef[] checkpoints = Array.Empty<CheckpointDef>();

        /// <summary>由 LevelRepository 为 prefab 关卡填入，不参与 JSON 序列化。</summary>
        [System.NonSerialized] public GameObject Prefab;
        /// <summary>关卡元数据里记录的金币数，预制体内找不到 Coin 时用它兜底。</summary>
        public int coinCount;

        /// <summary>金币总数：优先用元数据，否则退回 JSON 的 coins 数组长度。</summary>
        public int CoinCount => coinCount > 0 ? coinCount : (coins != null ? coins.Length : 0);

        /// <summary>解析后补默认值，并修掉退化到不可用的平台尺寸。</summary>
        public LevelData Sanitize()
        {
            // 反序列化或手写 JSON 可能把数组留成 null，统一补成空数组。
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
                // 宽高至少 0.5，防止零尺寸导致碰撞体失效或布局塌陷。
                p.width = Mathf.Max(0.5f, p.width);
                p.height = Mathf.Max(0.5f, p.height);
                if (string.IsNullOrEmpty(p.type)) p.type = "grass";
            }

            if (platforms.Length == 0)
            {
                // 一个平台都没有时给一块安全地面，保证关卡至少可通。
                platforms = new[] { new PlatformDef { x = -12f, y = -3f, width = 24f, height = 3f } };
                spawn = new Vec2(-6f, 0.2f);
                goal = new GoalDef { x = 8f, y = 0f };
            }

            // 菜单和存档都依赖 id/标题，不能留空。
            id = string.IsNullOrEmpty(id) ? "level" : id;
            displayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            // 目标时间同样兜到 5 秒，避免 0 秒让三星阈值失真。
            parTime = Mathf.Max(5f, parTime);
            return this;
        }

        /// <summary>生成一个最小可玩的兜底关卡，作为没有可用 prefab 时的最后保障。</summary>
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
