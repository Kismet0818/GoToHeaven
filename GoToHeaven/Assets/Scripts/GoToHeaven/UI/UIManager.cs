using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>
    /// Lives on the GameCanvas prefab. It owns the six screen controllers and
    /// the fade overlay; all visual layout is authored in the prefabs.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
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
        ScreenView activeScreen;

        public void Initialize(GameManager owner)
        {
            game = owner;
            EnsureCanvas();
            EnsureEventSystem();

            var sample = GetComponentInChildren<TMP_Text>(true);
            FontAsset = sample != null ? sample.font : null;

            mainMenu = new MainMenuView(this, Require("MainMenu"));
            levelSelect = new LevelSelectView(this, Require("LevelSelect"));
            settings = new SettingsView(this, Require("Settings"));
            hud = new HudView(this, Require("HUD"));
            pause = new PauseView(this, Require("Pause"));
            result = new ResultView(this, Require("Result"));

            var fade = transform.Find("Fade");
            if (fade != null)
            {
                fadeGroup = fade.GetComponent<CanvasGroup>();
                fadeImage = fade.GetComponent<Image>();
            }

            if (fadeGroup == null) BuildFallbackFade();

            ShowMainMenu();
            SetFade(1f);
        }

        void EnsureCanvas()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
            }

            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
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
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        Transform Require(string name)
        {
            var found = transform.Find(name);
            if (found == null)
                throw new MissingReferenceException($"GameCanvas 缺少子界面 '{name}'。请检查 UI Prefab。");
            return found;
        }

        void BuildFallbackFade()
        {
            var rt = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            rt.transform.SetParent(transform, false);
            var rect = (RectTransform)rt.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            fadeImage = rt.GetComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = true;
            fadeGroup = rt.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;
        }

        // ------------------------------------------------------------------ screens

        public void ShowMainMenu()
        {
            HideAllScreens();
            mainMenu.Show();
            activeScreen = mainMenu;
        }

        public void ShowLevelSelect()
        {
            HideAllScreens();
            levelSelect.Refresh();
            levelSelect.Show();
            activeScreen = levelSelect;
        }

        public void ShowSettings(Action onBack)
        {
            HideAllScreens();
            settings.Configure(onBack);
            settings.Show();
            activeScreen = settings;
        }

        public void ShowHud(LevelData data)
        {
            hud.Build(data);
            hud.Show();
        }

        public void HideHud()
        {
            hud.Hide();
        }

        public void UpdateHud(int health, int maxHealth, int coins, int totalCoins, float time)
        {
            if (hud.Visible) hud.UpdateStats(health, maxHealth, coins, totalCoins, time);
        }

        public void TickHud(float deltaTime)
        {
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
            mainMenu.Hide();
            levelSelect.Hide();
            settings.Hide();
            pause.Hide();
            result.Hide();
            if (activeScreen != null && activeScreen != hud) activeScreen.Hide();
            activeScreen = null;
        }

        public void Toast(string message)
        {
            hud.Toast(message);
        }

        public void SetGameplayInputBlocked(bool blocked)
        {
            if (fadeGroup != null) fadeGroup.blocksRaycasts = blocked;
        }

        // ------------------------------------------------------------------ fade

        public void SetFade(float alpha)
        {
            if (fadeGroup == null) return;
            fadeGroup.alpha = alpha;
            fadeGroup.blocksRaycasts = alpha > 0.001f;
        }

        public IEnumerator FadeOut(float duration)
        {
            yield return FadeRoutine(1f, duration);
        }

        public IEnumerator FadeIn(float duration)
        {
            yield return FadeRoutine(0f, duration);
        }

        IEnumerator FadeRoutine(float target, float duration)
        {
            if (fadeGroup == null) yield break;
            fadeGroup.blocksRaycasts = true;
            float start = fadeGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration)));
                yield return null;
            }
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
            group = root.GetComponent<CanvasGroup>();
            if (group == null) group = root.AddComponent<CanvasGroup>();
        }

        protected static T Find<T>(Transform parent, string path) where T : Component
        {
            var child = parent.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        protected static void Bind(Button button, Action action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            if (action != null) button.onClick.AddListener(() => action());
        }

        public virtual void Show()
        {
            root.SetActive(true);
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        public virtual void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
