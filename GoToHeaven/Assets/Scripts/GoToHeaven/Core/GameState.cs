namespace GoToHeaven
{
    /// <summary>High level flow of the game. Every screen is built at runtime from this state.</summary>
    public enum GameState
    {
        Boot,
        MainMenu,
        LevelSelect,
        Settings,
        Playing,
        Paused,
        GameOver,
        Victory
    }

    public enum Sfx
    {
        Click,
        Jump,
        DoubleJump,
        Coin,
        Hurt,
        Checkpoint,
        Goal,
        Bounce,
        Dash,
        GameOver,
        Victory
    }
}
