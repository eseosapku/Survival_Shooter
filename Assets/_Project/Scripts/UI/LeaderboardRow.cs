using System;
using System.Globalization;
using Ricochet.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet.UI
{
    /// <summary>One line of the leaderboard.</summary>
    public class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] TMP_Text rankText;
        [SerializeField] TMP_Text scoreText;
        [SerializeField] TMP_Text detailText;
        [SerializeField] Image background;
        [SerializeField] Color survivedColor = new Color(0.2f, 1f, 1f);
        [SerializeField] Color overrunColor = new Color(1f, 0.3f, 0.5f);

        public void Set(int rank, SessionResult r)
        {
            gameObject.SetActive(true);
            rankText.text = $"#{rank}";
            string result = r.survived ? "SURVIVED" : "OVERRUN";
            scoreText.text = $"{r.score:N0}  <size=60%><color=#{ColorUtility.ToHtmlStringRGB(r.survived ? survivedColor : overrunColor)}>{result}</color></size>";

            int secs = Mathf.FloorToInt(r.timeSurvived);
            string date = DateTime.TryParse(r.dateIso, null, DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToLocalTime().ToString("dd MMM HH:mm")
                : "-";
            detailText.text = $"{r.enemiesDefeated} kills  ·  {secs / 60}:{secs % 60:00}  ·  {r.difficulty}  ·  {date}";
        }

        public void Clear() => gameObject.SetActive(false);
    }
}
