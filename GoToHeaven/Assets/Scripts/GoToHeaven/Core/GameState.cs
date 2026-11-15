namespace GoToHeaven
{
    /// <summary>游戏的高层流程。所有界面都在运行时按这个状态构建，不依赖场景里的静态 UI。</summary>
    public enum GameState
    {
        // 启动：初始占位状态，Awake 末尾就会切到 MainMenu，运行时几乎观察不到。
        Boot,
        // 主菜单：相机与背景切回菜单主题，可开始游戏或进关卡选择。
        MainMenu,
        // 关卡选择：目前主流程没用到，选关是菜单上的覆盖层（UIManager.ShowLevelSelect）。
        LevelSelect,
        // 设置：同样没有状态入口，设置面板是菜单覆盖层（UIManager.ShowSettings）。
        Settings,
        // 游玩中：只有这个状态计时、计分并接收键盘输入。
        Playing,
        // 暂停：timeScale 归零、音乐压低，局面保持原样等待恢复。
        Paused,
        // 失败：生命耗尽，等待重开本关或回主菜单。
        GameOver,
        // 胜利：成绩已写档并算出星级，可进入下一关或回主菜单。
        Victory
    }

    // 音效键：AudioManager 在构造时按这些键逐个合成 AudioClip，
    // 新增键后要记得在那里补上合成，否则该音效会被静默跳过。
    /// <summary>音效键，覆盖角色动作、拾取、机关反馈与流程结算。</summary>
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
