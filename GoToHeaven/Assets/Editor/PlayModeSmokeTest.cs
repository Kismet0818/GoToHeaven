#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GoToHeaven.EditorTools
{
    /// <summary>
    /// Batch-mode smoke test. It opens the boot scene, enters play mode, starts
    /// level 1 and verifies that the runtime framework assembled correctly.
    ///
    /// Usage:
    ///   Tuanjie.exe -batchmode -projectPath &lt;project&gt; -executeMethod GoToHeaven.EditorTools.PlayModeSmokeTest.Batch -logFile smoke.log
    /// </summary>
    public static class PlayModeSmokeTest
    {
        const string BootScene = "Assets/Scenes/Boot.scene";

        static bool running;
        static bool startedLevel;
        static bool checkedLevel;
        static bool completed;
        static double startedAt;
        static double levelStartedAt;
        static int frames;
        static bool previousOptionsEnabled;
        static EnterPlayModeOptions previousOptions;

        public static void Batch()
        {
            if (Application.isPlaying) return;
            if (System.IO.File.Exists(BootScene))
                EditorSceneManager.OpenScene(BootScene);

            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            running = true;
            startedLevel = false;
            checkedLevel = false;
            completed = false;
            frames = 0;
            startedAt = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Tick()
        {
            if (!running) return;
            frames++;

            if (EditorApplication.timeSinceStartup - startedAt > 40.0)
            {
                Fail("Timed out waiting for play mode.");
                return;
            }

            if (!EditorApplication.isPlaying) return;

            var game = GameManager.Instance;
            if (game == null)
            {
                if (frames > 600) Fail("GameManager was never created (bootstrap failed).");
                return;
            }

            if (!startedLevel)
            {
                if (game.State == GameState.MainMenu)
                {
                    startedLevel = true;
                    game.StartLevel(0);
                }
                return;
            }

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
                    Fail($"Level did not start (state={game.State}).");
                }
                return;
            }

            if (!completed)
            {
                // Let a few physics steps run to prove the player really lands on the
                // generated terrain before we trigger the completion path.
                if (EditorApplication.timeSinceStartup - levelStartedAt < 1.2) return;
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
                completed = true;
                game.Level.CompleteLevel();
                return;
            }

            if (game.State == GameState.Victory)
            {
                Debug.Log($"GOTOTHEAVEN_SMOKE_PASS frames={frames} level={game.CurrentLevelIndex} " +
                          $"coins={game.RunCoins} time={game.RunTime:0.00} grounded={game.Level.Player.Grounded} " +
                          $"health={game.Level.Health.Current}");
                Finish(0);
            }
            else if (frames > 1200 && game.State == GameState.Playing)
            {
                Fail($"Completion did not fire (state={game.State}).");
            }
        }

        static void Fail(string message)
        {
            Debug.LogError($"GOTOTHEAVEN_SMOKE_FAIL: {message}");
            Finish(1);
        }

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
