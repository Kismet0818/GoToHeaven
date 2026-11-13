using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoToHeaven
{
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

        public HudView(UIManager owner, Transform parent) : base(parent, "HUD")
        {
            ui = owner;
            group.blocksRaycasts = false;

            var levelPanel = UIFactory.Image(root.transform, "LevelPanel",
                ProceduralArt.Panel(new Color(0.06f, 0.07f, 0.12f, 0.86f), new Color(1f, 1f, 1f, 0.1f)), Color.white);
            UIFactory.Place(levelPanel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24f, -20f), new Vector2(370f, 94f));
            levelPanel.raycastTarget = false;

            levelName = UIFactory.Text(levelPanel.transform, "LevelName", "LEVEL", 26f, UITheme.Cream);
            UIFactory.Place(levelName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -12f), new Vector2(330f, 36f));
            levelName.alignment = TextAlignmentOptions.Left;
            levelName.characterSpacing = 3f;

            timer = UIFactory.Text(levelPanel.transform, "Timer", "00:00.00", 36f, UITheme.Gold);
            UIFactory.Place(timer.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 10f), new Vector2(300f, 46f));
            timer.alignment = TextAlignmentOptions.BottomLeft;

            var statsPanel = UIFactory.Image(root.transform, "StatsPanel",
                ProceduralArt.Panel(new Color(0.06f, 0.07f, 0.12f, 0.86f), new Color(1f, 1f, 1f, 0.1f)), Color.white);
            UIFactory.Place(statsPanel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -20f), new Vector2(430f, 72f));
            statsPanel.raycastTarget = false;

            for (int i = 0; i < hearts.Length; i++)
            {
                var heart = UIFactory.Image(statsPanel.transform, $"Heart{i}", ProceduralArt.Heart(UITheme.Danger), Color.white);
                UIFactory.Place(heart.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(20f + i * 44f, 4f), new Vector2(38f, 34f));
                heart.preserveAspect = true;
                hearts[i] = heart;
            }

            var art = ui.Game.Art;
            var fruit = art.Frames(ArtPaths.Fruit, 32, 32, 24f, SpriteLibrary.CenterPivot);
            var coinIcon = UIFactory.Image(statsPanel.transform, "CoinIcon",
                fruit.Length > 0 ? fruit[0] : ProceduralArt.Circle(12, UITheme.Gold), Color.white);
            UIFactory.Place(coinIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(166f, 3f), new Vector2(34f, 34f));
            coinIcon.preserveAspect = true;

            coinText = UIFactory.Text(statsPanel.transform, "CoinText", "0 / 0", 24f, UITheme.Cream);
            UIFactory.Place(coinText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(208f, 2f), new Vector2(130f, 36f));
            coinText.alignment = TextAlignmentOptions.Left;

            var pause = UIFactory.Button(statsPanel.transform, "Pause", "", ProceduralArt.PauseIcon(UITheme.Cream),
                new Vector2(54f, 54f), () => ui.Game.PauseGame(), primary: true);
            UIFactory.Place(pause.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-10f, 0f), new Vector2(54f, 54f));

            hint = UIFactory.Text(root.transform, "Hint",
                "A / D  MOVE     SPACE  JUMP     SHIFT  DASH     S + SPACE  DROP", 20f, new Color(1f, 1f, 1f, 0.85f));
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.09f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1300f, 40f));
            hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            hintGroup.alpha = 0f;

            toast = UIFactory.Text(root.transform, "Toast", "", 42f, UITheme.Gold);
            UIFactory.Place(toast.rectTransform, new Vector2(0.5f, 0.76f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(900f, 70f));
            toast.characterSpacing = 8f;
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastGroup.alpha = 0f;
        }

        public void Build(LevelData data)
        {
            levelName.text = data.displayName.ToUpperInvariant();
            hint.text = "A / D  MOVE     SPACE  JUMP     SHIFT  DASH     S + SPACE  DROP";
            hintTimer = ui.Game.Save.Data.showHints ? 6f : 0f;
            hintGroup.alpha = hintTimer > 0f ? 1f : 0f;
            toastGroup.alpha = 0f;
            toastTimer = 0f;
        }

        public void UpdateStats(int health, int maxHealth, int coins, int totalCoins, float time)
        {
            for (int i = 0; i < hearts.Length; i++)
            {
                bool full = i < health;
                hearts[i].color = full ? Color.white : new Color(1f, 1f, 1f, 0.14f);
            }

            coinText.text = totalCoins > 0 ? $"{coins} / {totalCoins}" : coins.ToString();
            timer.text = FormatTime(time);
        }

        public void Toast(string message)
        {
            toast.text = message;
            toastGroup.alpha = 1f;
            toastTimer = 1.6f;
        }

        public void Tick(float deltaTime)
        {
            if (hintTimer > 0f)
            {
                hintTimer -= deltaTime;
                if (hintTimer <= 1f) hintGroup.alpha = Mathf.Clamp01(hintTimer);
            }

            if (toastTimer > 0f)
            {
                toastTimer -= deltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.5f);
            }
        }

        public override void Show()
        {
            base.Show();
            hintGroup.alpha = hintTimer > 0f ? 1f : 0f;
        }

        static string FormatTime(float seconds)
        {
            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes:00}:{rest:00.00}";
        }
    }

    public sealed class PauseView : ScreenView
    {
        public PauseView(UIManager owner, Transform parent) : base(parent, "Pause")
        {
            var dim = UIFactory.Image(root.transform, "Dim", null, new Color(0.02f, 0.02f, 0.05f, 0.74f), true);
            UIFactory.Stretch(dim.rectTransform);

            var panel = UIFactory.Image(root.transform, "Panel",
                ProceduralArt.Panel(new Color(0.07f, 0.08f, 0.14f, 0.96f), new Color(1f, 1f, 1f, 0.12f)), Color.white, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(500f, 560f));

            var title = UIFactory.Label(panel.transform, "Title", "PAUSED", 54f, UITheme.Cream,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(400f, 80f));
            title.characterSpacing = 8f;

            AddButton(panel.transform, "Resume", "RESUME", owner.Game.Art.Get(ArtPaths.IconPlay), 110f,
                () => owner.Game.ResumeGame(), true);
            AddButton(panel.transform, "Restart", "RESTART", owner.Game.Art.Get(ArtPaths.IconRestart), 20f,
                () => owner.Game.RestartLevel(), false);
            AddButton(panel.transform, "Menu", "MAIN MENU", owner.Game.Art.Get(ArtPaths.IconBack), -70f,
                () => owner.Game.ReturnToMenu(), false);

            var hint = UIFactory.Label(panel.transform, "Hint", "ESC  RESUME     R  RESTART", 18f, UITheme.Muted,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -196f), new Vector2(400f, 30f));
            hint.characterSpacing = 4f;
        }

        static void AddButton(Transform parent, string name, string label, Sprite icon, float y, System.Action action, bool primary)
        {
            var button = UIFactory.Button(parent, name, label, icon, new Vector2(360f, 70f), action, primary);
            UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(360f, 70f));
        }
    }

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

        public ResultView(UIManager owner, Transform parent) : base(parent, "Result")
        {
            ui = owner;

            var dim = UIFactory.Image(root.transform, "Dim", null, new Color(0.02f, 0.02f, 0.05f, 0.82f), true);
            UIFactory.Stretch(dim.rectTransform);

            panel = UIFactory.Image(root.transform, "Panel",
                ProceduralArt.Panel(new Color(0.07f, 0.08f, 0.14f, 0.97f), new Color(1f, 1f, 1f, 0.12f)), Color.white, true);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 660f));

            title = UIFactory.Label(panel.transform, "Title", "HEAVEN REACHED", 62f, UITheme.Gold,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(700f, 90f));
            title.characterSpacing = 7f;

            subtitle = UIFactory.Label(panel.transform, "Subtitle", "", 22f, UITheme.Muted,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 194f), new Vector2(680f, 40f));

            for (int i = 0; i < stars.Length; i++)
            {
                var star = UIFactory.Image(panel.transform, $"Star{i}", ProceduralArt.Star(UITheme.Gold), Color.white);
                UIFactory.Place(star.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((i - 1) * 82f, 115f), new Vector2(64f, 64f));
                star.preserveAspect = true;
                stars[i] = star;
            }

            stats = UIFactory.Text(panel.transform, "Stats", "", 28f, UITheme.Cream, TextAlignmentOptions.Center);
            UIFactory.Place(stats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(620f, 160f));
            stats.lineSpacing = 10f;

            best = UIFactory.Label(panel.transform, "Best", "", 20f, UITheme.Gold,
                new Vector2(0.5f, 0.5f), new Vector2(0f, -92f), new Vector2(600f, 34f));

            gameOverGroup = BuildGroup("GameOverButtons", -172f,
                ("RETRY", ArtPaths.IconRestart, (System.Action)(() => ui.Game.RestartLevel()), true),
                ("MAIN MENU", ArtPaths.IconBack, (System.Action)(() => ui.Game.ReturnToMenu()), false));

            var victoryRoot = UIFactory.Rect("VictoryButtons", panel.transform);
            victoryGroup = victoryRoot.gameObject;
            UIFactory.Place(victoryRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -172f), new Vector2(700f, 260f));

            nextButton = UIFactory.Button(victoryGroup.transform, "Next", "NEXT LEVEL", ui.Game.Art.Get(ArtPaths.IconNext),
                new Vector2(340f, 64f), () => ui.Game.NextLevel(), true);
            UIFactory.Place(nextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 52f), new Vector2(340f, 64f));

            var replay = UIFactory.Button(victoryGroup.transform, "Replay", "REPLAY", ui.Game.Art.Get(ArtPaths.IconRestart),
                new Vector2(340f, 58f), () => ui.Game.RestartLevel());
            UIFactory.Place(replay.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -22f), new Vector2(340f, 58f));

            var menu = UIFactory.Button(victoryGroup.transform, "Menu", "MAIN MENU", ui.Game.Art.Get(ArtPaths.IconBack),
                new Vector2(340f, 52f), () => ui.Game.ReturnToMenu());
            UIFactory.Place(menu.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -92f), new Vector2(340f, 52f));

            victoryGroup.SetActive(false);
        }

        GameObject BuildGroup(string name, float y, params (string label, string icon, System.Action action, bool primary)[] buttons)
        {
            var rootRt = UIFactory.Rect(name, panel.transform);
            UIFactory.Place(rootRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, y), new Vector2(700f, 220f));

            for (int i = 0; i < buttons.Length; i++)
            {
                var info = buttons[i];
                var button = UIFactory.Button(rootRt, info.label, info.label, ui.Game.Art.Get(info.icon),
                    new Vector2(340f, 64f), info.action, info.primary);
                UIFactory.Place(button.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 52f - i * 74f), new Vector2(340f, 64f));
            }
            return rootRt.gameObject;
        }

        public void ShowGameOver(LevelData data, float time, int coins, int totalCoins, int deaths, int bestCoins)
        {
            title.text = "YOU FELL";
            title.color = UITheme.Danger;
            subtitle.text = "THE HEAVENS WILL WAIT...";
            panel.color = Color.white;
            foreach (var star in stars) star.gameObject.SetActive(false);
            stats.text = $"TIME    {FormatTime(time)}\nCOINS   {coins} / {Mathf.Max(totalCoins, coins)}\nFALLS   {deaths}";
            best.text = bestCoins > 0 ? $"BEST COIN RUN  {bestCoins}" : "";
            gameOverGroup.SetActive(true);
            victoryGroup.SetActive(false);
            Show();
        }

        public void ShowVictory(LevelData data, float time, int coins, int totalCoins, int starCount, bool hasNext, float bestTime)
        {
            title.text = "HEAVEN REACHED";
            title.color = UITheme.Gold;
            subtitle.text = data.displayName.ToUpperInvariant();
            foreach (var star in stars) star.gameObject.SetActive(true);
            for (int i = 0; i < stars.Length; i++)
                stars[i].color = i < starCount ? Color.white : new Color(1f, 1f, 1f, 0.16f);

            stats.text = $"TIME    {FormatTime(time)}\nCOINS   {coins} / {totalCoins}\nPAR     {FormatTime(data.parTime)}";
            best.text = bestTime > 0f ? $"BEST TIME  {FormatTime(bestTime)}" : "";
            gameOverGroup.SetActive(false);
            victoryGroup.SetActive(true);
            nextButton.interactable = hasNext;
            nextButton.GetComponentInChildren<TextMeshProUGUI>().text = hasNext ? "NEXT LEVEL" : "NO MORE LEVELS";
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
