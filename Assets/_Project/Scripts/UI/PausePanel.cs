using Ricochet.Pooling;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet.UI
{
    /// <summary>Resume / Restart / Main Menu, plus live pool statistics (proof that nothing is instantiated during play).</summary>
    public class PausePanel : UIPanel
    {
        [SerializeField] Button resumeButton;
        [SerializeField] Button restartButton;
        [SerializeField] Button menuButton;
        [SerializeField] TMP_Text poolStatsText;

        protected override void OnInitialize()
        {
            resumeButton.onClick.AddListener(Game.Resume);
            restartButton.onClick.AddListener(Game.Restart);
            menuButton.onClick.AddListener(Game.BackToMenu);
        }

        protected override void OnShow()
        {
            if (poolStatsText) poolStatsText.text = PoolRegistry.BuildReport();
        }
    }
}
