using System.Collections.Generic;
using Ricochet.Audio;
using Ricochet.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ricochet.UI
{
    /// <summary>
    /// Shows exactly one main panel per game state. It only LISTENS to the state machine (Observer),
    /// so gameplay code never needs to know the UI exists. Also adds a click sound to every button.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        [SerializeField] MainMenuPanel mainMenu;
        [SerializeField] LeaderboardPanel leaderboard;
        [SerializeField] ScanPanel scan;
        [SerializeField] CountdownPanel countdown;
        [SerializeField] HUDPanel hud;
        [SerializeField] PausePanel pause;
        [SerializeField] EndPanel end;
        [SerializeField] FloatingTextPool floatingText;

        readonly Dictionary<GameStateId, UIPanel> _byState = new Dictionary<GameStateId, UIPanel>();
        readonly List<UIPanel> _all = new List<UIPanel>();
        GameManager _game;

        void Start()
        {
            _game = GameManager.Instance;

            _byState[GameStateId.MainMenu] = mainMenu;
            _byState[GameStateId.Leaderboard] = leaderboard;
            _byState[GameStateId.Scanning] = scan;
            _byState[GameStateId.Countdown] = countdown;
            _byState[GameStateId.Playing] = hud;
            _byState[GameStateId.Paused] = pause;
            _byState[GameStateId.GameOver] = end;

            foreach (var panel in _byState.Values)
            {
                _all.Add(panel);
                panel.Initialize(_game);
                panel.Hide();
            }
            if (floatingText) floatingText.Initialize(_game);

            foreach (var button in GetComponentsInChildren<Button>(true))
                button.onClick.AddListener(PlayClick);

            _game.StateMachine.StateChanged += OnStateChanged;
            if (_game.StateMachine.Current != null) ShowFor(_game.StateMachine.Current.Id);
        }

        void OnDestroy()
        {
            if (_game) _game.StateMachine.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(IGameState previous, IGameState current) => ShowFor(current.Id);

        void ShowFor(GameStateId id)
        {
            _byState.TryGetValue(id, out var target);
            foreach (var panel in _all)
                if (panel != target) panel.Hide();
            if (target && !target.IsVisible) target.Show();
        }

        static void PlayClick() => AudioManager.Instance?.Play(SoundId.UIClick);
    }
}
