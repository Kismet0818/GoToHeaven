using UnityEngine;

namespace GoToHeaven
{
    /// <summary>挂在每个关卡预制体根节点上的元数据，由 LevelRepository 读取。</summary>
    public sealed class LevelDefinition : MonoBehaviour
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
        /// <summary>预制体内找不到 Coin 时，用它兜底金币总数。</summary>
        public int coinCount;
    }
}
