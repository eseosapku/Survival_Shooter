using Ricochet.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet.UI
{
    /// <summary>Round summary: SURVIVED / OVERRUN, final score, kills (with breakdown), time survived, "New best!".</summary>
    public class EndPanel : UIPanel
    {
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text killsText;
        [SerializeField] TMP_Text breakdownText;
        [SerializeField] TMP_Text timeText;
        [SerializeField] GameObject newBestTag;
        [SerializeField] Button restartButton;
        [SerializeField] Button menuButton;
        [SerializeField] Color survivedColor = new Color(0.2f, 1f, 1f);
        [SerializeField] Color overrunColor = new Color(1f, 0.25f, 0.45f);

        protected override void OnInitialize()
        {
            restartButton.onClick.AddListener(Game.Restart);
            menuButton.onClick.AddListener(Game.BackToMenu);
            Game.RoundEnded += (result, best) => { if (IsVisible) Fill(result, best); };
        }

        protected override void OnShow()
        {
            if (Game.LastResult != null) Fill(Game.LastResult, Game.LastWasNewBest);
        }

        void Fill(SessionResult r, bool newBest)
        {
            titleText.text = r.survived ? "SURVIVED" : "OVERRUN";
            titleText.color = r.survived ? survivedColor : overrunColor;
            scoreText.text = r.score.ToString("N0");
            killsText.text = $"{r.enemiesDefeated} enemies defeated";
            breakdownText.text = $"Walkers {r.walkers}   ·   Spitters {r.spitters}";
            timeText.text = $"Time survived  {FormatTime(r.timeSurvived)}";
            newBestTag.SetActive(newBest);
        }
    }
}
