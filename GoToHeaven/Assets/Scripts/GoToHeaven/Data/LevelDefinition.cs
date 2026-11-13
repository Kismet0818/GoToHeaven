using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Metadata stored on each level prefab root.</summary>
    public sealed class LevelDefinition : MonoBehaviour
    {
        public string id = "level";
        public string displayName = "Untitled";
        public string subtitle = "";
        public string theme = "day";
        public float parTime = 60f;
        public float killY = -16f;
        public int coinCount;
    }
}
