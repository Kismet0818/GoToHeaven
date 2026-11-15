using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>主菜单的 prefab 控制器：界面布局全部在 MainMenu.prefab 里，代码只绑定按钮并刷新信息栏。</summary>
    public sealed class MainMenuView : ScreenView
    {
        readonly UIManager ui;
        readonly TextMeshProUGUI info;

        public MainMenuView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            ui = owner;
            info = Find<TextMeshProUGUI>(viewRoot, "Info");

            // 四个入口：开始游戏、关卡选择、设置（返回时回到主菜单本身）、退出
            Bind(Find<Button>(viewRoot, "Play"), () => ui.Game.PlayFromMenu());
            Bind(Find<Button>(viewRoot, "Levels"), () => ui.ShowLevelSelect());
            Bind(Find<Button>(viewRoot, "Settings"), () => ui.ShowSettings(() => ui.ShowMainMenu()));
            Bind(Find<Button>(viewRoot, "Quit"), () => ui.Game.QuitGame());
        }

        public override void Show()
        {
            base.Show();
            if (info == null) return;

            // 每次显示都重算通关数：玩家可能刚从关卡里回来，缓存会导致数字过期
            var save = ui.Game.Save.Data;
            int completed = 0;
            foreach (var level in ui.Game.Levels.Levels)
            {
                var record = ui.Game.Save.GetRecord(level.id);
                if (record != null && record.completed) completed++;
            }
            // 金币按三位补零、两组数据间用 6 个空格拉开，视觉对齐依赖这些空格
            info.text = $"COINS  {save.totalCoins:000}      LEVELS  {completed} / {ui.Game.Levels.Count}";
        }
    }

    /// <summary>关卡选择界面的 prefab 控制器。卡片已烘进 prefab，这里只负责按页刷新与绑定点击。</summary>
    public sealed class LevelSelectView : ScreenView
    {
        // 每页固定 6 张卡：prefab 里预摆的 LevelCard 数量必须与之相同，否则会出现空槽或多余卡
        const int PageSize = 6;

        readonly UIManager ui;
        readonly List<Card> cards = new List<Card>();
        readonly TextMeshProUGUI pageLabel;
        readonly Button previousButton;
        readonly Button nextButton;
        int page;

        // 单张关卡卡的引用集合：一个 prefab 节点上要同时改底色、序号、标题、成绩、可点击性与锁罩
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

            // 遍历直接子节点收集同名 LevelCard；子节点顺序即槽位顺序，调整层级会改变卡片与关卡的对应关系
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

                // 先取整数槽位再塞进闭包：捕获的是每张卡自己的副本，不会出现所有卡都点开最后一关的问题
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
            // 槽位加上页偏移才是真实关卡下标；越界或未解锁一律忽略，防止越权开局
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
            // 显示前先校正页码：关卡总数或解锁状态可能已变化，沿用上次的页码会越界
            int pages = Mathf.Max(1, Mathf.CeilToInt(ui.Game.Levels.Count / (float)PageSize));
            page = Mathf.Clamp(page, 0, pages - 1);
            Refresh();
            base.Show();
        }

        public void Refresh()
        {
            int pages = Mathf.Max(1, Mathf.CeilToInt(ui.Game.Levels.Count / (float)PageSize));
            // 只有一页时整组翻页控件隐藏，避免出现可点但无意义的按钮
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
                // 该页不足 6 关时，多余的卡整节点关掉，否则会显示空白卡或上一页的残留数据
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
                    // 序号两位补零，1 号关显示 "01"
                    card.number.text = (index + 1).ToString("00");
                    card.number.color = completed ? UITheme.Gold : UITheme.Muted;
                }
                if (card.name != null) card.name.text = level.displayName.ToUpperInvariant();
                if (card.stats != null)
                {
                    // 三种文案：已通关给最佳成绩、已解锁给副标题占位、未解锁提示需先过前一关
                    card.stats.text = completed
                        ? $"BEST  {FormatTime(record.bestTime)}\nCOINS  {record.bestCoins} / {level.CoinCount}"
                        : unlocked
                            ? $"{level.subtitle}\nBEST  --:--.--"
                            : "CLEAR THE PREVIOUS LEVEL";
                }

                // 已通关的卡片染成暖金色，作为视觉上的完成标记
                if (card.panel != null) card.panel.color = completed ? new Color(1f, 0.92f, 0.72f, 1f) : Color.white;
                if (card.button != null) card.button.interactable = unlocked;
                if (card.lockOverlay != null) card.lockOverlay.SetActive(!unlocked);
            }
        }

        static string FormatTime(float seconds)
        {
            // 0 或负数表示没有记录，显示占位符而不是 00:00.00
            if (seconds <= 0f) return "--:--.--";
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes:00}:{rest:00.00}";
        }
    }

    /// <summary>设置界面的 prefab 控制器；控件按节点名查找后在这里接线。</summary>
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

            // 节点名是“标签文本 + 控件类型后缀”直接拼接的，中间没有空格，写错只会静默取不到引用
            masterSlider = Find<Slider>(panel, "MASTER VOLUMESlider");
            musicSlider = Find<Slider>(panel, "MUSICSlider");
            sfxSlider = Find<Slider>(panel, "SOUND EFFECTSSlider");
            masterValue = Find<TextMeshProUGUI>(panel, "MASTER VOLUMEValue");
            musicValue = Find<TextMeshProUGUI>(panel, "MUSICValue");
            sfxValue = Find<TextMeshProUGUI>(panel, "SOUND EFFECTSValue");

            // data 是存档里的同一份对象，滑条改动直接落到存档字段上，无需额外拷贝
            var data = ui.Game.Save.Data;
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.RemoveAllListeners();
                masterSlider.onValueChanged.AddListener(v =>
                {
                    data.masterVolume = v;
                    // 先让音频系统重新套用混音参数，再回填百分比文字
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

            // 勾选框用一张 Image 的显隐表示开关状态，因此 Toggle 的表现完全由 BindToggle 保证一致
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

            // 返回动作由打开设置的一方注入（主菜单回主菜单、暂停页回暂停页）
            Bind(Find<Button>(panel, "Back"), () => onBack?.Invoke());
        }

        static void BindToggle(Button button, Image check, bool initial, Action<bool> onChange)
        {
            // 用闭包变量保存状态，而不是回头去读存档，避免外部改动后显示与逻辑不一致
            bool value = initial;
            if (check != null) check.gameObject.SetActive(value);
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                value = !value;
                if (check != null) check.gameObject.SetActive(value);
                // 先出点击音效再回调，开关手感更即时
                GameManager.Instance?.Audio?.Play(Sfx.Click);
                onChange?.Invoke(value);
            });
        }

        public void Configure(Action back)
        {
            onBack = back;
            // 每次打开都刷新一遍：音量可能在别处被改过
            RefreshValues();
        }

        void RefreshValues()
        {
            var data = ui.Game.Save.Data;
            // SetValueWithoutNotify 避免回填时再触发 onValueChanged 造成递归刷新
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(data.masterVolume);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(data.musicVolume);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(data.sfxVolume);
            // 存档里是 0~1 的浮点，界面显示为整数百分比
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
            // 离开设置页立刻落盘，防止玩家直接退出游戏导致设置丢失
            if (ui != null && ui.Game != null && ui.Game.Save != null) ui.Game.Save.Save();
            base.Hide();
        }
    }
}
