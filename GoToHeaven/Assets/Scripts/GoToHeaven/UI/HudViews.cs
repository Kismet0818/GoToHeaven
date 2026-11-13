using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoToHeaven
{
    /// <summary>HUD prefab controller: updates texts/hearts, owns the pause button.</summary>
    public sealed class HudView : ScreenView
    {
        readonly UIManager ui;
        readonly TextMeshProUGUI levelName;
        readonly TextMeshProUGUI timer;
        readonly TextMeshProUGUI coinText;
        readonly Image[] hearts = new Image[PlayerHealth.MaxHearts];
        readonly TextMeshProUGUI hint;
        readonly CanvasGroup hintGroup;
        readonly TextMeshProUGUI toast;
        readonly CanvasGroup toastGroup;

        float hintTimer;
        float toastTimer;

        public HudView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            ui = owner;

            levelName = Find<TextMeshProUGUI>(viewRoot, "LevelPanel/LevelName");
            timer = Find<TextMeshProUGUI>(viewRoot, "LevelPanel/Timer");
            for (int i = 0; i < hearts.Length; i++)
                hearts[i] = Find<Image>(viewRoot, $"StatsPanel/Heart{i}");
            coinText = Find<TextMeshProUGUI>(viewRoot, "StatsPanel/CoinText");

            Bind(Find<Button>(viewRoot, "StatsPanel/Pause"), () => ui.Game.PauseGame());

            hint = Find<TextMeshProUGUI>(viewRoot, "Hint");
            toast = Find<TextMeshProUGUI>(viewRoot, "Toast");
            hintGroup = hint != null ? hint.GetComponent<CanvasGroup>() : null;
            toastGroup = toast != null ? toast.GetComponent<CanvasGroup>() : null;
        }

        public void Build(LevelData data)
        {
            if (levelName != null) levelName.text = data.displayName.ToUpperInvariant();
            hintTimer = ui.Game.Save.Data.showHints ? 6f : 0f;
            if (hintGroup != null) hintGroup.alpha = hintTimer > 0f ? 1f : 0f;
            if (toastGroup != null) toastGroup.alpha = 0f;
            toastTimer = 0f;
        }

        public void UpdateStats(int health, int maxHealth, int coins, int totalCoins, float time)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] == null) continue;
                bool full = i < health;
                hearts[i].color = full ? Color.white : new Color(1f, 1f, 1f, 0.14f);
            }

            if (coinText != null) coinText.text = totalCoins > 0 ? $"{coins} / {totalCoins}" : coins.ToString();
            if (timer != null) timer.text = FormatTime(time);
        }

        public void Toast(string message)
        {
            if (toast == null) return;
            toast.text = message;
            if (toastGroup != null) toastGroup.alpha = 1f;
            toastTimer = 1.6f;
        }

        public void Tick(float deltaTime)
        {
            if (hintTimer > 0f)
            {
                hintTimer -= deltaTime;
                if (hintTimer <= 1f && hintGroup != null) hintGroup.alpha = Mathf.Clamp01(hintTimer);
            }

            if (toastTimer > 0f)
            {
                toastTimer -= deltaTime;
                if (toastGroup != null) toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.5f);
            }
        }

        public override void Show()
        {
            base.Show();
            if (hintGroup != null) hintGroup.alpha = hintTimer > 0f ? 1f : 0f;
        }

        static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes:00}:{rest:00.00}";
        }
    }

    /// <summary>Pause prefab controller.</summary>
    public sealed class PauseView : ScreenView
    {
        public PauseView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            var panel = viewRoot.Find("Panel");
            if (panel == null) panel = viewRoot;
            Bind(Find<Button>(panel, "Resume"), () => owner.Game.ResumeGame());
            Bind(Find<Button>(panel, "Restart"), () => owner.Game.RestartLevel());
            Bind(Find<Button>(panel, "Menu"), () => owner.Game.ReturnToMenu());
        }
    }

    /// <summary>Victory / game-over prefab controller.</summary>
    public sealed class ResultView : ScreenView
    {
        readonly UIManager ui;
        readonly Image panel;
        readonly TextMeshProUGUI title;
        readonly TextMeshProUGUI subtitle;
        readonly TextMeshProUGUI stats;
        readonly TextMeshProUGUI best;
        readonly Image[] stars = new Image[3];
        readonly GameObject gameOverGroup;
        readonly GameObject victoryGroup;
        readonly Button nextButton;

        public ResultView(UIManager owner, Transform viewRoot) : base(viewRoot)
        {
            ui = owner;
            var root = viewRoot.Find("Panel");
            if (root == null) root = viewRoot;

            panel = root.GetComponent<Image>();
            title = Find<TextMeshProUGUI>(root, "Title");
            subtitle = Find<TextMeshProUGUI>(root, "Subtitle");
            stats = Find<TextMeshProUGUI>(root, "Stats");
            best = Find<TextMeshProUGUI>(root, "Best");
            for (int i = 0; i < stars.Length; i++)
                stars[i] = Find<Image>(root, $"Star{i}");

            gameOverGroup = root.Find("GameOverButtons") != null ? root.Find("GameOverButtons").gameObject : null;
            victoryGroup = root.Find("VictoryButtons") != null ? root.Find("VictoryButtons").gameObject : null;

            Bind(Find<Button>(root, "GameOverButtons/RETRY"), () => ui.Game.RestartLevel());
            Bind(Find<Button>(root, "GameOverButtons/MAIN MENU"), () => ui.Game.ReturnToMenu());
            nextButton = Find<Button>(root, "VictoryButtons/Next");
            Bind(nextButton, () => ui.Game.NextLevel());
            Bind(Find<Button>(root, "VictoryButtons/Replay"), () => ui.Game.RestartLevel());
            Bind(Find<Button>(root, "VictoryButtons/Menu"), () => ui.Game.ReturnToMenu());

            if (victoryGroup != null) victoryGroup.SetActive(false);
        }

        public void ShowGameOver(LevelData data, float time, int coins, int totalCoins, int deaths, int bestCoins)
        {
            if (title != null)
            {
                title.text = "YOU FELL";
                title.color = UITheme.Danger;
            }
            if (subtitle != null) subtitle.text = "THE HEAVENS WILL WAIT...";
            if (panel != null) panel.color = Color.white;
            foreach (var star in stars)
                if (star != null) star.gameObject.SetActive(false);

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
            for (int i = 0; i < stars.Length; i++)
                if (stars[i] != null) stars[i].color = i < starCount ? Color.white : new Color(1f, 1f, 1f, 0.16f);

            if (stats != null)
                stats.text = $"TIME    {FormatTime(time)}\nCOINS   {coins} / {totalCoins}\nPAR     {FormatTime(data.parTime)}";
            if (best != null) best.text = bestTime > 0f ? $"BEST TIME  {FormatTime(bestTime)}" : "";
            if (gameOverGroup != null) gameOverGroup.SetActive(false);
            if (victoryGroup != null) victoryGroup.SetActive(true);
            if (nextButton != null)
            {
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
            return $"{minutes:00}:{rest:00.00}";
        }
    }
}
