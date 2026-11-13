using UnityEngine;

namespace GoToHeaven
{
    /// <summary>Named layers with safe numeric fallbacks if TagManager is not imported yet.</summary>
    public static class GameLayers
    {
        public static readonly int Ground = Resolve("Ground", 6);
        public static readonly int Player = Resolve("Player", 7);
        public static readonly int Hazard = Resolve("Hazard", 8);
        public static readonly int Collectible = Resolve("Collectible", 9);
        public static readonly int OneWay = Resolve("OneWay", 10);

        public static int GroundMask => (1 << Ground) | (1 << OneWay);
        public static int WallMask => 1 << Ground;

        static int Resolve(string name, int fallback)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer >= 0 ? layer : fallback;
        }

        public static void Apply(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
                Apply(child.gameObject, layer);
        }
    }
}
