#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GoToHeaven.EditorTools
{
    /// <summary>
    /// 批次模式冒烟测试：打开启动场景、进入播放模式、启动第 1 关，
    /// 再逐项验证运行时框架是否真的搭起来了（玩家、金币、UI、字体、相机、图集）。
    ///
    /// 用法：
    ///   Tuanjie.exe -batchmode -projectPath &lt;project&gt; -executeMethod GoToHeaven.EditorTools.PlayModeSmokeTest.Batch -logFile smoke.log
    /// </summary>
    public static class PlayModeSmokeTest
    {
        const string BootScene = "Assets/Scenes/Boot.scene";

        // 以下标志位分阶段推进流程：每次 update 只做一步，避免长时间阻塞编辑器。
        static bool running;
        static bool startedLevel;
        static bool checkedLevel;
        static bool completed;
        // 计时用编辑器启动起的秒数，比帧数稳定，不受机器性能波动影响。
        static double startedAt;
        static double levelStartedAt;
        // 帧数只用做兜底：播放模式已切过去但状态迟迟不变时，用它判定卡死。
        static int frames;
        // 进入播放模式前临时改过编辑器设置，退出时必须原样还原，否则会污染后续会话。
        static bool previousOptionsEnabled;
        static EnterPlayModeOptions previousOptions;

        /// <summary>批次入口：由 -executeMethod 调用，跑完整轮检查后按结果退出编辑器。</summary>
        public static void Batch()
        {
            // 已经在播放模式时直接返回，重复调用会让流程相互干扰。
            if (Application.isPlaying) return;
            // 场景缺失也继续往下走，让后续步骤报出更具体的原因，而不是在这里静默失败。
            if (System.IO.File.Exists(BootScene))
                EditorSceneManager.OpenScene(BootScene);

            // 关掉域重载：静态标志不会被重置，update 回调也不会在进入播放模式时丢失。
            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            // 复位全部阶段标志后挂上 update，进入播放模式，由 Tick 接管后续推进。
            running = true;
            startedLevel = false;
            checkedLevel = false;
            completed = false;
            frames = 0;
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        // 由 EditorApplication.update 驱动的阶段机：等播放模式 → 等引导 → 开局 → 校验框架。
        static void Tick()
        {
            if (!running) return;
            frames++;

            // 总超时 40 秒：批处理里没人能手动停，卡住必须自己退出，否则 CI 会一直挂到外部超时。
            if (EditorApplication.timeSinceStartup - startedAt > 40.0)
            {
                Fail("Timed out waiting for play mode.");
                return;
            }

            // 播放模式尚未真正切换完成，先让回调空转，下一帧再查。
            if (!EditorApplication.isPlaying) return;

            var game = GameManager.Instance;
            if (game == null)
            {
                // 给引导留出启动时间；600 帧仍没建出实例，说明 RuntimeInitializeOnLoadMethod 没跑。
                if (frames > 600) Fail("GameManager was never created (bootstrap failed).");
                return;
            }

            // 阶段一：主菜单出现后立刻用代码开 0 号关卡，顺带验证解锁判断与转场协程。
            if (!startedLevel)
            {
                if (game.State == GameState.MainMenu)
                {
                    startedLevel = true;
                    game.StartLevel(0);
                }
                return;
            }

            // 阶段二：关卡跑起来后逐项检查框架产物，断言文案要能直接定位缺哪一块。
            if (!checkedLevel)
            {
                if (game.State == GameState.Playing && game.Level != null)
                {
                    checkedLevel = true;
                    levelStartedAt = EditorApplication.timeSinceStartup;
                    if (game.Level.Player == null) { Fail("Player was not spawned."); return; }
                    if (game.Level.TotalCoins <= 0) { Fail("Level 1 has no coins."); return; }
                    if (game.UI == null || game.UI.FontAsset == null) { Fail("UI or TMP font is missing."); return; }
                    if (game.MainCamera == null) { Fail("Main camera is missing."); return; }
                    if (game.Art.Get(ArtPaths.PlayerIdle) == null) { Fail("Player sprite failed to load from Resources."); return; }
                    if (game.Art.TerrainTile(7) == null) { Fail("Terrain atlas failed to slice."); return; }
                }
                else if (frames > 900)
                {
                    // 900 帧还没进 Playing，多半是关卡预制体缺失或加载协程被打断。
                    Fail($"Level did not start (state={game.State}).");
                }
                return;
            }

            if (!completed)
            {
                // 先放几帧物理步进，确认玩家确实落在生成的地形上，再走通关流程；
                // 否则“通关”会掩盖掉落地判定失效的问题。
                if (EditorApplication.timeSinceStartup - levelStartedAt < 1.2) return;
                // 下面几条对应最常见的集成故障：层配错、预制体缺碰撞、血量初始化错、计时没启动。
                if (!game.Level.Player.Grounded) { Fail("Player never became grounded (layer/collider issue)."); return; }
                if (game.Level.Player.transform.position.y < game.Level.Data.spawn.y - 1.5f)
                {
                    Fail("Player fell through the generated platform.");
                    return;
                }
                if (game.Level.Health.Current != PlayerHealth.MaxHearts)
                {
                    Fail($"Player took damage while standing still (health={game.Level.Health.Current}).");
                    return;
                }
                if (game.RunTime <= 0f) { Fail("Run timer did not advance."); return; }
                // 直接调通关接口，等价于玩家碰到终点，不必等角色真的跑完全程。
                completed = true;
                game.Level.CompleteLevel();
                return;
            }

            // 阶段三：等胜利状态；成功时打印带前缀的汇总行供 CI 匹配，再以 0 退出。
            if (game.State == GameState.Victory)
            {
                Debug.Log($"GOTOTHEAVEN_SMOKE_PASS frames={frames} level={game.CurrentLevelIndex} " +
                          $"coins={game.RunCoins} time={game.RunTime:0.00} grounded={game.Level.Player.Grounded} " +
                          $"health={game.Level.Health.Current}");
                Finish(0);
            }
            else if (frames > 1200 && game.State == GameState.Playing)
            {
                // 通关调用之后仍是 Playing，说明完成回调到胜利状态的链路没打通。
                Fail($"Completion did not fire (state={game.State}).");
            }
        }

        // 统一失败出口：错误日志带固定前缀方便检索，再以非 0 码退出让 CI 判定失败。
        static void Fail(string message)
        {
            Debug.LogError($"GOTOTHEAVEN_SMOKE_FAIL: {message}");
            Finish(1);
        }

        // 收尾：摘掉 update 回调、还原进播放模式前改过的编辑器设置，再退出编辑器。
        static void Finish(int exitCode)
        {
            running = false;
            EditorApplication.update -= Tick;
            EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            EditorApplication.Exit(exitCode);
        }
    }
}
#endif
