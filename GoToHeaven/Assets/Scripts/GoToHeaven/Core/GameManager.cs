using System.Collections;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// Persistent application controller: owns services, drives the state machine
    /// and coordinates the level, UI, audio and camera.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public SpriteLibrary Art { get; private set; }
        public LevelRepository Levels { get; private set; }
        public SaveSystem Save { get; private set; }
        public AudioManager Audio { get; private set; }
        public UIManager UI { get; private set; }
        public Camera MainCamera { get; private set; }
        public LevelController Level { get; private set; }

        public GameState State { get; private set; } = GameState.Boot;
        public int CurrentLevelIndex { get; private set; }
        public int RunCoins { get; private set; }
        public float RunTime { get; private set; }
        public int Deaths { get; private set; }

        public bool HasNextLevel => CurrentLevelIndex + 1 < Levels.Count;
        public LevelData CurrentLevelData => Levels.Get(CurrentLevelIndex);

        CameraFollow cameraFollow;
        Transform sceneRoot;
        bool transitioning;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;

            Art = new SpriteLibrary();
            Levels = new LevelRepository();
            Save = new SaveSystem();

            SetupCamera();
            Audio = new AudioManager(transform, Save.Data);

            UI = FindObjectOfType<UIManager>();
            if (UI != null)
            {
                UI.transform.SetParent(transform, false);
            }
            else
            {
                var uiPrefab = Resources.Load<GameObject>("GoToHeaven/UI/GameCanvas");
                if (uiPrefab != null)
                {
                    var uiGo = Instantiate(uiPrefab, transform);
                    uiGo.name = "UI";
                    UI = uiGo.GetComponent<UIManager>();
                }
            }
            if (UI == null)
            {
                Debug.LogError("[GoToHeaven] GameCanvas prefab is missing. UI cannot be initialized.");
            }
            else
            {
                UI.Initialize(this);
            }

            sceneRoot = new GameObject("Scene").transform;
            sceneRoot.SetParent(transform, false);

            EnterMenu(initial: true);
        }

        void Start()
        {
            if (UI != null) StartCoroutine(UI.FadeIn(0.8f));
        }

        void Update()
        {
            UI.TickHud(Time.unscaledDeltaTime);

            if (State == GameState.Playing)
            {
                RunTime += Time.deltaTime;
                UI.UpdateHud(Level != null && Level.Health != null ? Level.Health.Current : PlayerHealth.MaxHearts,
                    PlayerHealth.MaxHearts, RunCoins, Level != null ? Level.TotalCoins : 0, RunTime);
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == GameState.Playing) PauseGame();
                else if (State == GameState.Paused) ResumeGame();
            }

            if (Input.GetKeyDown(KeyCode.R) &&
                (State == GameState.Playing || State == GameState.Paused || State == GameState.GameOver || State == GameState.Victory))
            {
                RestartLevel();
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (State == GameState.MainMenu) PlayFromMenu();
                else if (State == GameState.GameOver) RestartLevel();
                else if (State == GameState.Victory && HasNextLevel) NextLevel();
            }
        }

        // ------------------------------------------------------------------ state

        void SetState(GameState state)
        {
            State = state;
        }

        void EnterMenu(bool initial)
        {
            Time.timeScale = 1f;
            ClearScene();
            SetState(GameState.MainMenu);
            if (cameraFollow != null) cameraFollow.ClearTarget();
            if (MainCamera != null)
            {
                MainCamera.transform.position = new Vector3(0f, 3f, -10f);
                MainCamera.orthographicSize = 8f;
                MainCamera.backgroundColor = ThemePalette.Get("menu").skyBottom;
            }

            if (sceneRoot != null)
                Backdrop.Create(MainCamera, "menu", sceneRoot);

            UI.HideHud();
            if (initial) UI.SetFade(1f);
            UI.ShowMainMenu();
            Audio.PlayMusic();
        }

        public void PlayFromMenu()
        {
            if (transitioning) return;
            int index = Mathf.Clamp(Save.Data.unlocked, 0, Mathf.Max(0, Levels.Count - 1));
            StartLevel(index);
        }

        public void StartLevel(int index)
        {
            if (transitioning || index < 0 || index >= Levels.Count) return;
            if (!Save.IsUnlocked(index)) return;
            StartCoroutine(LoadLevelRoutine(index));
        }

        IEnumerator LoadLevelRoutine(int index)
        {
            transitioning = true;
            yield return UI.FadeOut(0.18f);

            Time.timeScale = 1f;
            ClearScene();
            Audio.SetMusicDucked(false);

            CurrentLevelIndex = index;
            RunCoins = 0;
            RunTime = 0f;
            Deaths = 0;

            var data = Levels.Get(index);
            if (data == null || data.Prefab == null)
            {
                Debug.LogError($"[GoToHeaven] Level {index + 1} has no prefab. Cannot load.");
                transitioning = false;
                yield break;
            }

            var levelGo = Instantiate(data.Prefab, sceneRoot);
            levelGo.name = data.displayName;
            Level = levelGo.GetComponent<LevelController>();
            if (Level == null)
            {
                Debug.LogError("[GoToHeaven] Level prefab is missing LevelController.");
                Destroy(levelGo);
                transitioning = false;
                yield break;
            }
            Level.Initialize(data, this);

            SetState(GameState.Playing);
            UI.ShowPause(false);
            UI.HideAllScreens();
            UI.ShowHud(data);
            UI.UpdateHud(PlayerHealth.MaxHearts, PlayerHealth.MaxHearts, 0, Level.TotalCoins, 0f);

            yield return null;
            yield return UI.FadeIn(0.3f);
            transitioning = false;
        }

        public void RestartLevel()
        {
            if (transitioning) return;
            if (State == GameState.MainMenu) return;
            Time.timeScale = 1f;
            UI.ShowPause(false);
            StartLevel(CurrentLevelIndex);
        }

        public void NextLevel()
        {
            if (transitioning || !HasNextLevel) return;
            StartLevel(CurrentLevelIndex + 1);
        }

        public void ReturnToMenu()
        {
            if (transitioning) return;
            StartCoroutine(ReturnToMenuRoutine());
        }

        IEnumerator ReturnToMenuRoutine()
        {
            transitioning = true;
            Time.timeScale = 1f;
            Audio.SetMusicDucked(false);
            UI.ShowPause(false);
            Level?.FreezePlayer(true);
            yield return UI.FadeOut(0.18f);
            EnterMenu(initial: false);
            yield return null;
            yield return UI.FadeIn(0.35f);
            transitioning = false;
        }

        public void PauseGame()
        {
            if (State != GameState.Playing || transitioning) return;
            SetState(GameState.Paused);
            Time.timeScale = 0f;
            Audio.SetMusicDucked(true);
            UI.ShowPause(true);
        }

        public void ResumeGame()
        {
            if (State != GameState.Paused) return;
            SetState(GameState.Playing);
            Time.timeScale = 1f;
            Audio.SetMusicDucked(false);
            UI.ShowPause(false);
        }

        public void AddRunCoin()
        {
            RunCoins++;
        }

        public void OnLevelCompleted()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Victory);
            int stars = CalculateStars();
            var data = CurrentLevelData;
            int levelIndex = CurrentLevelIndex;
            Save.RecordLevel(data.id, levelIndex, Levels.Count, RunTime, RunCoins);
            StartCoroutine(VictoryRoutine(data, stars, levelIndex));
        }

        IEnumerator VictoryRoutine(LevelData data, int stars, int levelIndex)
        {
            yield return new WaitForSecondsRealtime(0.9f);
            if (State != GameState.Victory || CurrentLevelIndex != levelIndex) yield break;
            var record = Save.GetRecord(data.id);
            float best = record != null ? record.bestTime : RunTime;
            UI.ShowVictory(data, RunTime, RunCoins, Level.TotalCoins, stars, HasNextLevel, best);
        }

        public void OnPlayerDefeated()
        {
            if (State != GameState.Playing) return;
            Deaths++;
            int levelIndex = CurrentLevelIndex;
            SetState(GameState.GameOver);
            StartCoroutine(GameOverRoutine(levelIndex));
        }

        IEnumerator GameOverRoutine(int levelIndex)
        {
            yield return new WaitForSecondsRealtime(0.45f);
            if (State != GameState.GameOver || CurrentLevelIndex != levelIndex) yield break;
            var data = CurrentLevelData;
            var record = Save.GetRecord(data.id);
            UI.ShowGameOver(data, RunTime, RunCoins, Level != null ? Level.TotalCoins : 0, Deaths,
                record != null ? record.bestCoins : 0);
        }

        int CalculateStars()
        {
            var data = CurrentLevelData;
            int stars = 1;
            if (RunTime <= data.parTime * 1.25f) stars = 2;
            if (RunTime <= data.parTime && RunCoins >= data.CoinCount) stars = 3;
            return stars;
        }

        public void ApplySettings()
        {
            Audio.RefreshSettings(Save.Data);
            Save.Save();
        }

        public void QuitGame()
        {
            Save.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ helpers

        void SetupCamera()
        {
            MainCamera = Camera.main;
            if (MainCamera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                MainCamera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            MainCamera.orthographic = true;
            MainCamera.orthographicSize = 8f;
            MainCamera.clearFlags = CameraClearFlags.SolidColor;
            MainCamera.backgroundColor = ThemePalette.Get("day").skyTop;
            MainCamera.transform.position = new Vector3(0f, 3f, -10f);
            if (MainCamera.GetComponent<AudioListener>() == null)
                MainCamera.gameObject.AddComponent<AudioListener>();

            cameraFollow = MainCamera.GetComponent<CameraFollow>();
            if (cameraFollow == null) cameraFollow = MainCamera.gameObject.AddComponent<CameraFollow>();
            cameraFollow.Configure(8f);
        }

        void ClearScene()
        {
            if (Level != null)
            {
                Destroy(Level.gameObject);
                Level = null;
            }

            if (sceneRoot == null) return;
            for (int i = sceneRoot.childCount - 1; i >= 0; i--)
                Destroy(sceneRoot.GetChild(i).gameObject);
        }

        void OnApplicationQuit()
        {
            Save?.Save();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
