using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet.UI
{
    /// <summary>Shows the latest 5 runs (newest first), with a two-tap Clear and a Back button.</summary>
    public class LeaderboardPanel : UIPanel
    {
        [SerializeField] LeaderboardRow[] rows = new LeaderboardRow[5];
        [SerializeField] TMP_Text emptyText;
        [SerializeField] Button clearButton;
        [SerializeField] TMP_Text clearLabel;
        [SerializeField] Button backButton;
        [SerializeField, Min(0.5f)] float confirmWindow = 3f;

        float _confirmTimer;

        protected override void OnInitialize()
        {
            backButton.onClick.AddListener(Game.BackToMenu);
            clearButton.onClick.AddListener(OnClear);
            Game.Leaderboard.Changed += Refresh;
        }

        protected override void OnShow()
        {
            _confirmTimer = 0f;
            clearLabel.text = "CLEAR";
            Refresh();
        }

        void Refresh()
        {
            var sessions = Game.Leaderboard.Sessions;
            for (int i = 0; i < rows.Length; i++)
            {
                if (i < sessions.Count) rows[i].Set(i + 1, sessions[i]);
                else rows[i].Clear();
            }
            emptyText.gameObject.SetActive(sessions.Count == 0);
            clearButton.interactable = sessions.Count > 0;
        }

        void OnClear()
        {
            if (_confirmTimer > 0f)
            {
                Game.Leaderboard.Clear();
                _confirmTimer = 0f;
                clearLabel.text = "CLEAR";
                return;
            }
            _confirmTimer = confirmWindow;
            clearLabel.text = "TAP AGAIN";
        }

        void Update()
        {
            if (_confirmTimer <= 0f) return;
            _confirmTimer -= Time.unscaledDeltaTime;
            if (_confirmTimer <= 0f) clearLabel.text = "CLEAR";
        }
    }
}
