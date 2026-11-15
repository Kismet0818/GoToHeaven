using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>
    /// 挂在 GameCanvas prefab 上。持有六个界面控制器与全屏黑色遮罩；
    /// 所有视觉布局都在 prefab 里，代码只按名字取引用、切换状态。
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        // 从界面里采样到的 TMP 字体资源，运行时动态创建文本时需要复用它
        public TMP_FontAsset FontAsset { get; private set; }
        public GameManager Game => game;

        GameManager game;
        Canvas canvas;
        CanvasGroup fadeGroup;
        Image fadeImage;

        MainMenuView mainMenu;
        LevelSelectView levelSelect;
        SettingsView settings;
        HudView hud;
        PauseView pause;
        ResultView result;
        // 当前占据主区域的界面（HUD 不参与，它始终在底层常驻）
        ScreenView activeScreen;

        public void Initialize(GameManager owner)
        {
            game = owner;
            EnsureCanvas();
            EnsureEventSystem();

            // 六个界面都是 GameCanvas 的直接子节点，名字必须与下面 Require 的参数一致
            // 字体直接从任意一个 TMP 文本上采样，省去额外配置字段
            var sample = GetComponentInChildren<TMP_Text>(true);
            FontAsset = sample != null ? sample.font : null;

            mainMenu = new MainMenuView(this, Require("MainMenu"));
            levelSelect = new LevelSelectView(this, Require("LevelSelect"));
            settings = new SettingsView(this, Require("Settings"));
            hud = new HudView(this, Require("HUD"));
            pause = new PauseView(this, Require("Pause"));
            result = new ResultView(this, Require("Result"));

            // 全屏遮罩用于转场：先找 prefab 里的 Fade 节点，取它的 CanvasGroup 与 Image
            var fade = transform.Find("Fade");
            if (fade != null)
            {
                fadeGroup = fade.GetComponent<CanvasGroup>();
                fadeImage = fade.GetComponent<Image>();
            }

            // prefab 没做遮罩（或漏挂 CanvasGroup）时退回代码生成，保证转场逻辑永远可用
            if (fadeGroup == null) BuildFallbackFade();

            ShowMainMenu();
            // 初始黑屏：由转场协程负责淡出，避免开场瞬间看到未就绪的画面
            SetFade(1f);
        }

        void EnsureCanvas()
        {
            // 复用已有组件，缺什么补什么：prefab 可以只摆层级，Canvas 相关设置允许由代码补齐
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                // Overlay 模式不依赖相机，sortingOrder 100 保证压在其他 UI 之上
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
                // 以 1920x1080 为基准，宽高 0.5 折中缩放，兼顾超宽与竖屏
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            if (GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        void EnsureEventSystem()
        {
            // 场景里没有 EventSystem 时按钮全都点不动，所以初始化阶段兜底补一个
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        Transform Require(string name)
        {
            // 界面缺失属于 prefab 装配错误，直接抛异常比留 null 后在运行中空引用更好排查
            var found = transform.Find(name);
            if (found == null)
                throw new MissingReferenceException($"GameCanvas 缺少子界面 '{name}'。请检查 UI Prefab。");
            return found;
        }

        void BuildFallbackFade()
        {
            // 代码生成的兜底遮罩：必须同时具备 RectTransform、Image、CanvasGroup 三件套
            var rt = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            rt.transform.SetParent(transform, false);
            var rect = (RectTransform)rt.transform;
            // 四向锚定 + 零偏移，铺满整个 Canvas，任何分辨率下都不留缝
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            fadeImage = rt.GetComponent<Image>();
            fadeImage.color = Color.black;
            // 必须接收射线，配合 blocksRaycasts 才能在转场时挡住玩家点击
            fadeImage.raycastTarget = true;
            fadeGroup = rt.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;
        }

        // ------------------------------------------------------------------ 界面屏幕

        public void ShowMainMenu()
        {
            // 切界面的统一套路：先收起所有界面，再显示目标界面，最后记录 activeScreen
            HideAllScreens();
            mainMenu.Show();
            activeScreen = mainMenu;
        }

        public void ShowLevelSelect()
        {
            HideAllScreens();
            // 先刷新再显示：解锁状态与成绩可能在上一局后已经变化
            levelSelect.Refresh();
            levelSelect.Show();
            activeScreen = levelSelect;
        }

        public void ShowSettings(Action onBack)
        {
            HideAllScreens();
            // 返回回调由调用方注入，设置页自身不知道来源界面
            settings.Configure(onBack);
            settings.Show();
            activeScreen = settings;
        }

        public void ShowHud(LevelData data)
        {
            // HUD 不参与 activeScreen 轮换，只随关卡开始/结束独立开关
            hud.Build(data);
            hud.Show();
        }

        public void HideHud()
        {
            hud.Hide();
        }

        public void UpdateHud(int health, int maxHealth, int coins, int totalCoins, float time)
        {
            // 隐藏状态下跳过刷新，省掉每帧的文本重建开销
            if (hud.Visible) hud.UpdateStats(health, maxHealth, coins, totalCoins, time);
        }

        public void TickHud(float deltaTime)
        {
            // 提示与 Toast 的倒计时同样只在可见时推进
            if (hud.Visible) hud.Tick(deltaTime);
        }

        public void ShowPause(bool show)
        {
            if (show) pause.Show();
            else pause.Hide();
        }

        public void ShowGameOver(LevelData data, float time, int coins, int totalCoins, int deaths, int bestCoins)
        {
            HideAllScreens();
            // 结算时 HUD 要一起收起，否则会和结算面板叠在一起
            hud.Hide();
            result.ShowGameOver(data, time, coins, totalCoins, deaths, bestCoins);
            activeScreen = result;
        }

        public void ShowVictory(LevelData data, float time, int coins, int totalCoins, int stars, bool hasNext, float bestTime)
        {
            HideAllScreens();
            hud.Hide();
            result.ShowVictory(data, time, coins, totalCoins, stars, hasNext, bestTime);
            activeScreen = result;
        }

        public void HideAllScreens()
        {
            // 逐个显式收起：有些界面并非通过 ShowXxx 打开（例如暂停），不能只依赖 activeScreen
            mainMenu.Hide();
            levelSelect.Hide();
            settings.Hide();
            pause.Hide();
            result.Hide();
            // hud 可能正被 activeScreen 指着，但它的显隐由关卡流程管理，这里排除掉避免误关
            if (activeScreen != null && activeScreen != hud) activeScreen.Hide();
            activeScreen = null;
        }

        public void Toast(string message)
        {
            // 统一走 HUD 的 Toast，保证样式与曝光时间只有一处实现
            hud.Toast(message);
        }

        public void SetGameplayInputBlocked(bool blocked)
        {
            // 只切遮罩的射线拦截，不改 alpha：用于过场/暂停时挡住底下的点击
            if (fadeGroup != null) fadeGroup.blocksRaycasts = blocked;
        }

        // ------------------------------------------------------------------ 淡入淡出

        public void SetFade(float alpha)
        {
            if (fadeGroup == null) return;
            fadeGroup.alpha = alpha;
            // 几乎全透明时才放开射线，避免残留一层看不见的拦截
            fadeGroup.blocksRaycasts = alpha > 0.001f;
        }

        public IEnumerator FadeOut(float duration)
        {
            // FadeOut 指名的是“淡出画面”：alpha 升到 1，屏幕变黑
            yield return FadeRoutine(1f, duration);
        }

        public IEnumerator FadeIn(float duration)
        {
            // FadeIn 反之：alpha 降到 0，画面重新显现
            yield return FadeRoutine(0f, duration);
        }

        IEnumerator FadeRoutine(float target, float duration)
        {
            // 没有遮罩就直接结束，调用方仍可安全 yield 该协程
            if (fadeGroup == null) yield break;
            // 整个过渡期间都拦截点击，防止玩家在动画中途操作
            fadeGroup.blocksRaycasts = true;
            float start = fadeGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 用 unscaledDeltaTime：暂停（timeScale = 0）时转场仍需正常播放
                elapsed += Time.unscaledDeltaTime;
                // duration 取下限防止除零；Clamp01 保证不会过冲
                fadeGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration)));
                yield return null;
            }
            // 收尾直接给目标值，避免浮点累计误差留下半透明残影
            fadeGroup.alpha = target;
            fadeGroup.blocksRaycasts = target > 0.001f;
        }
    }

    public abstract class ScreenView
    {
        protected readonly GameObject root;
        protected readonly CanvasGroup group;

        public bool Visible => root != null && root.activeSelf;

        protected ScreenView(Transform viewRoot)
        {
            if (viewRoot == null) throw new MissingReferenceException("UI screen root is missing.");
            root = viewRoot.gameObject;
            // prefab 可能没挂 CanvasGroup，这里补一个，好让所有界面都能统一控制整体透明度与交互
            group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
        }

        protected static T Find<T>(Transform parent, string path) where T : Component
        {
            // 相对于传入节点的 transform.Find，只支持预制里固定的层级路径；找不到返回 null 由调用方判空
            var child = parent.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        protected static void Bind(Button button, Action action)
        {
            if (button == null) return;
            // 先清空 prefab 上可能存在的监听，保证按钮行为只由代码定义，重复初始化也不会叠加回调
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }

        public virtual void Show()
        {
            // 显示 = 打开节点并恢复交互；用 CanvasGroup 而不是逐个子物体开关，一次到位
            root.SetActive(true);
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        public virtual void Hide()
        {
            // 只关节点不卸载，下次显示无需重建 UI，代价是常驻内存
            if (root != null) root.SetActive(false);
        }
    }
}
