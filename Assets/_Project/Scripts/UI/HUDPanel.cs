using Ricochet.AR;
using Ricochet.Cards;
using Ricochet.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet.UI
{
    /// <summary>
    /// In-game HUD. Purely an observer: it subscribes to gameplay events (health, score, timer, heat,
    /// spread tier, walls, charges, cards) and updates widgets. Its buttons send commands to the game.
    /// </summary>
    public class HUDPanel : UIPanel
    {
        static readonly string[] TierNames = { "", "SINGLE", "TWIN", "TRI", "QUAD" };

        [Header("Top bar")]
        [SerializeField] Image healthFill;
        [SerializeField] TMP_Text healthText;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text timeText;
        [SerializeField] Button pauseButton;

        [Header("Weapon")]
        [SerializeField] FireButton fireButton;
        [SerializeField] Image heatFill;
        [SerializeField] TMP_Text heatLabel;
        [SerializeField] TMP_Text spreadText;

        [Header("Gadgets")]
        [SerializeField] Button mirrorButton;
        [SerializeField] TMP_Text mirrorCount;
        [SerializeField] Button prismButton;
        [SerializeField] TMP_Text prismCount;

        [Header("Info & feedback")]
        [SerializeField] TMP_Text wallsText;
        [SerializeField] TMP_Text toastText;
        [SerializeField] CanvasGroup damageVignette;
        [SerializeField] CanvasGroup deathOverlay;

        [Header("Colours")]
        [SerializeField] Color timeColor = Color.white;
        [SerializeField] Color timeWarningColor = new Color(1f, 0.25f, 0.3f);
        [SerializeField] Color heatColor = new Color(0.2f, 1f, 1f);
        [SerializeField] Color heatHotColor = new Color(1f, 0.3f, 0.2f);

        float _vignette;
        float _toastTimer;
        bool _overheated;

        protected override void OnInitialize()
        {
            pauseButton.onClick.AddListener(Game.Pause);
            mirrorButton.onClick.AddListener(() => Game.AbilityPlacer.TryPlaceMirror());
            prismButton.onClick.AddListener(() => Game.AbilityPlacer.TryPlacePrism());
            fireButton.HeldChanged += held => Game.Blaster.SetTriggerHeld(held);

            Game.PlayerHealth.HealthChanged += OnHealthChanged;
            Game.PlayerHealth.Damaged += _ => _vignette = 0.75f;
            Game.Score.ScoreChanged += OnScoreChanged;
            Game.Timer.TimeChanged += OnTimeChanged;
            Game.Blaster.Heat.HeatChanged += OnHeatChanged;
            Game.Blaster.Heat.OverheatChanged += OnOverheat;
            Game.Blaster.SpreadTierChanged += OnSpreadChanged;
            Game.AbilityPlacer.ChargesChanged += OnChargesChanged;
            Game.Cards.CardCollected += OnCardCollected;
            ARPlaneStyler.WallCountChanged += OnWallsChanged;
        }

        void OnDestroy() => ARPlaneStyler.WallCountChanged -= OnWallsChanged;

        protected override void OnShow()
        {
            OnHealthChanged(Game.PlayerHealth.Current, Game.PlayerHealth.Max);
            OnScoreChanged(Game.Score.Score);
            OnTimeChanged(Game.Timer.Remaining);
            OnHeatChanged(Game.Blaster.Heat.Heat01);
            OnOverheat(Game.Blaster.Heat.IsOverheated);
            OnSpreadChanged(Game.Blaster.SpreadTier);
            OnChargesChanged(Game.AbilityPlacer.MirrorCharges, Game.AbilityPlacer.PrismCharges);
            OnWallsChanged(ARPlaneStyler.WallCount);
            _vignette = 0f;
            damageVignette.alpha = 0f;
            deathOverlay.alpha = 0f;
            if (_toastTimer <= 0f) toastText.text = "";
        }

        protected override void OnHide()
        {
            fireButton.ForceRelease();
            Game.Blaster.SetTriggerHeld(false);
        }

        void OnHealthChanged(float current, float max)
        {
            healthFill.fillAmount = max > 0f ? current / max : 0f;
            healthText.text = Mathf.CeilToInt(current).ToString();
        }

        void OnScoreChanged(int score) => scoreText.text = score.ToString("N0");

        void OnTimeChanged(float remaining)
        {
            timeText.text = FormatTime(remaining);
            timeText.color = remaining <= 10f ? timeWarningColor : timeColor;
        }

        void OnHeatChanged(float heat01)
        {
            heatFill.fillAmount = heat01;
            heatFill.color = _overheated ? heatHotColor : Color.Lerp(heatColor, heatHotColor, heat01 * heat01);
        }

        void OnOverheat(bool hot)
        {
            _overheated = hot;
            heatLabel.text = hot ? "OVERHEAT!" : "HEAT";
            heatLabel.color = hot ? heatHotColor : heatColor;
        }

        void OnSpreadChanged(int tier) => spreadText.text = TierNames[Mathf.Clamp(tier, 1, 4)];

        void OnChargesChanged(int mirrors, int prisms)
        {
            // Gadget buttons only appear once the player has collected a charge.
            mirrorButton.gameObject.SetActive(mirrors > 0);
            prismButton.gameObject.SetActive(prisms > 0);
            mirrorCount.text = mirrors.ToString();
            prismCount.text = prisms.ToString();
        }

        void OnWallsChanged(int walls) => wallsText.text = $"WALLS: {walls}";

        void OnCardCollected(AbilityCard card)
        {
            toastText.text = card.Title;
            toastText.color = card.Color;
            _toastTimer = 1.8f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Red vignette: flash on hit, gentle pulse when health is low.
            _vignette = Mathf.MoveTowards(_vignette, 0f, dt * 1.8f);
            float lowHealth = Game.PlayerHealth.Normalized < 0.3f && Game.PlayerHealth.IsAlive
                ? 0.25f + Mathf.Sin(Time.unscaledTime * 5f) * 0.1f
                : 0f;
            damageVignette.alpha = Mathf.Max(_vignette, lowHealth);

            // Death: screen fades to red during the slow-motion beat.
            if (!Game.PlayerHealth.IsAlive)
                deathOverlay.alpha = Mathf.MoveTowards(deathOverlay.alpha, 0.85f, dt * 1.2f);

            if (_toastTimer > 0f)
            {
                _toastTimer -= dt;
                toastText.alpha = Mathf.Clamp01(_toastTimer * 2f);
            }

            if (Game.Blaster.SpreadBonusRemaining > 0f)
                spreadText.text = $"{TierNames[Game.Blaster.SpreadTier]} {Mathf.CeilToInt(Game.Blaster.SpreadBonusRemaining)}s";
        }
    }
}
