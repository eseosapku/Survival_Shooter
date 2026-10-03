using Ricochet.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet.UI
{
    /// <summary>Title screen: Start, Leaderboard, difficulty selector (saved), vibration toggle, Quit.</summary>
    public class MainMenuPanel : UIPanel
    {
        [SerializeField] Button startButton;
        [SerializeField] Button leaderboardButton;
        [SerializeField] Button quitButton;
        [SerializeField] Button vibrationButton;
        [SerializeField] TMP_Text vibrationLabel;
        [SerializeField] Button[] difficultyButtons = new Button[3];
        [SerializeField] TMP_Text difficultyInfo;
        [SerializeField] TMP_Text bestScoreText;
        [SerializeField] Color selectedColor = new Color(1f, 0.2f, 0.8f, 1f);
        [SerializeField] Color unselectedColor = new Color(0.08f, 0.12f, 0.2f, 0.9f);

        protected override void OnInitialize()
        {
            startButton.onClick.AddListener(Game.StartGame);
            leaderboardButton.onClick.AddListener(Game.OpenLeaderboard);
            quitButton.onClick.AddListener(Quit);
            vibrationButton.onClick.AddListener(ToggleVibration);

            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                int index = i;
                difficultyButtons[i].onClick.AddListener(() => Game.SelectDifficulty(index));
            }
            Game.Difficulties.SelectionChanged += _ => RefreshDifficulty();
        }

        protected override void OnShow()
        {
            RefreshDifficulty();
            RefreshVibration();
            int best = Game.Leaderboard.BestScore;
            bestScoreText.text = best > 0 ? $"BEST  {best:N0}" : "";
        }

        void RefreshDifficulty()
        {
            int selected = Game.Difficulties.SelectedIndex;
            for (int i = 0; i < difficultyButtons.Length; i++)
                difficultyButtons[i].image.color = i == selected ? selectedColor : unselectedColor;

            var d = Game.Difficulties.Selected;
            difficultyInfo.text = $"{d.RoundLength:0}s round  ·  {d.PlayerMaxHealth:0} HP  ·  up to {d.MaxEnemiesAlive} zombies";
        }

        void ToggleVibration()
        {
            GameSettings.Vibration = !GameSettings.Vibration;
            RefreshVibration();
        }

        void RefreshVibration() => vibrationLabel.text = GameSettings.Vibration ? "VIBRATION: ON" : "VIBRATION: OFF";

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
