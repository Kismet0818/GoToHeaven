namespace GoToHeaven
{
    /// <summary>All runtime loaded art lives under Assets/Resources/GoToHeaven.</summary>
    public static class ArtPaths
    {
        public const string Root = "GoToHeaven/Art/";

        public const string PlayerIdle = Root + "Characters/mask_idle";
        public const string PlayerRun = Root + "Characters/mask_run";
        public const string PlayerJump = Root + "Characters/mask_jump";
        public const string PlayerFall = Root + "Characters/mask_fall";
        public const string PlayerDoubleJump = Root + "Characters/mask_double_jump";
        public const string PlayerWallJump = Root + "Characters/mask_wall_jump";
        public const string PlayerHit = Root + "Characters/mask_hit";

        public const string Terrain = Root + "Environment/terrain";

        public const string Fruit = Root + "Items/fruit";
        public const string FruitCollected = Root + "Items/fruit_collected";

        public const string CheckpointOn = Root + "Props/checkpoint_flag";
        public const string CheckpointOff = Root + "Props/checkpoint_off";
        public const string GoalIdle = Root + "Props/goal_idle";
        public const string GoalPressed = Root + "Props/goal_pressed";

        public const string Spikes = Root + "Traps/spikes";
        public const string TrampolineIdle = Root + "Traps/trampoline_idle";
        public const string TrampolineJump = Root + "Traps/trampoline_jump";
        public const string Saw = Root + "Traps/saw";

        public const string Dust = Root + "VFX/dust";
        public const string Confetti = Root + "VFX/confetti";

        public const string IconPlay = Root + "UI/play";
        public const string IconRestart = Root + "UI/restart";
        public const string IconBack = Root + "UI/back";
        public const string IconClose = Root + "UI/close";
        public const string IconLevels = Root + "UI/levels";
        public const string IconNext = Root + "UI/next";
        public const string IconPrevious = Root + "UI/previous";
        public const string IconSettings = Root + "UI/settings";
        public const string IconVolume = Root + "UI/volume";
        public const string IconTrophy = Root + "UI/achievements";
        public const string IconLeaderboard = Root + "UI/leaderboard";

        public const string Font = "GoToHeaven/Fonts/kaph_regular";
        public const string LevelFolder = "GoToHeaven/Levels";
    }
}
