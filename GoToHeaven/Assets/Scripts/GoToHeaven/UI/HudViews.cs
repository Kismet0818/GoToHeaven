using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>HUD prefab 控制器：刷新关卡名、计时、金币与心数图标，并持有暂停按钮。</summary>
    public sealed class HudView : ScreenView
    {
        readonly UIManager ui;
        readonly TextMeshProUGUI levelName;
        readonly TextMeshProUGUI timer;
        readonly TextMeshProUGUI coinText;
        // 心数图标数组：长度由 PlayerHealth.MaxHearts 决定，prefab 必须提供同样数量的 HeartN 子节点
        readonly Image[] hearts = new Image[PlayerHealth.MaxHearts];
        readonly TextMeshProUGUI hint;
        readonly CanvasGroup hintGroup;
        readonly TextMeshProUGUI toast;
        readonly CanvasGroup toastGroup;

        // 提示与 Toast 的剩余显示时间，由 Tick 递减，归零即彻底淡出
        float hintTimer;
        float toastTimer;

        public HudView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            ui = owner;

            // 所有引用都按相对 viewRoot 的 transform.Find 路径取，查不到返回 null，后续使用处逐个判空
            levelName = Find<TextMeshProUGUI>(viewRoot, "LevelPanel/LevelName");
            timer = Find<TextMeshProUGUI>(viewRoot, "LevelPanel/Timer");
            // 心数图标命名必须是 Heart0、Heart1… 与下标严格对应，名字写错只会静默少一颗心
            for (int i = 0; i < hearts.Length; i++)
                hearts[i] = Find<Image>(viewRoot, $"StatsPanel/Heart{i}");
            coinText = Find<TextMeshProUGUI>(viewRoot, "StatsPanel/CoinText");

            // Bind 会先清掉 prefab 上的旧监听，保证暂停逻辑只由代码这一处驱动
            Bind(Find<Button>(viewRoot, "StatsPanel/Pause"), () => ui.Game.PauseGame());

            // 提示与 Toast 的淡入淡出靠 CanvasGroup.alpha，因此两者都必须挂 CanvasGroup
            hint = Find<TextMeshProUGUI>(viewRoot, "Hint");
            toast = Find<TextMeshProUGUI>(viewRoot, "Toast");
            hintGroup = hint != null ? hint.GetComponent<CanvasGroup>() : null;
            toastGroup = toast != null ? toast.GetComponent<CanvasGroup>() : null;
        }

        public void Build(LevelData data)
        {
            // 关卡名统一转大写，保持美术设定的全大写风格；TMP 文本只在这里赋值
            if (levelName != null) levelName.text = data.displayName.ToUpperInvariant();
            // 关闭了提示开关就直接给 0，避免先显示再隐藏的闪烁
            hintTimer = ui.Game.Save.Data.showHints ? 6f : 0f;
            if (hintGroup != null) hintGroup.alpha = hintTimer > 0f ? 1f : 0f;
            // 进入关卡时清空 Toast 状态，防止上一关的残留文案
            if (toastGroup != null) toastGroup.alpha = 0f;
            toastTimer = 0f;
        }

        public void UpdateStats(int health, int maxHealth, int coins, int totalCoins, float time)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null) continue;
                bool full = i < health;
                // 不用 SetActive 而是压低 alpha：布局不抖动，空槽位仍占位
                hearts[i].color = full ? Color.white : new Color(1f, 1f, 1f, 0.14f);
            }

            // totalCoins 为 0 表示该关没有金币总量，退化成分数单独显示
            if (coinText != null) coinText.text = totalCoins > 0 ? $"{coins} / {totalCoins}" : coins.ToString();
            if (timer != null) timer.text = FormatTime(time);
        }

        public void Toast(string message)
        {
            if (toast == null) return;
            toast.text = message;
            if (toastGroup != null) toastGroup.alpha = 1f;
            // 固定 1.6 秒：后 0.5 秒由 Tick 负责淡出
            toastTimer = 1.6f;
        }

        public void Tick(float deltaTime)
        {
            // 提示只在最后一秒淡出（alpha 直接跟着剩余时间线性下降）
            if (hintTimer > 0f)
            {
                hintTimer -= deltaTime;
                if (hintTimer <= 1f && hintGroup != null) hintGroup.alpha = Mathf.Clamp01(hintTimer);
            }

            // Toast 用 0.5 秒做淡出，除以 0.5 把剩余时间归一到 0~1
            if (toastTimer > 0f)
            {
                toastTimer -= deltaTime;
                if (toastGroup != null) toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.5f);
            }
        }

        public override void Show()
        {
            base.Show();
            // 重新显示时按计时器状态恢复 alpha：Hide 期间 Tick 可能已把透明度改掉
            if (hintGroup != null) hintGroup.alpha = hintTimer > 0f ? 1f : 0f;
        }

        static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            // 分:秒.百分秒，秒保留两位小数；注意 59.996 这类值会显示成 60.00 而不进位到分钟
            return $"{minutes:00}:{rest:00.00}";
        }
    }

    /// <summary>暂停界面的 prefab 控制器。</summary>
    public sealed class PauseView : ScreenView
    {
        public PauseView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            // 优先在 Panel 子节点下找按钮，找不到就把根节点当容器，兼容两种 prefab 结构
            var panel = viewRoot.Find("Panel");
            if (panel == null) panel = viewRoot;
            Bind(Find<Button>(panel, "Resume"), () => owner.Game.ResumeGame());
            Bind(Find<Button>(panel, "Restart"), () => owner.Game.RestartLevel());
            Bind(Find<Button>(panel, "Menu"), () => owner.Game.ReturnToMenu());
        }
    }

    /// <summary>胜利 / 失败结算界面的 prefab 控制器。</summary>
    public sealed class ResultView : ScreenView
    {
        readonly UIManager ui;
        readonly Image panel;
        readonly TextMeshProUGUI title;
        readonly TextMeshProUGUI subtitle;
        readonly TextMeshProUGUI stats;
        readonly TextMeshProUGUI best;
        // 三颗星：多划少划都不行，成就星数按星数点亮前 N 颗
        readonly Image[] stars = new Image[3];
        readonly GameObject gameOverGroup;
        readonly GameObject victoryGroup;
        readonly Button nextButton;

        public ResultView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            ui = owner;
            // 与暂停界面同样的兼容策略：有 Panel 就用 Panel，没有就直接用根节点
            var root = viewRoot.Find("Panel");
            if (root == null) root = viewRoot;

            panel = root.GetComponent<Image>();
            title = Find<TextMeshProUGUI>(root, "Title");
            subtitle = Find<TextMeshProUGUI>(root, "Subtitle");
            stats = Find<TextMeshProUGUI>(root, "Stats");
            best = Find<TextMeshProUGUI>(root, "Best");
            for (int i = 0; i < stars.Length; i++)
                stars[i] = Find<Image>(root, $"Star{i}");

            // 胜利与失败各有一套按钮组，靠互相 SetActive 切换；这里先缓存 GameObject 备用
            gameOverGroup = root.Find("GameOverButtons") != null ? root.Find("GameOverButtons").gameObject : null;
            victoryGroup = root.Find("VictoryButtons") != null ? root.Find("VictoryButtons").gameObject : null;

            // 注意 "MAIN MENU" 节点名里带空格，路径必须与 prefab 完全一致
            Bind(Find<Button>(root, "GameOverButtons/RETRY"), () => ui.Game.RestartLevel());
            Bind(Find<Button>(root, "GameOverButtons/MAIN MENU"), () => ui.Game.ReturnToMenu());
            nextButton = Find<Button>(root, "VictoryButtons/Next");
            Bind(nextButton, () => ui.Game.NextLevel());
            Bind(Find<Button>(root, "VictoryButtons/Replay"), () => ui.Game.RestartLevel());
            Bind(Find<Button>(root, "VictoryButtons/Menu"), () => ui.Game.ReturnToMenu());

            // 默认先收起胜利按钮组，避免首次显示时两边同时出现
            if (victoryGroup != null) victoryGroup.SetActive(false);
        }

        public void ShowGameOver(LevelData data, float time, int coins, int totalCoins, int deaths, int bestCoins)
        {
            if (title != null)
            {
                title.text = "YOU FELL";
                // 失败标题用警示红，与胜利的金色形成对比
                title.color = UITheme.Danger;
            }
            if (subtitle != null) subtitle.text = "THE HEAVENS WILL WAIT...";
            // 面板底色恢复纯白，避免上一局胜利时留下的染色
            if (panel != null) panel.color = Color.white;
            foreach (var star in stars)
                if (star != null) star.gameObject.SetActive(false);

            // 用空格对齐三列，字符串里的换行与空格是排版的一部分，不能改动
            if (stats != null)
                stats.text = $"TIME    {FormatTime(time)}\nCOINS   {coins} / {Mathf.Max(totalCoins, coins)}\nFALLS   {deaths}";
            if (best != null) best.text = bestCoins > 0 ? $"BEST COIN RUN  {bestCoins}" : "";
            if (gameOverGroup != null) gameOverGroup.SetActive(true);
            if (victoryGroup != null) victoryGroup.SetActive(false);
            Show();
        }

        public void ShowVictory(LevelData data, float time, int coins, int totalCoins, int starCount, bool hasNext, float bestTime)
        {
            if (title != null)
            {
                title.text = "HEAVEN REACHED";
                title.color = UITheme.Gold;
            }
            if (subtitle != null) subtitle.text = data.displayName.ToUpperInvariant();

            foreach (var star in stars)
                if (star != null) star.gameObject.SetActive(true);
            // 未获得的星压到 0.16 alpha，保留星形轮廓而不是直接隐藏
            for (int i = 0; i < stars.Length; i++)
                if (stars[i] != null) stars[i].color = i < starCount ? Color.white : new Color(1f, 1f, 1f, 0.16f);

            // PAR 是关卡设计的目标时间，放这里方便玩家对照自己成绩
            if (stats != null)
                stats.text = $"TIME    {FormatTime(time)}\nCOINS   {coins} / {totalCoins}\nPAR     {FormatTime(data.parTime)}";
            if (best != null) best.text = bestTime > 0f ? $"BEST TIME  {FormatTime(bestTime)}" : "";
            if (gameOverGroup != null) gameOverGroup.SetActive(false);
            if (victoryGroup != null) victoryGroup.SetActive(true);
            if (nextButton != null)
            {
                // 没有下一关时按钮置灰，同时把文案换成说明，避免玩家反复点击
                nextButton.interactable = hasNext;
                var label = nextButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = hasNext ? "NEXT LEVEL" : "NO MORE LEVELS";
            }
            Show();
        }

        static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            // 与 HUD 保持一致的分:秒.百分秒格式，两处必须同步修改
            return $"{minutes:00}:{rest:00.00}";
        }
    }
}
