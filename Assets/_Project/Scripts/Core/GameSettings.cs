using UnityEngine;

namespace Ricochet.Core
{
    /// <summary>Small player preferences stored in PlayerPrefs.</summary>
    public static class GameSettings
    {
        const string VibrationKey = "Ricochet.Vibration";

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }
    }
}
