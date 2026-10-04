using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ricochet
{
    public class UIManager : MonoBehaviour
    {
        [SerializeField] RectTransform safeArea;
        [SerializeField] RectTransform floatingRoot;
        [SerializeField] TextMeshProUGUI floatingPrefab;

        [SerializeField] MainMenuPanel mainMenu = new MainMenuPanel();
        [SerializeField] InstructionsPanel instructions = new InstructionsPanel();
        [SerializeField] LeaderboardPanel leaderboard = new LeaderboardPanel();
        [SerializeField] ScanPanel scan = new ScanPanel();
        [SerializeField] SetupPanel setup = new SetupPanel();
        [SerializeField] CountdownPanel countdown = new CountdownPanel();
        [SerializeField] HUDPanel hud = new HUDPanel();
        [SerializeField] PausePanel pause = new PausePanel();
        [SerializeField] EndPanel end = new EndPanel();

        static readonly Color PointsColor = new Color(0.3f, 1f, 1f);
        static readonly Color RicochetColor = new Color(1f, 0.25f, 0.85f);
        static readonly Color BlockedColor = new Color(0.75f, 0.5f, 1f);

        readonly Dictionary<GameStateId, UIPanel> panels = new Dictionary<GameStateId, UIPanel>();
        readonly List<Popup> popups = new List<Popup>();
        ObjectPool<TextMeshProUGUI> popupPool;
        GameManager game;
        Rect appliedSafeArea;

        class Popup
        {
            public TextMeshProUGUI Text;
            public Vector3 World;
            public float Age;
        }

        void Start()
        {
            game = GameManager.Instance;
            panels[GameStateId.MainMenu] = mainMenu;
            panels[GameStateId.Instructions] = instructions;
            panels[GameStateId.Leaderboard] = leaderboard;
            panels[GameStateId.Scanning] = scan;
            panels[GameStateId.Setup] = setup;
            panels[GameStateId.Countdown] = countdown;
            panels[GameStateId.Playing] = hud;
            panels[GameStateId.Paused] = pause;
            panels[GameStateId.GameOver] = end;

            foreach (var panel in panels.Values)
            {
                panel.Init(game);
                panel.Hide();
            }

            foreach (var button in GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(() => AudioManager.Instance?.Play(SoundId.UIClick));

            popupPool = new ObjectPool<TextMeshProUGUI>(floatingPrefab, 12, floatingRoot);
            game.Score.Gained += OnPointsGained;
            GameEvents.HitBlocked += OnHitBlocked;
            game.States.Changed += OnStateChanged;
            if (game.States.Current != null) Show(game.States.Current.Id);
        }

        void OnDestroy()
        {
            GameEvents.HitBlocked -= OnHitBlocked;
            if (!game) return;
            game.States.Changed -= OnStateChanged;
            game.Score.Gained -= OnPointsGained;
        }

        void OnStateChanged(IGameState previous, IGameState current) => Show(current.Id);

        void Show(GameStateId id)
        {
            panels.TryGetValue(id, out var target);
            foreach (var panel in panels.Values)
                if (panel != target) panel.Hide();
            if (target != null && !target.IsVisible) target.Show();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var panel in panels.Values)
                if (panel.IsVisible) panel.Tick(dt);
            FitSafeArea();
            UpdatePopups(dt);
        }

        void FitSafeArea()
        {
            Rect safe = Screen.safeArea;
            if (safe == appliedSafeArea || Screen.width <= 0 || Screen.height <= 0) return;
            appliedSafeArea = safe;
            safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        void OnPointsGained(int points, float multiplier, Vector3 world)
        {
            ShowPopup("+" + points, PointsColor, 64f, world);
            if (multiplier > 1f)
                ShowPopup($"x{multiplier:0.#} RICOCHET!", RicochetColor, 56f, world + Vector3.up * 0.25f);
        }

        void OnHitBlocked(Vector3 world) => ShowPopup("MIRROR SHOTS ONLY!", BlockedColor, 46f, world);

        void ShowPopup(string text, Color color, float size, Vector3 world)
        {
            var t = popupPool.Get(Vector3.zero, Quaternion.identity);
            t.text = text;
            t.color = color;
            t.fontSize = size;
            popups.Add(new Popup { Text = t, World = world });
        }

        void UpdatePopups(float dt)
        {
            var cam = game.Player.Camera;
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.Age += dt;
                float t = p.Age / 1.1f;
                Vector3 screen = cam.WorldToScreenPoint(p.World + Vector3.up * 0.35f * Mathf.Clamp01(t));
                p.Text.enabled = screen.z > 0f;
                if (screen.z > 0f)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(floatingRoot, screen, null, out var local);
                    p.Text.rectTransform.anchoredPosition = local;
                }
                p.Text.alpha = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                p.Text.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.25f - t) * 2f);
                if (t < 1f) continue;
                popupPool.Return(p.Text);
                popups.RemoveAt(i);
            }
        }
    }

    [Serializable]
    public abstract class UIPanel
    {
        [SerializeField] protected GameObject root;

        CanvasGroup group;
        protected GameManager Game { get; private set; }
        public bool IsVisible => root && root.activeSelf;

        public void Init(GameManager game)
        {
            Game = game;
            group = root.GetComponent<CanvasGroup>();
            OnInit();
        }

        public void Show()
        {
            root.SetActive(true);
            if (group) group.alpha = 0f;
            OnShow();
        }

        public void Hide()
        {
            if (!root.activeSelf) return;
            OnHide();
            root.SetActive(false);
        }

        public void Tick(float dt)
        {
            if (group && group.alpha < 1f) group.alpha = Mathf.MoveTowards(group.alpha, 1f, dt * 6f);
            OnTick(dt);
        }

        protected abstract void OnInit();
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnTick(float dt) { }

        protected static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }

    [Serializable]
    public class MainMenuPanel : UIPanel
    {
        [SerializeField] Button startButton, leaderboardButton, howToPlayButton, quitButton, vibrationButton;
        [SerializeField] TMP_Text vibrationLabel, difficultyInfo, bestScoreText;
        [SerializeField] Button[] difficultyButtons = new Button[3];
        [SerializeField] Color selectedColor = new Color(1f, 0.2f, 0.8f);
        [SerializeField] Color unselectedColor = new Color(0.08f, 0.12f, 0.2f, 0.95f);

        protected override void OnInit()
        {
            startButton.onClick.AddListener(Game.StartGame);
            leaderboardButton.onClick.AddListener(Game.ShowLeaderboard);
            if (howToPlayButton) howToPlayButton.onClick.AddListener(Game.ShowInstructions);
            quitButton.onClick.AddListener(Quit);
            vibrationButton.onClick.AddListener(() =>
            {
                Settings.Vibration = !Settings.Vibration;
                Refresh();
            });
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                int index = i;
                difficultyButtons[i].onClick.AddListener(() => Game.SetDifficulty(index));
            }
            Game.DifficultyChanged += _ => Refresh();
        }

        protected override void OnShow() => Refresh();

        void Refresh()
        {
            for (int i = 0; i < difficultyButtons.Length; i++)
                difficultyButtons[i].image.color = i == Game.DifficultyIndex ? selectedColor : unselectedColor;
            var d = Game.Difficulty;
            difficultyInfo.text = $"{d.RoundLength:0}s round  ·  {d.PlayerHealth:0} HP  ·  up to {d.MaxEnemies} zombies";
            vibrationLabel.text = Settings.Vibration ? "VIBRATION: ON" : "VIBRATION: OFF";
            int best = Game.Leaderboard.Best;
            bestScoreText.text = best > 0 ? $"BEST  {best:N0}" : "";
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    [Serializable]
    public class InstructionsPanel : UIPanel
    {
        [SerializeField] Button backButton;

        protected override void OnInit() => backButton.onClick.AddListener(Game.ShowMenu);
    }

    [Serializable]
    public class LeaderboardPanel : UIPanel
    {
        [SerializeField] GameObject[] rows = new GameObject[5];
        [SerializeField] TMP_Text[] rankTexts = new TMP_Text[5];
        [SerializeField] TMP_Text[] scoreTexts = new TMP_Text[5];
        [SerializeField] TMP_Text[] detailTexts = new TMP_Text[5];
        [SerializeField] TMP_Text emptyText, clearLabel;
        [SerializeField] Button clearButton, backButton;

        float confirmTimer;

        protected override void OnInit()
        {
            backButton.onClick.AddListener(Game.ShowMenu);
            clearButton.onClick.AddListener(OnClear);
            Game.Leaderboard.Changed += Refresh;
        }

        protected override void OnShow()
        {
            confirmTimer = 0f;
            clearLabel.text = "CLEAR";
            Refresh();
        }

        protected override void OnTick(float dt)
        {
            if (confirmTimer > 0f && (confirmTimer -= dt) <= 0f) clearLabel.text = "CLEAR";
        }

        void OnClear()
        {
            if (confirmTimer > 0f)
            {
                Game.Leaderboard.Clear();
                confirmTimer = 0f;
                clearLabel.text = "CLEAR";
                return;
            }
            confirmTimer = 3f;
            clearLabel.text = "TAP AGAIN";
        }

        void Refresh()
        {
            var runs = Game.Leaderboard.Runs;
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].SetActive(i < runs.Count);
                if (i >= runs.Count) continue;
                var r = runs[i];
                rankTexts[i].text = "#" + (i + 1);
                string result = r.survived ? "<color=#33FFFF>SURVIVED</color>" : "<color=#FF4D80>OVERRUN</color>";
                scoreTexts[i].text = $"{r.score:N0}  <size=60%>{result}</size>";
                int secs = Mathf.FloorToInt(r.timeSurvived);
                string date = DateTime.TryParse(r.dateIso, null, DateTimeStyles.RoundtripKind, out var dt)
                    ? dt.ToLocalTime().ToString("dd MMM HH:mm")
                    : "-";
                detailTexts[i].text = $"{r.enemiesDefeated} kills  ·  {secs / 60}:{secs % 60:00}  ·  {r.difficulty}  ·  {date}";
            }
            emptyText.gameObject.SetActive(runs.Count == 0);
            clearButton.interactable = runs.Count > 0;
        }
    }

    [Serializable]
    public class ScanPanel : UIPanel
    {
        [SerializeField] TMP_Text instruction;
        [SerializeField] RectTransform phoneIcon;
        [SerializeField] Button backButton;
        float time;

        protected override void OnInit()
        {
            backButton.onClick.AddListener(Game.ShowMenu);
            Game.AR.CanPlaceChanged += SetText;
        }

        protected override void OnShow() => SetText(Game.AR.CanPlace);

        void SetText(bool canPlace)
        {
            instruction.text = canPlace ? "Aim at the floor and\ntap to place the beacon" : "Move your phone slowly\nto scan the floor";
            phoneIcon.gameObject.SetActive(!canPlace);
        }

        protected override void OnTick(float dt)
        {
            time += dt;
            phoneIcon.anchoredPosition = new Vector2(Mathf.Sin(time * 2f) * 90f, phoneIcon.anchoredPosition.y);
            phoneIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 2f) * -12f);
        }
    }

    [Serializable]
    public class SetupPanel : UIPanel
    {
        [SerializeField] TMP_Text countText;
        [SerializeField] Button startButton, clearButton, backButton;

        protected override void OnInit()
        {
            startButton.onClick.AddListener(Game.StartCountdown);
            clearButton.onClick.AddListener(Game.Cards.ClearSetupMirrors);
            backButton.onClick.AddListener(Game.ShowMenu);
            Game.Cards.SetupMirrorsChanged += SetCount;
        }

        protected override void OnShow() => SetCount(Game.Cards.SetupMirrorCount, Game.Cards.SetupMirrorLimit);

        void SetCount(int count, int max)
        {
            countText.text = $"MIRRORS  {count} / {max}";
            clearButton.interactable = count > 0;
        }
    }

    [Serializable]
    public class CountdownPanel : UIPanel
    {
        [SerializeField] TMP_Text numberText;
        float punch;

        protected override void OnInit() => Game.CountdownTick += n =>
        {
            numberText.text = n > 0 ? n.ToString() : "GO!";
            numberText.color = n > 0 ? new Color(0.2f, 1f, 1f) : new Color(1f, 0.2f, 0.8f);
            punch = 1f;
        };

        protected override void OnTick(float dt)
        {
            punch = Mathf.MoveTowards(punch, 0f, dt * 2.5f);
            numberText.rectTransform.localScale = Vector3.one * (1f + punch * punch * 0.6f);
        }
    }

    [Serializable]
    public class HUDPanel : UIPanel
    {
        static readonly string[] SpreadNames = { "", "SINGLE", "TWIN", "TRI", "QUAD" };
        static readonly Color Cyan = new Color(0.2f, 1f, 1f);
        static readonly Color Hot = new Color(1f, 0.3f, 0.2f);
        static readonly Color Warning = new Color(1f, 0.25f, 0.3f);

        [SerializeField] Image healthFill, heatFill;
        [SerializeField] TMP_Text healthText, scoreText, timeText, heatLabel, spreadText, wallsText, toastText, mirrorCount, prismCount;
        [SerializeField] Button pauseButton, mirrorButton, prismButton;
        [SerializeField] GameObject fireButton;
        [SerializeField] RectTransform fireVisual;
        [SerializeField] CanvasGroup damageVignette, deathOverlay;

        float vignette, toastTimer;
        int fingers;

        protected override void OnInit()
        {
            pauseButton.onClick.AddListener(Game.Pause);
            mirrorButton.onClick.AddListener(() => Game.Cards.PlaceGadget(Gadget.Mirror));
            prismButton.onClick.AddListener(() => Game.Cards.PlaceGadget(Gadget.Prism));

            var trigger = fireButton.GetComponent<EventTrigger>();
            if (!trigger) trigger = fireButton.AddComponent<EventTrigger>();
            OnPointer(trigger, EventTriggerType.PointerDown, () =>
            {
                fingers++;
                SetFiring(true);
            });
            OnPointer(trigger, EventTriggerType.PointerUp, () =>
            {
                fingers = Mathf.Max(0, fingers - 1);
                if (fingers == 0) SetFiring(false);
            });

            var p = Game.Player;
            p.HealthChanged += (current, max) =>
            {
                healthFill.fillAmount = max > 0f ? current / max : 0f;
                healthText.text = Mathf.CeilToInt(current).ToString();
            };
            p.Damaged += _ => vignette = 0.75f;
            p.SpreadChanged += level => spreadText.text = SpreadNames[Mathf.Clamp(level, 1, 4)];
            p.Heat.Changed += SetHeat;
            p.Heat.OverheatChanged += hot =>
            {
                heatLabel.text = hot ? "OVERHEAT!" : "HEAT";
                heatLabel.color = hot ? Hot : Cyan;
                SetHeat(p.Heat.Value);
            };
            Game.Score.Changed += s => scoreText.text = s.ToString("N0");
            Game.Timer.Changed += SetTime;
            Game.AR.WallsChanged += n => wallsText.text = "WALLS: " + n;
            Game.Cards.ChargesChanged += SetCharges;
            Game.Cards.Collected += card =>
            {
                toastText.text = card.Title;
                toastText.color = card.Color;
                toastTimer = 1.8f;
            };
        }

        static void OnPointer(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        void SetFiring(bool on)
        {
            fireVisual.localScale = Vector3.one * (on ? 0.9f : 1f);
            Game.Player.SetFiring(on);
        }

        protected override void OnShow()
        {
            var p = Game.Player;
            healthFill.fillAmount = p.HealthPercent;
            healthText.text = Mathf.CeilToInt(p.Health).ToString();
            scoreText.text = Game.Score.Points.ToString("N0");
            SetTime(Game.Timer.Remaining);
            SetHeat(p.Heat.Value);
            spreadText.text = SpreadNames[p.SpreadLevel];
            wallsText.text = "WALLS: " + Game.AR.WallCount;
            SetCharges(Game.Cards.MirrorCharges, Game.Cards.PrismCharges);
            vignette = 0f;
            damageVignette.alpha = deathOverlay.alpha = 0f;
            if (toastTimer <= 0f) toastText.text = "";
        }

        protected override void OnHide()
        {
            fingers = 0;
            SetFiring(false);
        }

        void SetTime(float remaining)
        {
            timeText.text = Clock(remaining);
            timeText.color = remaining <= 10f ? Warning : Color.white;
        }

        void SetHeat(float value)
        {
            heatFill.fillAmount = value;
            heatFill.color = Game.Player.Heat.Overheated ? Hot : Color.Lerp(Cyan, Hot, value * value);
        }

        void SetCharges(int mirrors, int prisms)
        {
            mirrorButton.gameObject.SetActive(mirrors > 0);
            prismButton.gameObject.SetActive(prisms > 0);
            mirrorCount.text = mirrors.ToString();
            prismCount.text = prisms.ToString();
        }

        protected override void OnTick(float dt)
        {
            var p = Game.Player;
            vignette = Mathf.MoveTowards(vignette, 0f, dt * 1.8f);
            float lowHealth = p.IsAlive && p.HealthPercent < 0.3f ? 0.25f + Mathf.Sin(Time.unscaledTime * 5f) * 0.1f : 0f;
            damageVignette.alpha = Mathf.Max(vignette, lowHealth);
            if (!p.IsAlive) deathOverlay.alpha = Mathf.MoveTowards(deathOverlay.alpha, 0.85f, dt * 1.2f);
            if (toastTimer > 0f)
            {
                toastTimer -= dt;
                toastText.alpha = Mathf.Clamp01(toastTimer * 2f);
            }
            if (p.SpreadTimeLeft > 0f)
                spreadText.text = $"{SpreadNames[p.SpreadLevel]} {Mathf.CeilToInt(p.SpreadTimeLeft)}s";
        }
    }

    [Serializable]
    public class PausePanel : UIPanel
    {
        [SerializeField] Button resumeButton, restartButton, menuButton;
        [SerializeField] TMP_Text poolStatsText;

        protected override void OnInit()
        {
            resumeButton.onClick.AddListener(Game.Resume);
            restartButton.onClick.AddListener(Game.Restart);
            menuButton.onClick.AddListener(Game.ShowMenu);
        }

        protected override void OnShow() => poolStatsText.text = Pools.Report();
    }

    [Serializable]
    public class EndPanel : UIPanel
    {
        [SerializeField] TMP_Text titleText, scoreText, killsText, breakdownText, timeText;
        [SerializeField] GameObject newBestTag;
        [SerializeField] Button restartButton, menuButton;

        protected override void OnInit()
        {
            restartButton.onClick.AddListener(Game.Restart);
            menuButton.onClick.AddListener(Game.ShowMenu);
        }

        protected override void OnShow()
        {
            var r = Game.LastRun;
            if (r == null) return;
            titleText.text = r.survived ? "SURVIVED" : "OVERRUN";
            titleText.color = r.survived ? new Color(0.2f, 1f, 1f) : new Color(1f, 0.25f, 0.45f);
            scoreText.text = r.score.ToString("N0");
            killsText.text = r.enemiesDefeated + " enemies defeated";
            breakdownText.text = $"Walkers {r.walkers}   ·   Spitters {r.spitters}   ·   Ghosts {r.ghosts}";
            timeText.text = "Time survived  " + Clock(r.timeSurvived);
            newBestTag.SetActive(Game.NewBest);
        }
    }
}
