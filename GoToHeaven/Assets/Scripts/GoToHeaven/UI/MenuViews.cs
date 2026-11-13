using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoToHeaven
{
    public sealed class MainMenuView : ScreenView
    {
        readonly UIManager ui;
        readonly TextMeshProUGUI info;
        readonly TextMeshProUGUI version;

        public MainMenuView(UIManager owner, Transform parent) : base(parent, "MainMenu")
        {
            ui = owner;

            var dim = UIFactory.Image(root.transform, "Dim", null, new Color(0.02f, 0.02f, 0.05f, 0.24f), true);
            UIFactory.Stretch(dim.rectTransform);

            var titleShadow = UIFactory.Label(root.transform, "TitleShadow", "GO TO HEAVEN", 112f,
                new Color(0.03f, 0.03f, 0.06f, 0.75f), new Vector2(0.5f, 0.79f), new Vector2(7f, -7f), new Vector2(1400f, 200f));
            titleShadow.characterSpacing = 8f;
            titleShadow.fontStyle = FontStyles.Bold;

            var title = UIFactory.Label(root.transform, "Title", "GO TO HEAVEN", 112f, UITheme.Cream,
                new Vector2(0.5f, 0.79f), Vector2.zero, new Vector2(1400f, 200f));
            title.characterSpacing = 8f;
            title.fontStyle = FontStyles.Bold;

            var subtitle = UIFactory.Label(root.transform, "Subtitle", "A PIXEL PLATFORMER", 24f, UITheme.Gold,
                new Vector2(0.5f, 0.71f), Vector2.zero, new Vector2(900f, 50f));
            subtitle.characterSpacing = 12f;

            var heroFrames = owner.Game.Art.Frames(ArtPaths.PlayerIdle, 32, 32, 16f, SpriteLibrary.PlayerPivot);
            if (heroFrames.Length > 0)
            {
                var hero = UIFactory.Image(root.transform, "Hero", heroFrames[0], Color.white);
                UIFactory.Place(hero.rectTransform, new Vector2(0.78f, 0.46f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(330f, 330f));
                hero.preserveAspect = true;
                var animated = hero.gameObject.AddComponent<UIAnimatedImage>();
                animated.Target = hero;
                animated.Frames = heroFrames;
                animated.Fps = 10f;
            }

            var play = UIFactory.Button(root.transform, "Play", "PLAY", owner.Game.Art.Get(ArtPaths.IconPlay),
                new Vector2(390f, 74f), () => ui.Game.PlayFromMenu(), primary: true);
            UIFactory.Place(play.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 128f), play.GetComponent<RectTransform>().sizeDelta);

            var levels = UIFactory.Button(root.transform, "Levels", "LEVELS", owner.Game.Art.Get(ArtPaths.IconLevels),
                new Vector2(390f, 74f), () => ui.ShowLevelSelect());
            UIFactory.Place(levels.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 38f), levels.GetComponent<RectTransform>().sizeDelta);

            var settings = UIFactory.Button(root.transform, "Settings", "SETTINGS", owner.Game.Art.Get(ArtPaths.IconSettings),
                new Vector2(390f, 74f), () => ui.ShowSettings(() => ui.ShowMainMenu()));
            UIFactory.Place(settings.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -52f), settings.GetComponent<RectTransform>().sizeDelta);

            var quit = UIFactory.Button(root.transform, "Quit", "QUIT", owner.Game.Art.Get(ArtPaths.IconClose),
                new Vector2(390f, 74f), () => ui.Game.QuitGame());
            UIFactory.Place(quit.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -142f), quit.GetComponent<RectTransform>().sizeDelta);

            info = UIFactory.Label(root.transform, "Info", "", 22f, UITheme.Muted,
                new Vector2(0.5f, 0.115f), Vector2.zero, new Vector2(1200f, 40f));
            info.characterSpacing = 3f;

            version = UIFactory.Label(root.transform, "Version", "v1.0  |  TUANJIE 2022.3", 16f, new Color(1f, 1f, 1f, 0.4f),
                new Vector2(0.5f, 0.045f), Vector2.zero, new Vector2(800f, 30f));

            var hint = UIFactory.Label(root.transform, "Hint", "A / D  MOVE      SPACE  JUMP      SHIFT  DASH      ESC  PAUSE",
                18f, new Color(1f, 1f, 1f, 0.5f), new Vector2(0.5f, 0.165f), Vector2.zero, new Vector2(1400f, 36f));
            hint.characterSpacing = 2f;
        }

        public override void Show()
        {
            base.Show();
            var save = ui.Game.Save.Data;
            int completed = 0;
            foreach (var level in ui.Game.Levels.Levels)
            {
                var record = ui.Game.Save.GetRecord(level.id);
                if (record != null && record.completed) completed++;
            }
            info.text = $"COINS  {save.totalCoins:000}      LEVELS  {completed} / {ui.Game.Levels.Count}";
        }
    }

    public sealed class LevelSelectView : ScreenView
    {
        const int Columns = 3;
        const int PageSize = 6;

        readonly UIManager ui;
        readonly List<Card> cards = new List<Card>();
        readonly TextMeshProUGUI pageLabel;
        readonly Button previousButton;
        readonly Button nextButton;
        int page;

        sealed class Card
        {
            public GameObject root;
            public Image panel;
            public TextMeshProUGUI number;
            public TextMeshProUGUI name;
            public TextMeshProUGUI stats;
            public Button button;
            public GameObject lockOverlay;
            public TextMeshProUGUI lockText;
        }

        public LevelSelectView(UIManager owner, Transform parent) : base(parent, "LevelSelect")
        {
            ui = owner;

            var dim = UIFactory.Image(root.transform, "Dim", null, new Color(0.02f, 0.02f, 0.05f, 0.82f), true);
            UIFactory.Stretch(dim.rectTransform);

            var title = UIFactory.Label(root.transform, "Title", "CHOOSE A LEVEL", 66f, UITheme.Cream,
                new Vector2(0.5f, 0.86f), Vector2.zero, new Vector2(900f, 110f));
            title.characterSpacing = 6f;
            UIFactory.Label(root.transform, "Subtitle", "EVERY PEAK STARTS WITH ONE STEP", 20f, UITheme.Gold,
                new Vector2(0.5f, 0.795f), Vector2.zero, new Vector2(900f, 34f)).characterSpacing = 6f;

            for (int i = 0; i < PageSize; i++)
            {
                var card = BuildCard();
                int slot = i;
                card.button.onClick.AddListener(() =>
                {
                    int index = page * PageSize + slot;
                    if (index < ui.Game.Levels.Count) ui.Game.StartLevel(index);
                });
                cards.Add(card);
            }

            var back = UIFactory.Button(root.transform, "Back", "BACK", ui.Game.Art.Get(ArtPaths.IconBack),
                new Vector2(250f, 64f), () => ui.ShowMainMenu());
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0.12f, 0.12f), new Vector2(0.5f, 0.5f),
                Vector2.zero, back.GetComponent<RectTransform>().sizeDelta);

            previousButton = UIFactory.Button(root.transform, "Previous", "", ui.Game.Art.Get(ArtPaths.IconPrevious),
                new Vector2(64f, 64f), () => ChangePage(-1), primary: true);
            UIFactory.Place(previousButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.5f),
                new Vector2(-110f, 0f), previousButton.GetComponent<RectTransform>().sizeDelta);

            pageLabel = UIFactory.Label(root.transform, "Page", "1 / 1", 22f, UITheme.Muted,
                new Vector2(0.5f, 0.12f), Vector2.zero, new Vector2(120f, 40f));

            nextButton = UIFactory.Button(root.transform, "Next", "", ui.Game.Art.Get(ArtPaths.IconNext),
                new Vector2(64f, 64f), () => ChangePage(1), primary: true);
            UIFactory.Place(nextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.12f), new Vector2(0.5f, 0.5f),
                new Vector2(110f, 0f), nextButton.GetComponent<RectTransform>().sizeDelta);
        }

        Card BuildCard()
        {
            var card = new Card();
            var rt = UIFactory.Rect("LevelCard", root.transform);
            card.root = rt.gameObject;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = ProceduralArt.Panel(UITheme.Panel, new Color(1f, 1f, 1f, 0.1f));
            image.type = Image.Type.Sliced;
            image.raycastTarget = true;
            card.panel = image;

            card.button = rt.gameObject.AddComponent<Button>();
            card.button.targetGraphic = image;
            card.button.transition = Selectable.Transition.ColorTint;
            card.button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.96f, 0.85f, 1f),
                pressedColor = new Color(0.85f, 0.8f, 0.68f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.6f, 0.6f, 0.65f, 0.8f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            card.button.navigation = new Navigation { mode = Navigation.Mode.None };
            rt.gameObject.AddComponent<UIButtonFX>();

            card.number = UIFactory.Text(rt, "Number", "01", 54f, UITheme.Gold);
            UIFactory.Place(card.number.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -16f), new Vector2(120f, 64f));
            card.number.alignment = TextAlignmentOptions.TopLeft;

            card.name = UIFactory.Text(rt, "Name", "FIRST STEPS", 26f, UITheme.Cream);
            UIFactory.Place(card.name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(22f, -86f), new Vector2(290f, 40f));
            card.name.alignment = TextAlignmentOptions.TopLeft;

            card.stats = UIFactory.Text(rt, "Stats", "", 20f, UITheme.Muted);
            UIFactory.Place(card.stats.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(22f, 22f), new Vector2(290f, 70f));
            card.stats.alignment = TextAlignmentOptions.BottomLeft;

            var lockRt = UIFactory.FullScreen("Locked", rt);
            var lockBg = lockRt.gameObject.AddComponent<Image>();
            lockBg.color = new Color(0.03f, 0.03f, 0.06f, 0.78f);
            lockBg.raycastTarget = false;
            card.lockOverlay = lockRt.gameObject;

            var lockIcon = UIFactory.Image(lockRt, "LockIcon", ProceduralArt.Lock(UITheme.Muted), Color.white);
            UIFactory.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(36f, 44f));

            card.lockText = UIFactory.Text(lockRt, "LockedText", "LOCKED", 20f, UITheme.Muted);
            UIFactory.Place(card.lockText.rectTransform, new Vector2(0.5f, 0.36f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(240f, 30f));
            card.lockText.characterSpacing = 4f;
            card.lockOverlay.SetActive(false);
            return card;
        }

        void ChangePage(int delta)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(ui.Game.Levels.Count / (float)PageSize));
            page = Mathf.Clamp(page + delta, 0, pages - 1);
            Refresh();
        }

        public override void Show()
        {
            page = Mathf.Clamp(page, 0, Mathf.Max(0, Mathf.CeilToInt(ui.Game.Levels.Count / (float)PageSize) - 1));
            Refresh();
            base.Show();
        }

        public void Refresh()
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(ui.Game.Levels.Count / (float)PageSize));
            previousButton.gameObject.SetActive(pages > 1);
            nextButton.gameObject.SetActive(pages > 1);
            pageLabel.gameObject.SetActive(pages > 1);
            pageLabel.text = $"{page + 1} / {pages}";

            int columns = Mathf.Min(Columns, Mathf.Max(1, ui.Game.Levels.Count));
            for (int slot = 0; slot < cards.Count; slot++)
            {
                int index = page * PageSize + slot;
                var card = cards[slot];
                if (index >= ui.Game.Levels.Count)
                {
                    card.root.SetActive(false);
                    continue;
                }

                card.root.SetActive(true);
                int row = slot / Columns;
                int column = slot % Columns;
                int rowsOnPage = Mathf.Min(2, Mathf.CeilToInt((ui.Game.Levels.Count - page * PageSize) / (float)Columns));
                float x = (column - (columns - 1) * 0.5f) * 370f;
                float y = rowsOnPage <= 1 ? 0f : (row == 0 ? 130f : -150f);
                UIFactory.Place(card.root.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(340f, 250f));

                var level = ui.Game.Levels.Get(index);
                var record = ui.Game.Save.GetRecord(level.id);
                bool unlocked = ui.Game.Save.IsUnlocked(index);
                bool completed = record != null && record.completed;

                card.number.text = (index + 1).ToString("00");
                card.name.text = level.displayName.ToUpperInvariant();
                card.stats.text = completed
                    ? $"BEST  {FormatTime(record.bestTime)}\nCOINS  {record.bestCoins} / {level.CoinCount}"
                    : unlocked
                        ? $"{level.subtitle}\nBEST  --:--.--"
                        : "CLEAR THE PREVIOUS LEVEL";

                card.number.color = completed ? UITheme.Gold : UITheme.Muted;
                card.panel.color = completed ? new Color(1f, 0.92f, 0.72f, 1f) : Color.white;
                card.button.interactable = unlocked;
                card.lockOverlay.SetActive(!unlocked);
            }
        }

        static string FormatTime(float seconds)
        {
            if (seconds <= 0f) return "--:--.--";
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes:00}:{rest:00.00}";
        }
    }

    public sealed class SettingsView : ScreenView
    {
        readonly UIManager ui;
        readonly Slider masterSlider;
        readonly Slider musicSlider;
        readonly Slider sfxSlider;
        readonly TextMeshProUGUI masterValue;
        readonly TextMeshProUGUI musicValue;
        readonly TextMeshProUGUI sfxValue;
        Action onBack;

        public SettingsView(UIManager owner, Transform parent) : base(parent, "Settings")
        {
            ui = owner;

            var dim = UIFactory.Image(root.transform, "Dim", null, new Color(0.02f, 0.02f, 0.05f, 0.8f), true);
            UIFactory.Stretch(dim.rectTransform);

            var panel = UIFactory.Image(root.transform, "Panel", ProceduralArt.Panel(UITheme.Panel, new Color(1f, 1f, 1f, 0.12f)), Color.white, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(780f, 720f));

            var title = UIFactory.Label(panel.transform, "Title", "SETTINGS", 56f, UITheme.Cream,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 278f), new Vector2(600f, 80f));
            title.characterSpacing = 6f;

            var data = ui.Game.Save.Data;
            masterSlider = AddSlider(panel.transform, "MASTER VOLUME", data.masterVolume, 168f, out masterValue,
                v => { data.masterVolume = v; ui.Game.Audio.RefreshSettings(data); RefreshValues(); });
            musicSlider = AddSlider(panel.transform, "MUSIC", data.musicVolume, 84f, out musicValue,
                v => { data.musicVolume = v; ui.Game.Audio.RefreshSettings(data); RefreshValues(); });
            sfxSlider = AddSlider(panel.transform, "SOUND EFFECTS", data.sfxVolume, 0f, out sfxValue,
                v => { data.sfxVolume = v; ui.Game.Audio.RefreshSettings(data); RefreshValues(); });

            UIFactory.Label(panel.transform, "ShakeLabel", "SCREEN SHAKE", 24f, UITheme.Cream,
                new Vector2(0.5f, 0.5f), new Vector2(-160f, -96f), new Vector2(340f, 40f), TextAlignmentOptions.Left);
            UIFactory.Toggle(panel.transform, "ShakeToggle", data.screenShake, v =>
            {
                data.screenShake = v;
                ui.Game.ApplySettings();
            }, out _, new Vector2(0.5f, 0.5f), new Vector2(240f, -96f));

            UIFactory.Label(panel.transform, "HintLabel", "ON-SCREEN HINTS", 24f, UITheme.Cream,
                new Vector2(0.5f, 0.5f), new Vector2(-160f, -156f), new Vector2(340f, 40f), TextAlignmentOptions.Left);
            UIFactory.Toggle(panel.transform, "HintToggle", data.showHints, v =>
            {
                data.showHints = v;
                ui.Game.ApplySettings();
            }, out _, new Vector2(0.5f, 0.5f), new Vector2(240f, -156f));

            var controls = UIFactory.Text(panel.transform, "Controls",
                "A / D  or  ARROW KEYS    MOVE\nSPACE / W    JUMP  (hold for a higher jump)\nSHIFT / J    DASH\nS / DOWN + SPACE    DROP THROUGH ONE-WAY PLATFORMS\nESC    PAUSE",
                20f, UITheme.Muted, TextAlignmentOptions.Left);
            UIFactory.Place(controls.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -246f), new Vector2(620f, 130f));
            controls.enableWordWrapping = true;
            controls.lineSpacing = 8f;

            var back = UIFactory.Button(panel.transform, "Back", "BACK", ui.Game.Art.Get(ArtPaths.IconBack),
                new Vector2(260f, 64f), () => onBack?.Invoke());
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -320f), back.GetComponent<RectTransform>().sizeDelta);
        }

        Slider AddSlider(Transform parent, string label, float value, float y, out TextMeshProUGUI valueLabel, Action<float> onChange)
        {
            UIFactory.Label(parent, label + "Label", label, 24f, UITheme.Cream,
                new Vector2(0.5f, 0.5f), new Vector2(-210f, y), new Vector2(300f, 40f), TextAlignmentOptions.Left);

            var slider = UIFactory.Slider(parent, label + "Slider", value, onChange,
                new Vector2(0.5f, 0.5f), new Vector2(110f, y), 330f);

            valueLabel = UIFactory.Label(parent, label + "Value", Mathf.RoundToInt(value * 100f) + "%", 22f, UITheme.Gold,
                new Vector2(0.5f, 0.5f), new Vector2(300f, y), new Vector2(80f, 34f), TextAlignmentOptions.Right);
            return slider;
        }

        public void Configure(Action back)
        {
            onBack = back;
            RefreshValues();
        }

        void RefreshValues()
        {
            var data = ui.Game.Save.Data;
            masterValue.text = Mathf.RoundToInt(data.masterVolume * 100f) + "%";
            musicValue.text = Mathf.RoundToInt(data.musicVolume * 100f) + "%";
            sfxValue.text = Mathf.RoundToInt(data.sfxVolume * 100f) + "%";
        }

        public override void Hide()
        {
            if (ui != null && ui.Game != null && ui.Game.Save != null) ui.Game.Save.Save();
            base.Hide();
        }
    }
}
