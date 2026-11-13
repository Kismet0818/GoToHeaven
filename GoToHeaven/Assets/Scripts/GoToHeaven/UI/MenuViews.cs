using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>Main menu prefab controller: layout is authored in MainMenu.prefab.</summary>
    public sealed class MainMenuView : ScreenView
    {
        readonly UIManager ui;
        readonly TextMeshProUGUI info;

        public MainMenuView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            ui = owner;
            info = Find<TextMeshProUGUI>(viewRoot, "Info");

            Bind(Find<Button>(viewRoot, "Play"), () => ui.Game.PlayFromMenu());
            Bind(Find<Button>(viewRoot, "Levels"), () => ui.ShowLevelSelect());
            Bind(Find<Button>(viewRoot, "Settings"), () => ui.ShowSettings(() => ui.ShowMainMenu()));
            Bind(Find<Button>(viewRoot, "Quit"), () => ui.Game.QuitGame());
        }

        public override void Show()
        {
            base.Show();
            if (info == null) return;

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

    /// <summary>Level select prefab controller. Cards are baked into the prefab; logic is bound here.</summary>
    public sealed class LevelSelectView : ScreenView
    {
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
        }

        public LevelSelectView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            ui = owner;

            foreach (Transform child in viewRoot)
            {
                if (child.name != "LevelCard") continue;
                var card = new Card
                {
                    root = child.gameObject,
                    panel = child.GetComponent<Image>(),
                    button = child.GetComponent<Button>(),
                    number = Find<TextMeshProUGUI>(child, "Number"),
                    name = Find<TextMeshProUGUI>(child, "Name"),
                    stats = Find<TextMeshProUGUI>(child, "Stats"),
                    lockOverlay = child.Find("Locked") != null ? child.Find("Locked").gameObject : null
                };

                int slot = cards.Count;
                if (card.button != null)
                {
                    card.button.onClick.RemoveAllListeners();
                    card.button.onClick.AddListener(() => OnCardClicked(slot));
                }
                cards.Add(card);
            }

            pageLabel = Find<TextMeshProUGUI>(viewRoot, "Page");
            previousButton = Find<Button>(viewRoot, "Previous");
            nextButton = Find<Button>(viewRoot, "Next");
            Bind(previousButton, () => ChangePage(-1));
            Bind(nextButton, () => ChangePage(1));
            Bind(Find<Button>(viewRoot, "Back"), () => ui.ShowMainMenu());
        }

        void OnCardClicked(int slot)
        {
            int index = page * PageSize + slot;
            if (index < ui.Game.Levels.Count && ui.Game.Save.IsUnlocked(index))
                ui.Game.StartLevel(index);
        }

        void ChangePage(int delta)
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(ui.Game.Levels.Count / (float)PageSize));
            page = Mathf.Clamp(page + delta, 0, pages - 1);
            Refresh();
        }

        public override void Show()
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(ui.Game.Levels.Count / (float)PageSize));
            page = Mathf.Clamp(page, 0, pages - 1);
            Refresh();
            base.Show();
        }

        public void Refresh()
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(ui.Game.Levels.Count / (float)PageSize));
            if (previousButton != null) previousButton.gameObject.SetActive(pages > 1);
            if (nextButton != null) nextButton.gameObject.SetActive(pages > 1);
            if (pageLabel != null)
            {
                pageLabel.gameObject.SetActive(pages > 1);
                pageLabel.text = $"{page + 1} / {pages}";
            }

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
                var level = ui.Game.Levels.Get(index);
                var record = ui.Game.Save.GetRecord(level.id);
                bool unlocked = ui.Game.Save.IsUnlocked(index);
                bool completed = record != null && record.completed;

                if (card.number != null)
                {
                    card.number.text = (index + 1).ToString("00");
                    card.number.color = completed ? UITheme.Gold : UITheme.Muted;
                }
                if (card.name != null) card.name.text = level.displayName.ToUpperInvariant();
                if (card.stats != null)
                {
                    card.stats.text = completed
                        ? $"BEST  {FormatTime(record.bestTime)}\nCOINS  {record.bestCoins} / {level.CoinCount}"
                        : unlocked
                            ? $"{level.subtitle}\nBEST  --:--.--"
                            : "CLEAR THE PREVIOUS LEVEL";
                }

                if (card.panel != null) card.panel.color = completed ? new Color(1f, 0.92f, 0.72f, 1f) : Color.white;
                if (card.button != null) card.button.interactable = unlocked;
                if (card.lockOverlay != null) card.lockOverlay.SetActive(!unlocked);
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

    /// <summary>Settings prefab controller; controls are found by name and wired here.</summary>
    public sealed class SettingsView : ScreenView
    {
        readonly UIManager ui;
        readonly Slider masterSlider;
        readonly Slider musicSlider;
        readonly Slider sfxSlider;
        readonly TextMeshProUGUI masterValue;
        readonly TextMeshProUGUI musicValue;
        readonly TextMeshProUGUI sfxValue;
        readonly Button shakeToggle;
        readonly Image shakeCheck;
        readonly Button hintToggle;
        readonly Image hintCheck;
        Action onBack;

        public SettingsView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            ui = owner;
            var panel = viewRoot.Find("Panel");
            if (panel == null) panel = viewRoot;

            masterSlider = Find<Slider>(panel, "MASTER VOLUMESlider");
            musicSlider = Find<Slider>(panel, "MUSICSlider");
            sfxSlider = Find<Slider>(panel, "SOUND EFFECTSSlider");
            masterValue = Find<TextMeshProUGUI>(panel, "MASTER VOLUMEValue");
            musicValue = Find<TextMeshProUGUI>(panel, "MUSICValue");
            sfxValue = Find<TextMeshProUGUI>(panel, "SOUND EFFECTSValue");

            var data = ui.Game.Save.Data;
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveAllListeners();
                masterSlider.onValueChanged.AddListener(v =>
                {
                    data.masterVolume = v;
                    ui.Game.Audio.RefreshSettings(data);
                    RefreshValues();
                });
            }
            if (musicSlider != null)
            {
                musicSlider.onValueChanged.RemoveAllListeners();
                musicSlider.onValueChanged.AddListener(v =>
                {
                    data.musicVolume = v;
                    ui.Game.Audio.RefreshSettings(data);
                    RefreshValues();
                });
            }
            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(v =>
                {
                    data.sfxVolume = v;
                    ui.Game.Audio.RefreshSettings(data);
                    RefreshValues();
                });
            }

            shakeToggle = Find<Button>(panel, "ShakeToggle");
            shakeCheck = Find<Image>(panel, "ShakeToggle/Check");
            hintToggle = Find<Button>(panel, "HintToggle");
            hintCheck = Find<Image>(panel, "HintToggle/Check");
            BindToggle(shakeToggle, shakeCheck, data.screenShake, v =>
            {
                data.screenShake = v;
                ui.Game.ApplySettings();
            });
            BindToggle(hintToggle, hintCheck, data.showHints, v =>
            {
                data.showHints = v;
                ui.Game.ApplySettings();
            });

            Bind(Find<Button>(panel, "Back"), () => onBack?.Invoke());
        }

        static void BindToggle(Button button, Image check, bool initial, Action<bool> onChange)
        {
            bool value = initial;
            if (check != null) check.gameObject.SetActive(value);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                value = !value;
                if (check != null) check.gameObject.SetActive(value);
                GameManager.Instance?.Audio?.Play(Sfx.Click);
                onChange?.Invoke(value);
            });
        }

        public void Configure(Action back)
        {
            onBack = back;
            RefreshValues();
        }

        void RefreshValues()
        {
            var data = ui.Game.Save.Data;
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(data.masterVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(data.musicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(data.sfxVolume);
            if (masterValue != null) masterValue.text = Mathf.RoundToInt(data.masterVolume * 100f) + "%";
            if (musicValue != null) musicValue.text = Mathf.RoundToInt(data.musicVolume * 100f) + "%";
            if (sfxValue != null) sfxValue.text = Mathf.RoundToInt(data.sfxVolume * 100f) + "%";
        }

        public override void Show()
        {
            base.Show();
            RefreshValues();
        }

        public override void Hide()
        {
            if (ui != null && ui.Game != null && ui.Game.Save != null) ui.Game.Save.Save();
            base.Hide();
        }
    }
}
