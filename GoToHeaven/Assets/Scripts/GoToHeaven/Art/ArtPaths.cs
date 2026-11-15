namespace GoToHeaven
{
    /// <summary>运行时加载的所有美术资源都位于 Assets/Resources/GoToHeaven 下。</summary>
    public static class ArtPaths
    {
        // 根路径相对 Resources 目录，不带扩展名；各子路径直接字符串拼接，结尾斜杠不可省。
        public const string Root = "GoToHeaven/Art/";

        // 角色（面具）各动作序列帧：命名与状态机一一对应，缺图时由 SpriteLibrary 打警告。
        public const string PlayerIdle = Root + "Characters/mask_idle";
        public const string PlayerRun = Root + "Characters/mask_run";
        public const string PlayerJump = Root + "Characters/mask_jump";
        public const string PlayerFall = Root + "Characters/mask_fall";
        public const string PlayerDoubleJump = Root + "Characters/mask_double_jump";
        public const string PlayerWallJump = Root + "Characters/mask_wall_jump";
        public const string PlayerHit = Root + "Characters/mask_hit";

        // 地形图集：16x16 一格的网格，由 SpriteLibrary.TerrainTile 按索引切出。
        public const string Terrain = Root + "Environment/terrain";

        // 收集品：水果的常态与已拾取（消散）两种外观。
        public const string Fruit = Root + "Items/fruit";
        public const string FruitCollected = Root + "Items/fruit_collected";

        // 场景道具：存档旗的升起/放下状态，终点门的待机/按下状态。
        public const string CheckpointOn = Root + "Props/checkpoint_flag";
        public const string CheckpointOff = Root + "Props/checkpoint_off";
        public const string GoalIdle = Root + "Props/goal_idle";
        public const string GoalPressed = Root + "Props/goal_pressed";

        // 陷阱：尖刺、弹簧的待机/弹起帧，以及旋转锯片。
        public const string Spikes = Root + "Traps/spikes";
        public const string TrampolineIdle = Root + "Traps/trampoline_idle";
        public const string TrampolineJump = Root + "Traps/trampoline_jump";
        public const string Saw = Root + "Traps/saw";

        // 特效贴图：尘土为单帧，彩纸是横向序列帧，切分参数见 Fx.Confetti。
        public const string Dust = Root + "VFX/dust";
        public const string Confetti = Root + "VFX/confetti";
    }
}
