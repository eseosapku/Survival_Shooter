using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Ricochet.Data
{
    /// <summary>
    /// Saves the latest 5 sessions (newest first) as JSON in Application.persistentDataPath,
    /// so they survive closing the app. The best score ever is stored separately in PlayerPrefs.
    /// </summary>
    public class LeaderboardService
    {
        public const int MaxEntries = 5;
        const string FileName = "leaderboard.json";
        const string BestScoreKey = "Ricochet.BestScore";

        readonly string _path;
        SessionResultList _data = new SessionResultList();

        public event Action Changed;

        public IReadOnlyList<SessionResult> Sessions => _data.sessions;
        public int BestScore => PlayerPrefs.GetInt(BestScoreKey, 0);

        public LeaderboardService()
        {
            _path = Path.Combine(Application.persistentDataPath, FileName);
            Load();
        }

        public void Load()
        {
            _data = new SessionResultList();
            try
            {
                if (File.Exists(_path))
                {
                    var loaded = JsonUtility.FromJson<SessionResultList>(File.ReadAllText(_path));
                    if (loaded != null && loaded.sessions != null)
                        _data = loaded;
                }
            }
            catch (Exception e)
            {
                // A corrupt file must never crash the game: start with an empty board instead.
                Debug.LogWarning($"[Leaderboard] Could not read {_path}, starting empty. {e.Message}");
                _data = new SessionResultList();
            }
            Trim();
        }

        /// <summary>Adds a run at the front and keeps only the latest 5. Returns true if it is a new best score.</summary>
        public bool Add(SessionResult result)
        {
            _data.sessions.Insert(0, result);
            Trim();

            bool newBest = result.score > BestScore;
            if (newBest)
            {
                PlayerPrefs.SetInt(BestScoreKey, result.score);
                PlayerPrefs.Save();
            }

            Save();
            Changed?.Invoke();
            return newBest;
        }

        public void Clear()
        {
            _data.sessions.Clear();
            Save();
            Changed?.Invoke();
        }

        void Trim()
        {
            if (_data.sessions.Count > MaxEntries)
                _data.sessions.RemoveRange(MaxEntries, _data.sessions.Count - MaxEntries);
        }

        void Save()
        {
            try
            {
                File.WriteAllText(_path, JsonUtility.ToJson(_data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboard] Could not save: {e.Message}");
            }
        }
    }
}
