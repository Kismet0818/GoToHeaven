using UnityEngine;

namespace GoToHeaven
{
    /// <summary>常用层的具名引用；TagManager 未导入时退回项目约定的保底编号，保证碰撞判定仍可用。</summary>
    public static class GameLayers
    {
        public static readonly int Ground = Resolve("Ground", 6);
        public static readonly int Player = Resolve("Player", 7);
        public static readonly int Hazard = Resolve("Hazard", 8);
        public static readonly int Collectible = Resolve("Collectible", 9);
        public static readonly int OneWay = Resolve("OneWay", 10);

        /// <summary>可站立层：实体地面加上可穿过的单向平台，角色落地检测共用。</summary>
        public static int GroundMask => (1 << Ground) | (1 << OneWay);
        /// <summary>墙面层：只有实体地面算墙，单向平台不能贴墙跳。</summary>
        public static int WallMask => 1 << Ground;

        // 用名字查层；查不到（TagManager 未配置或层名被改）时退回保底编号，
        // 否则会返回 -1，位掩码算出来全是错的。
        static int Resolve(string name, int fallback)
        {
            int layer = LayerMask.NameToLayer(name);
            return layer >= 0 ? layer : fallback;
        }

        /// <summary>把对象连同所有子物体一起设为指定层，预制体漏设子层时靠它兜住。</summary>
        public static void Apply(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform)
                Apply(child.gameObject, layer);
        }
    }
}
