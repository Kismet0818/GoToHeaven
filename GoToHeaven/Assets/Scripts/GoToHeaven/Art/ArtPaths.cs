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
    }
}
