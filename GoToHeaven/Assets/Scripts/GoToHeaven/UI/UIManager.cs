using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>
    /// Builds and owns every screen of the game with uGUI at runtime.
    /// No Canvas prefab or scene reference is required.
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
            gameObject.layer = LayerMask.NameToLayer("UI");

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            gameObject.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();
            FontAsset = CreateFont();
            UIFactory.Font = FontAsset;

            mainMenu = new MainMenuView(this, transform);
            levelSelect = new LevelSelectView(this, transform);
            settings = new SettingsView(this, transform);
            hud = new HudView(this, transform);
            pause = new PauseView(this, transform);
            result = new ResultView(this, transform);

            BuildFade();
            ShowMainMenu();
            SetFade(1f);
        }

        void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(transform, false);
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        TMP_FontAsset CreateFont()
        {
            var fontFile = Resources.Load<Font>(ArtPaths.Font);
            if (fontFile != null)
            {
                try
                {
                    var created = TMP_FontAsset.CreateFontAsset(fontFile, 90, 9, GlyphRenderMode.SDFAA,
                        1024, 1024, AtlasPopulationMode.Dynamic, true);
                    if (created != null)
                    {
                        created.name = "Kaph Runtime";
                        created.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                        created.isMultiAtlasTexturesEnabled = true;
                        if (created.material != null && created.atlasTextures != null &&
                            created.atlasTextures.Length > 0 && created.atlasTextures[0] != null)
                            return created;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[GoToHeaven] Could not create the Kaph TMP font, falling back: {e.Message}");
                }
            }

            if (TMP_Settings.defaultFontAsset != null) return TMP_Settings.defaultFontAsset;
            var liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (liberation != null) return liberation;

            try
            {
                var osFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial" }, 90);
                if (osFont != null) return TMP_FontAsset.CreateFontAsset(osFont);
            }
            catch
            {
                // ignored, we surface the error below
            }

            Debug.LogError("[GoToHeaven] No TMP font could be created. UI text will be missing.");
            return null;
        }

        void BuildFade()
        {
            var rt = UIFactory.FullScreen("Fade", transform);
            fadeImage = rt.gameObject.AddComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = true;
            fadeGroup = UIFactory.Group(rt.gameObject);
            fadeGroup.alpha = 1f;
            fadeGroup.blocksRaycasts = true;
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
            fadeGroup.blocksRaycasts = blocked;
        }

        // ------------------------------------------------------------------ fade

        public void SetFade(float alpha)
        {
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

        protected ScreenView(Transform parent, string name)
        {
            var rt = UIFactory.FullScreen(name, parent);
            root = rt.gameObject;
            group = UIFactory.Group(root);
            root.SetActive(false);
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
