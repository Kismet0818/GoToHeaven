using System.Collections;
using UnityEngine;

namespace GoToHeaven
{
    /// <summary>
    /// 常驻的应用总控：持有各项服务、驱动状态机，并协调关卡、UI、音频与相机。
    /// 全项目只允许存在一个实例，换关时它不被销毁。
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        /// <summary>全局唯一实例；在 Awake 中抢占，场景里多出来的副本会自毁。</summary>
        public static GameManager Instance { get; private set; }

        /// <summary>程序化拼装的像素图库，贴图缺失时给出占位图而不是报错。</summary>
        public SpriteLibrary Art { get; private set; }
        /// <summary>关卡清单，索引即关卡顺序；解锁进度另存于 Save.Data。</summary>
        public LevelRepository Levels { get; private set; }
        /// <summary>存档服务，负责 PlayerPrefs 的读写，并预留了存档版本号供迁移。</summary>
        public SaveSystem Save { get; private set; }
        /// <summary>音频服务，构造时读取存档里的音量设置。</summary>
        public AudioManager Audio { get; private set; }
        /// <summary>UI 总控；先复用场景内实例，找不到才从 Resources 加载预制体。</summary>
        public UIManager UI { get; private set; }
        /// <summary>正交主相机，由 SetupCamera 兜底创建，转场时用它切背景色。</summary>
        public Camera MainCamera { get; private set; }
        /// <summary>当前关卡控制器；菜单态或转场途中为 null，使用前必须判空。</summary>
        public LevelController Level { get; private set; }

        /// <summary>状态机当前状态；转场协程没走完时可能仍是上一个状态。</summary>
        public GameState State { get; private set; } = GameState.Boot;
        /// <summary>当前关卡在 Levels 中的索引，从 0 开始。</summary>
        public int CurrentLevelIndex { get; private set; }
        /// <summary>本局已拾取金币数，换关或重开时归零。</summary>
        public int RunCoins { get; private set; }
        /// <summary>本局计时（秒），用 deltaTime 累加，所以暂停时不再增长。</summary>
        public float RunTime { get; private set; }
        /// <summary>本局死亡次数，只用于结算展示，不影响解锁进度。</summary>
        public int Deaths { get; private set; }

        /// <summary>是否还有下一关，决定胜利界面是否显示进关入口。</summary>
        public bool HasNextLevel => CurrentLevelIndex + 1 < Levels.Count;
        /// <summary>当前关卡的配置数据；调用前应确保索引合法。</summary>
        public LevelData CurrentLevelData => Levels.Get(CurrentLevelIndex);

        // 下列引用都由代码自建，不依赖 Inspector 拖拽，换场景后不会丢引用。
        CameraFollow cameraFollow;
        Transform sceneRoot;
        // 转场锁：为 true 时丢弃新的加载请求，防止协程叠加把状态改乱。
        bool transitioning;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            // 跨场景常驻：换关时整棵服务树不销毁，省掉重复初始化，也保住 Instance。
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            // 关垂直同步并把帧率顶到 120，让高刷屏手感一致、deltaTime 也更稳。
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;

            // 服务先于相机与 UI 创建：UIManager.Initialize 会立刻用到 Save 与 Art。
            Art = new SpriteLibrary();
            Levels = new LevelRepository();
            Save = new SaveSystem();

            SetupCamera();
            Audio = new AudioManager(transform, Save.Data);

            // 优先复用场景里已有的 UIManager，方便测试场景预放一个定制界面。
            UI = FindObjectOfType<UIManager>();
            if (UI != null)
            {
                UI.transform.SetParent(transform, false);
            }
            else
            {
                // 预制体路径是硬约定，缺失时只报错不抛异常，保证游戏仍能进菜单。
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

            // 关卡与背景统一挂在 Scene 节点下，ClearScene 清空该节点即可回收一整关。
            sceneRoot = new GameObject("Scene").transform;
            sceneRoot.SetParent(transform, false);

            // 启动即进菜单；initial 为 true 时先显示黑场，避免第一帧露出空场景。
            EnterMenu(initial: true);
        }

        void Start()
        {
            // 启动时从全黑淡入，用淡入遮住首帧的程序化资源构建卡顿。
            if (UI != null) StartCoroutine(UI.FadeIn(0.8f));
        }

        void Update()
        {
            // HUD 用非缩放时间刷新，暂停时数值动画仍能走完，不会定格在半路。
            UI.TickHud(Time.unscaledDeltaTime);

            // 只有 Playing 才计时计分；暂停与结算期间不累加，否则星级会被误判。
            if (State == GameState.Playing)
            {
                RunTime += Time.deltaTime;
                UI.UpdateHud(Level != null && Level.Health != null ? Level.Health.Current : PlayerHealth.MaxHearts,
                    PlayerHealth.MaxHearts, RunCoins, Level != null ? Level.TotalCoins : 0, RunTime);
            }

            // Esc 只在游玩与暂停之间来回切，其余状态的返回逻辑交给界面按钮。
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (State == GameState.Playing) PauseGame();
                else if (State == GameState.Paused) ResumeGame();
            }

            // R 是速重开键，任何进行中的局面都能用；主菜单与关卡选择里不响应。
            if (Input.GetKeyDown(KeyCode.R) &&
                (State == GameState.Playing || State == GameState.Paused || State == GameState.GameOver || State == GameState.Victory))
            {
                RestartLevel();
            }

            // 回车统一表示“确认”：菜单开局、失败重来、胜利进下一关。
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (State == GameState.MainMenu) PlayFromMenu();
                else if (State == GameState.GameOver) RestartLevel();
                else if (State == GameState.Victory && HasNextLevel) NextLevel();
            }
        }

        // ------------------------------------------------------------------ 状态

        // 所有状态迁移都走这一个入口，日后要挂 UI、音频或埋点只改这里。
        void SetState(GameState state)
        {
            State = state;
        }

        // 进入菜单：清场、复位相机与背景、切回菜单音乐；initial 为 true 时保持黑场。
        void EnterMenu(bool initial)
        {
            // 先恢复时间缩放再拆场景，避免残留对象在菜单背景里继续跑物理与动画。
            Time.timeScale = 1f;
            ClearScene();
            SetState(GameState.MainMenu);
            // 相机若还挂着已销毁的跟随目标，位置就会被拉到非法值，先清空再摆位。
            if (cameraFollow != null) cameraFollow.ClearTarget();
            if (MainCamera != null)
            {
                MainCamera.transform.position = new Vector3(0f, 3f, -10f);
                MainCamera.orthographicSize = 8f;
                MainCamera.backgroundColor = ThemePalette.Get("menu").skyBottom;
            }

            // 菜单背景是运行时按主题键生成的，换主题只改 ThemePalette 里的一组颜色。
            if (sceneRoot != null)
                Backdrop.Create(MainCamera, "menu", sceneRoot);

            // 菜单不显示 HUD；首次进入额外铺一层黑场，让启动画面过渡不露空场景。
            UI.HideHud();
            if (initial) UI.SetFade(1f);
            UI.ShowMainMenu();
            Audio.PlayMusic();
        }

        /// <summary>主菜单的“开始游戏”：进入存档记录的解锁进度所在关卡。</summary>
        public void PlayFromMenu()
        {
            if (transitioning) return;
            // 存档里的 unlocked 可能因关卡被删而越界，夹到合法区间免得取到空数据。
            int index = Mathf.Clamp(Save.Data.unlocked, 0, Mathf.Max(0, Levels.Count - 1));
            StartLevel(index);
        }

        /// <summary>按索引加载关卡；索引非法或尚未解锁时静默忽略。</summary>
        public void StartLevel(int index)
        {
            // 解锁校验放在这一层而不是 UI 层，绕开界面调用也跳不进未解锁的关卡。
            if (transitioning || index < 0 || index >= Levels.Count) return;
            if (!Save.IsUnlocked(index)) return;
            StartCoroutine(LoadLevelRoutine(index));
        }

        // 转场协程：淡出 → 拆旧关建新关 → 淡入；全程由 transitioning 挡住重入。
        IEnumerator LoadLevelRoutine(int index)
        {
            transitioning = true;
            yield return UI.FadeOut(0.18f);

            // 上一局可能停在暂停态（timeScale 为 0），这里必须复位，否则新关是静止的。
            Time.timeScale = 1f;
            ClearScene();
            Audio.SetMusicDucked(false);

            // 每关重置本局统计；星级与结算都读这几个字段，漏重置会串数据。
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

            // 关卡是运行时拼装的：实例化后必须拿到 LevelController，否则视为预制体配错。
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
            // 先把地形、玩家、道具全部建好再切状态，UI 才能一次拿到正确的总金币数。
            Level.Initialize(data, this);

            SetState(GameState.Playing);
            // 界面顺序有讲究：先收暂停层、再清空所有全屏界面、最后开 HUD，避免叠层闪烁。
            UI.ShowPause(false);
            UI.HideAllScreens();
            UI.ShowHud(data);
            UI.UpdateHud(PlayerHealth.MaxHearts, PlayerHealth.MaxHearts, 0, Level.TotalCoins, 0f);

            // 先让出一帧，等新建对象跑完 Awake/Start，否则淡入首帧会看到未激活的画面。
            yield return null;
            yield return UI.FadeIn(0.3f);
            transitioning = false;
        }

        /// <summary>重开当前关；菜单态下无效，避免从菜单误触直接开局。</summary>
        public void RestartLevel()
        {
            if (transitioning) return;
            if (State == GameState.MainMenu) return;
            Time.timeScale = 1f;
            UI.ShowPause(false);
            StartLevel(CurrentLevelIndex);
        }

        /// <summary>进入下一关；已是最后一关时不做任何事。</summary>
        public void NextLevel()
        {
            if (transitioning || !HasNextLevel) return;
            StartLevel(CurrentLevelIndex + 1);
        }

        /// <summary>返回主菜单；走协程以便先淡出再拆场景。</summary>
        public void ReturnToMenu()
        {
            if (transitioning) return;
            StartCoroutine(ReturnToMenuRoutine());
        }

        // 回菜单的转场：先冻住玩家再淡出，防止淡出期间角色还能操作导致画面穿帮。
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

        /// <summary>暂停游戏：时间归零、音乐压低，只留暂停层在 HUD 之上。</summary>
        public void PauseGame()
        {
            // 只有游玩态可暂停；转场途中暂停会让淡入淡出卡在半路。
            if (State != GameState.Playing || transitioning) return;
            SetState(GameState.Paused);
            Time.timeScale = 0f;
            Audio.SetMusicDucked(true);
            UI.ShowPause(true);
        }

        /// <summary>从暂停恢复；非暂停态调用无效。</summary>
        public void ResumeGame()
        {
            if (State != GameState.Paused) return;
            SetState(GameState.Playing);
            Time.timeScale = 1f;
            Audio.SetMusicDucked(false);
            UI.ShowPause(false);
        }

        /// <summary>拾取一枚金币，只累加本局数量，通关时才统一写入存档。</summary>
        public void AddRunCoin()
        {
            RunCoins++;
        }

        /// <summary>关卡通关回调：先落盘成绩，再延迟弹出胜利界面。</summary>
        public void OnLevelCompleted()
        {
            // 只有游玩态才算通关，重复触发（终点多次碰撞）在这里被挡掉。
            if (State != GameState.Playing) return;
            SetState(GameState.Victory);
            int stars = CalculateStars();
            var data = CurrentLevelData;
            int levelIndex = CurrentLevelIndex;
            // 先写存档再弹界面：玩家即使直接杀进程，成绩也已经保存。
            Save.RecordLevel(data.id, levelIndex, Levels.Count, RunTime, RunCoins);
            StartCoroutine(VictoryRoutine(data, stars, levelIndex));
        }

        // 胜利界面延迟出现，让终点动画与音效播完；期间状态或关卡变了就放弃弹出。
        IEnumerator VictoryRoutine(LevelData data, int stars, int levelIndex)
        {
            // 用 Realtime 等待，结算期间 timeScale 可能被外部改动，缩放时间会不准。
            yield return new WaitForSecondsRealtime(0.9f);
            if (State != GameState.Victory || CurrentLevelIndex != levelIndex) yield break;
            var record = Save.GetRecord(data.id);
            float best = record != null ? record.bestTime : RunTime;
            UI.ShowVictory(data, RunTime, RunCoins, Level.TotalCoins, stars, HasNextLevel, best);
        }

        /// <summary>玩家死亡回调：累计死亡数并切到失败流程。</summary>
        public void OnPlayerDefeated()
        {
            // 只有游玩态才计死亡；胜利或暂停时的误触发一律忽略。
            if (State != GameState.Playing) return;
            Deaths++;
            int levelIndex = CurrentLevelIndex;
            SetState(GameState.GameOver);
            StartCoroutine(GameOverRoutine(levelIndex));
        }

        // 失败界面延迟弹出，留出死亡特效的时间；期间若已重开或切关就不再显示。
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
            // 一星保底；用时不超过 parTime 的 1.25 倍升二星；再满金币且达标时间才给三星。
            var data = CurrentLevelData;
            int stars = 1;
            if (RunTime <= data.parTime * 1.25f) stars = 2;
            if (RunTime <= data.parTime && RunCoins >= data.CoinCount) stars = 3;
            return stars;
        }

        /// <summary>应用设置项：先刷新音频参数再落盘，保证下次启动读到新值。</summary>
        public void ApplySettings()
        {
            Audio.RefreshSettings(Save.Data);
            Save.Save();
        }

        /// <summary>退出游戏：编辑器里停播放模式，真机走 Application.Quit。</summary>
        public void QuitGame()
        {
            // 退出前补一次存档，防止玩家改完设置直接关窗口丢进度。
            Save.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ 辅助

        void SetupCamera()
        {
            // 相机可能是场景里现成的，也可能在空场景里不存在，这里兜底创建。
            MainCamera = Camera.main;
            if (MainCamera == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                MainCamera = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }

            // 统一切成正交 + 半高 8 单位：像素图在任何窗口比例下都不会被拉伸。
            MainCamera.orthographic = true;
            MainCamera.orthographicSize = 8f;
            MainCamera.clearFlags = CameraClearFlags.SolidColor;
            MainCamera.backgroundColor = ThemePalette.Get("day").skyTop;
            MainCamera.transform.position = new Vector3(0f, 3f, -10f);
            if (MainCamera.GetComponent<AudioListener>() == null)
                MainCamera.gameObject.AddComponent<AudioListener>();

            // CameraFollow 由代码挂载并统一配置，预制体里漏挂也不会出现相机不跟随。
            cameraFollow = MainCamera.GetComponent<CameraFollow>();
            if (cameraFollow == null) cameraFollow = MainCamera.gameObject.AddComponent<CameraFollow>();
            cameraFollow.Configure(8f);
        }

        // 拆关统一走这里：先销毁关卡控制器，再清空场景根节点下的全部子物体。
        void ClearScene()
        {
            if (Level != null)
            {
                Destroy(Level.gameObject);
                Level = null;
            }

            if (sceneRoot == null) return;
            // 倒序删除：将来若换成 DestroyImmediate，正序会因索引前移漏删一半子节点。
            for (int i = sceneRoot.childCount - 1; i >= 0; i--)
                Destroy(sceneRoot.GetChild(i).gameObject);
        }

        // 退出播放或关闭应用时补一次存档，兜住没走 QuitGame 的关闭路径。
        void OnApplicationQuit()
        {
            Save?.Save();
        }

        // 只清自己的静态引用：被销毁的重复副本不能把别人的 Instance 抹掉。
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
