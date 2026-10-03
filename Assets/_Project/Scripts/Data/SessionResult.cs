using System;
using System.Collections.Generic;

namespace Ricochet.Data
{
    /// <summary>One finished run. Serializable so JsonUtility can save it.</summary>
    [Serializable]
    public class SessionResult
    {
        public int score;
        public int enemiesDefeated;
        public int walkers;
        public int spitters;
        public float timeSurvived;
        public string difficulty;
        public bool survived;
        public string dateIso;
    }

    /// <summary>JsonUtility cannot serialize a bare list, so it is wrapped in a class.</summary>
    [Serializable]
    public class SessionResultList
    {
        public List<SessionResult> sessions = new List<SessionResult>();
    }
}
