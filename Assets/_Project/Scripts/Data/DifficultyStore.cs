using System;
using UnityEngine;

namespace Ricochet.Data
{
    /// <summary>Holds the three difficulty assets and remembers the player's choice in PlayerPrefs.</summary>
    [Serializable]
    public class DifficultyStore
    {
        const string Key = "Ricochet.Difficulty";

        [SerializeField] DifficultySettings[] options = new DifficultySettings[3];

        public event Action<int> SelectionChanged;

        public int Count => options.Length;
        public int SelectedIndex { get; private set; } = 1;
        public DifficultySettings Selected => options[SelectedIndex];

        public DifficultySettings Get(int index) => options[index];

        public void Load()
        {
            SelectedIndex = Mathf.Clamp(PlayerPrefs.GetInt(Key, 1), 0, options.Length - 1);
        }

        public void Select(int index)
        {
            index = Mathf.Clamp(index, 0, options.Length - 1);
            if (index == SelectedIndex) return;
            SelectedIndex = index;
            PlayerPrefs.SetInt(Key, index);
            PlayerPrefs.Save();
            SelectionChanged?.Invoke(index);
        }
    }
}
