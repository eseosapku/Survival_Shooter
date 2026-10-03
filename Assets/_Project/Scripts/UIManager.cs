using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ricochet
{
    /// <summary>
    /// Drives every screen. The panels themselves are already built in the scene (UI Canvas); this script only
    /// wires them up. It shows exactly one panel per game state by LISTENING to GameStateMachine.StateChanged
    /// (Observer), so gameplay code never knows the UI exists. Also: safe area, button click sounds and the
    /// pooled floating "+150 x2 RICOCHET!" texts.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [SerializeField] RectTransform safeArea;
        [SerializeField] RectTransform floatingRoot;
        [SerializeField] TextMeshProUGUI floatingPrefab;

        [SerializeField] MainMenuPanel mainMenu = new MainMenuPanel();
        [SerializeField] LeaderboardPanel leaderboard = new LeaderboardPanel();
        [SerializeField] ScanPanel scan = new ScanPanel();
        [SerializeField] CountdownPanel countdown = new CountdownPanel();
        [SerializeField] HUDPanel hud = new HUDPanel();
        [SerializeField] PausePanel pause = new PausePanel();
        [SerializeField] EndPanel end = new EndPanel();

        readonly Dictionary<GameStateId, UIPanel> _byState = new Dictionary<GameStateId, UIPanel>();
        GameManager _game;
        Rect _safeApplied;

        class Floating { public TextMeshProUGUI Text; public Vector3 World; public float Age; }
        readonly List<Floating> _floating = new List<Floating>();
        ObjectPool<TextMeshProUGUI> _floatPool;

        void Start()
        {
            _game = GameManager.Instance;
            _byState[GameStateId.MainMenu] = mainMenu;
            _byState[GameStateId.Leaderboard] = leaderboard;
            _byState[GameStateId.Scanning] = scan;
            _byState[GameStateId.Countdown] = countdown;
            _byState[GameStateId.Playing] = hud;
            _byState[GameStateId.Paused] = pause;
            _byState[GameStateId.GameOver] = end;

            foreach (var panel in _byState.Values)
            {
                panel.Initialize(_game);
                panel.Hide();
            }

            foreach (var button in GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(() => AudioManager.Instance?.Play(SoundId.UIClick));

            _floatPool = new ObjectPool<TextMeshProUGUI>(floatingPrefab, 12, floatingRoot);
            _game.Score.PointsAwarded += OnPointsAwarded;
            _game.StateMachine.StateChanged += OnStateChanged;
            if (_game.StateMachine.Current != null) ShowFor(_game.StateMachine.Current.Id);
        }

        void OnDestroy()
        {
            if (!_game) return;
            _game.StateMachine.StateChanged -= OnStateChanged;
            _game.Score.PointsAwarded -= OnPointsAwarded;
        }

        void OnStateChanged(IGameState previous, IGameState current) => ShowFor(current.Id);

        void ShowFor(GameStateId id)
        {
            _byState.TryGetValue(id, out var target);
            foreach (var panel in _byState.Values)
                if (panel != target) panel.Hide();
            if (target != null && !target.IsVisible) target.Show();
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var panel in _byState.Values)
                if (panel.IsVisible) panel.Tick(dt);
            ApplySafeArea();
            UpdateFloating(dt);
        }

        /// <summary>Keeps every panel inside Screen.safeArea (notches, rounded corners).</summary>
        void ApplySafeArea()
        {
            Rect safe = Screen.safeArea;
            if (safe == _safeApplied || Screen.width <= 0 || Screen.height <= 0) return;
            _safeApplied = safe;
            safeArea.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        // ---------- Floating score text (pooled) ----------

        void OnPointsAwarded(int points, float multiplier, Vector3 world)
        {
            SpawnFloating($"+{points}", new Color(0.3f, 1f, 1f), 64f, world);
            if (multiplier > 1f)
                SpawnFloating($"x{multiplier:0.#} RICOCHET!", new Color(1f, 0.25f, 0.85f), 56f, world + Vector3.up * 0.25f);
        }

        void SpawnFloating(string text, Color color, float size, Vector3 world)
        {
            var t = _floatPool.Get(Vector3.zero, Quaternion.identity);
            t.text = text;
            t.color = color;
            t.fontSize = size;
            _floating.Add(new Floating { Text = t, World = world });
        }

        void UpdateFloating(float dt)
        {
            var cam = _game.Player.Camera;
            for (int i = _floating.Count - 1; i >= 0; i--)
            {
                var f = _floating[i];
                f.Age += dt;
                float t = f.Age / 1.1f;
                Vector3 screen = cam.WorldToScreenPoint(f.World + Vector3.up * 0.35f * Mathf.Clamp01(t));
                f.Text.enabled = screen.z > 0f;
                if (screen.z > 0f)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(floatingRoot, screen, null, out var local);
                    f.Text.rectTransform.anchoredPosition = local;
                }
                f.Text.alpha = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                f.Text.rectTransform.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.25f - t) * 2f);
                if (t < 1f) continue;
                _floatPool.Release(f.Text);
                _floating.RemoveAt(i);
            }
        }
    }

    // =========================================================================
    // PANELS: abstract base + one subclass per screen (abstraction, inheritance, polymorphism)
    // =========================================================================

    /// <summary>
    /// Base for every screen: the same Show/Hide behaviour with a quick fade-in on unscaled time (works while paused).
    /// Subclasses hook into gameplay events in OnInitialize and refresh themselves in OnShow.
    /// </summary>
    [Serializable]
    public abstract class UIPanel
    {
        [SerializeField] protected GameObject root;

        CanvasGroup _group;
        protected GameManager Game { get; private set; }
        public bool IsVisible => root && root.activeSelf;

        public void Initialize(GameManager game)
        {
            Game = game;
            _group = root.GetComponent<CanvasGroup>();
            OnInitialize();
        }

        public void Show()
        {
            root.SetActive(true);
            if (_group) _group.alpha = 0f;
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
            if (_group && _group.alpha < 1f) _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, dt * 6f);
            OnTick(dt);
        }

        protected abstract void OnInitialize();
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnTick(float dt) { }

        protected static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }

    /// <summary>Title, Start, Leaderboard, difficulty selector (saved), vibration toggle, Quit.</summary>
    [Serializable]
    public class MainMenuPanel : UIPanel
    {
        [SerializeField] Button startButton, leaderboardButton, quitButton, vibrationButton;
        [SerializeField] TMP_Text vibrationLabel, difficultyInfo, bestScoreText;
        [SerializeField] Button[] difficultyButtons = new Button[3];
        [SerializeField] Color selectedColor = new Color(1f, 0.2f, 0.8f);
        [SerializeField] Color unselectedColor = new Color(0.08f, 0.12f, 0.2f, 0.95f);

        protected override void OnInitialize()
        {
            startButton.onClick.AddListener(Game.StartGame);
            leaderboardButton.onClick.AddListener(Game.OpenLeaderboard);
            quitButton.onClick.AddListener(Quit);
            vibrationButton.onClick.AddListener(() => { GameSettings.Vibration = !GameSettings.Vibration; Refresh(); });
            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                int index = i;
                difficultyButtons[i].onClick.AddListener(() => Game.SelectDifficulty(index));
            }
            Game.DifficultyChanged += _ => Refresh();
        }

        protected override void OnShow() => Refresh();

        void Refresh()
        {
            for (int i = 0; i < difficultyButtons.Length; i++)
                difficultyButtons[i].image.color = i == Game.DifficultyIndex ? selectedColor : unselectedColor;
            var d = Game.ActiveDifficulty;
            difficultyInfo.text = $"{d.RoundLength:0}s round  ·  {d.PlayerMaxHealth:0} HP  ·  up to {d.MaxEnemiesAlive} zombies";
            vibrationLabel.text = GameSettings.Vibration ? "VIBRATION: ON" : "VIBRATION: OFF";
            int best = Game.Leaderboard.BestScore;
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

    /// <summary>Latest 5 runs, newest first; two-tap Clear; Back.</summary>
    [Serializable]
    public class LeaderboardPanel : UIPanel
    {
        [SerializeField] GameObject[] rows = new GameObject[5];
        [SerializeField] TMP_Text[] rankTexts = new TMP_Text[5];
        [SerializeField] TMP_Text[] scoreTexts = new TMP_Text[5];
        [SerializeField] TMP_Text[] detailTexts = new TMP_Text[5];
        [SerializeField] TMP_Text emptyText, clearLabel;
        [SerializeField] Button clearButton, backButton;

        float _confirm;

        protected override void OnInitialize()
        {
            backButton.onClick.AddListener(Game.BackToMenu);
            clearButton.onClick.AddListener(OnClear);
            Game.Leaderboard.Changed += Refresh;
        }

        protected override void OnShow()
        {
            _confirm = 0f;
            clearLabel.text = "CLEAR";
            Refresh();
        }

        protected override void OnTick(float dt)
        {
            if (_confirm > 0f && (_confirm -= dt) <= 0f) clearLabel.text = "CLEAR";
        }

        void OnClear()
        {
            if (_confirm > 0f)
            {
                Game.Leaderboard.Clear();
                _confirm = 0f;
                clearLabel.text = "CLEAR";
                return;
            }
            _confirm = 3f;
            clearLabel.text = "TAP AGAIN";
        }

        void Refresh()
        {
            var sessions = Game.Leaderboard.Sessions;
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i].SetActive(i < sessions.Count);
                if (i >= sessions.Count) continue;
                var r = sessions[i];
                rankTexts[i].text = $"#{i + 1}";
                string result = r.survived ? "<color=#33FFFF>SURVIVED</color>" : "<color=#FF4D80>OVERRUN</color>";
                scoreTexts[i].text = $"{r.score:N0}  <size=60%>{result}</size>";
                int secs = Mathf.FloorToInt(r.timeSurvived);
                string date = DateTime.TryParse(r.dateIso, null, DateTimeStyles.RoundtripKind, out var dt)
                    ? dt.ToLocalTime().ToString("dd MMM HH:mm") : "-";
                detailTexts[i].text = $"{r.enemiesDefeated} kills  ·  {secs / 60}:{secs % 60:00}  ·  {r.difficulty}  ·  {date}";
            }
            emptyText.gameObject.SetActive(sessions.Count == 0);
            clearButton.interactable = sessions.Count > 0;
        }
    }

    /// <summary>Guides scanning: "move your phone" → "tap to place". No full-screen background, so taps reach the AR view.</summary>
    [Serializable]
    public class ScanPanel : UIPanel
    {
        [SerializeField] TMP_Text instruction;
        [SerializeField] RectTransform phoneIcon;
        [SerializeField] Button backButton;
        float _time;

        protected override void OnInitialize()
        {
            backButton.onClick.AddListener(Game.BackToMenu);
            Game.AR.ReticleValidChanged += SetValid;
        }

        protected override void OnShow() => SetValid(Game.AR.ReticleValid);

        void SetValid(bool valid)
        {
            instruction.text = valid ? "Aim at the floor and\ntap to place the beacon" : "Move your phone slowly\nto scan the floor";
            phoneIcon.gameObject.SetActive(!valid);
        }

        protected override void OnTick(float dt)
        {
            _time += dt;
            phoneIcon.anchoredPosition = new Vector2(Mathf.Sin(_time * 2f) * 90f, phoneIcon.anchoredPosition.y);
            phoneIcon.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_time * 2f) * -12f);
        }
    }

    /// <summary>Big 3-2-1-GO! with a punch animation.</summary>
    [Serializable]
    public class CountdownPanel : UIPanel
    {
        [SerializeField] TMP_Text numberText;
        float _punch;

        protected override void OnInitialize() => Game.CountdownTicked += n =>
        {
            numberText.text = n > 0 ? n.ToString() : "GO!";
            numberText.color = n > 0 ? new Color(0.2f, 1f, 1f) : new Color(1f, 0.2f, 0.8f);
            _punch = 1f;
        };

        protected override void OnTick(float dt)
        {
            _punch = Mathf.MoveTowards(_punch, 0f, dt * 2.5f);
            numberText.rectTransform.localScale = Vector3.one * (1f + _punch * _punch * 0.6f);
        }
    }

    /// <summary>In-game HUD. Purely an observer of gameplay events; its buttons send commands.</summary>
    [Serializable]
    public class HUDPanel : UIPanel
    {
        static readonly string[] TierNames = { "", "SINGLE", "TWIN", "TRI", "QUAD" };

        [SerializeField] Image healthFill, heatFill;
        [SerializeField] TMP_Text healthText, scoreText, timeText, heatLabel, spreadText, wallsText, toastText, mirrorCount, prismCount;
        [SerializeField] Button pauseButton, mirrorButton, prismButton;
        [SerializeField] GameObject fireButton;
        [SerializeField] RectTransform fireVisual;
        [SerializeField] CanvasGroup damageVignette, deathOverlay;

        static readonly Color Cyan = new Color(0.2f, 1f, 1f), Hot = new Color(1f, 0.3f, 0.2f), Warn = new Color(1f, 0.25f, 0.3f);
        float _vignette, _toast;
        int _pointers;

        protected override void OnInitialize()
        {
            pauseButton.onClick.AddListener(Game.Pause);
            mirrorButton.onClick.AddListener(() => Game.Cards.TryPlace(GadgetType.Mirror));
            prismButton.onClick.AddListener(() => Game.Cards.TryPlace(GadgetType.Prism));

            // Hold-to-fire: built-in EventTrigger reports finger down / up on the fire button.
            var trigger = fireButton.GetComponent<EventTrigger>();
            if (!trigger) trigger = fireButton.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, () => { _pointers++; SetFiring(true); });
            AddTrigger(trigger, EventTriggerType.PointerUp, () => { _pointers = Mathf.Max(0, _pointers - 1); if (_pointers == 0) SetFiring(false); });

            var p = Game.Player;
            p.HealthChanged += (cur, max) => { healthFill.fillAmount = max > 0f ? cur / max : 0f; healthText.text = Mathf.CeilToInt(cur).ToString(); };
            p.Damaged += _ => _vignette = 0.75f;
            p.SpreadTierChanged += t => spreadText.text = TierNames[Mathf.Clamp(t, 1, 4)];
            p.Heat.HeatChanged += OnHeat;
            p.Heat.OverheatChanged += hot => { heatLabel.text = hot ? "OVERHEAT!" : "HEAT"; heatLabel.color = hot ? Hot : Cyan; OnHeat(p.Heat.Heat01); };
            Game.Score.ScoreChanged += s => scoreText.text = s.ToString("N0");
            Game.Timer.TimeChanged += OnTime;
            Game.AR.WallCountChanged += n => wallsText.text = $"WALLS: {n}";
            Game.Cards.ChargesChanged += OnCharges;
            Game.Cards.CardCollected += card => { toastText.text = card.Title; toastText.color = card.Color; _toast = 1.8f; };
        }

        static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        void SetFiring(bool held)
        {
            fireVisual.localScale = Vector3.one * (held ? 0.9f : 1f);
            Game.Player.SetTriggerHeld(held);
        }

        protected override void OnShow()
        {
            var p = Game.Player;
            healthFill.fillAmount = p.Health01;
            healthText.text = Mathf.CeilToInt(p.Health).ToString();
            scoreText.text = Game.Score.Score.ToString("N0");
            OnTime(Game.Timer.Remaining);
            OnHeat(p.Heat.Heat01);
            spreadText.text = TierNames[p.SpreadTier];
            wallsText.text = $"WALLS: {Game.AR.WallCount}";
            OnCharges(Game.Cards.MirrorCharges, Game.Cards.PrismCharges);
            _vignette = 0f;
            damageVignette.alpha = deathOverlay.alpha = 0f;
            if (_toast <= 0f) toastText.text = "";
        }

        protected override void OnHide()
        {
            _pointers = 0;
            SetFiring(false);
        }

        void OnTime(float remaining)
        {
            timeText.text = FormatTime(remaining);
            timeText.color = remaining <= 10f ? Warn : Color.white; // red under 10 s
        }

        void OnHeat(float heat01)
        {
            heatFill.fillAmount = heat01;
            heatFill.color = Game.Player.Heat.IsOverheated ? Hot : Color.Lerp(Cyan, Hot, heat01 * heat01);
        }

        void OnCharges(int mirrors, int prisms)
        {
            mirrorButton.gameObject.SetActive(mirrors > 0); // gadget buttons appear once you have a charge
            prismButton.gameObject.SetActive(prisms > 0);
            mirrorCount.text = mirrors.ToString();
            prismCount.text = prisms.ToString();
        }

        protected override void OnTick(float dt)
        {
            var p = Game.Player;
            _vignette = Mathf.MoveTowards(_vignette, 0f, dt * 1.8f);
            float low = p.IsAlive && p.Health01 < 0.3f ? 0.25f + Mathf.Sin(Time.unscaledTime * 5f) * 0.1f : 0f;
            damageVignette.alpha = Mathf.Max(_vignette, low);
            if (!p.IsAlive) deathOverlay.alpha = Mathf.MoveTowards(deathOverlay.alpha, 0.85f, dt * 1.2f); // fade to red on death
            if (_toast > 0f) { _toast -= dt; toastText.alpha = Mathf.Clamp01(_toast * 2f); }
            if (p.SpreadBonusRemaining > 0f) spreadText.text = $"{TierNames[p.SpreadTier]} {Mathf.CeilToInt(p.SpreadBonusRemaining)}s";
        }
    }

    /// <summary>Resume / Restart / Main Menu + live pool statistics (proof that nothing is instantiated during play).</summary>
    [Serializable]
    public class PausePanel : UIPanel
    {
        [SerializeField] Button resumeButton, restartButton, menuButton;
        [SerializeField] TMP_Text poolStatsText;

        protected override void OnInitialize()
        {
            resumeButton.onClick.AddListener(Game.Resume);
            restartButton.onClick.AddListener(Game.Restart);
            menuButton.onClick.AddListener(Game.BackToMenu);
        }

        protected override void OnShow() => poolStatsText.text = PoolRegistry.BuildReport();
    }

    /// <summary>SURVIVED / OVERRUN, final score, kills with breakdown, time survived, "New best!", Restart, Main Menu.</summary>
    [Serializable]
    public class EndPanel : UIPanel
    {
        [SerializeField] TMP_Text titleText, scoreText, killsText, breakdownText, timeText;
        [SerializeField] GameObject newBestTag;
        [SerializeField] Button restartButton, menuButton;

        protected override void OnInitialize()
        {
            restartButton.onClick.AddListener(Game.Restart);
            menuButton.onClick.AddListener(Game.BackToMenu);
        }

        protected override void OnShow()
        {
            var r = Game.LastResult;
            if (r == null) return;
            titleText.text = r.survived ? "SURVIVED" : "OVERRUN";
            titleText.color = r.survived ? new Color(0.2f, 1f, 1f) : new Color(1f, 0.25f, 0.45f);
            scoreText.text = r.score.ToString("N0");
            killsText.text = $"{r.enemiesDefeated} enemies defeated";
            breakdownText.text = $"Walkers {r.walkers}   ·   Spitters {r.spitters}";
            timeText.text = $"Time survived  {FormatTime(r.timeSurvived)}";
            newBestTag.SetActive(Game.LastWasNewBest);
        }
    }
}
