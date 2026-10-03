using Ricochet.Core;
using UnityEngine;

namespace Ricochet.UI
{
    /// <summary>
    /// Abstract base for every screen (Abstraction + Inheritance). Gives all panels the same
    /// Show/Hide behaviour with a quick fade-in that uses unscaled time (works while paused).
    /// Derived panels hook into gameplay events in OnInitialize and refresh themselves in OnShow.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPanel : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float fadeSpeed = 6f;

        CanvasGroup _group;

        protected GameManager Game { get; private set; }
        public bool IsVisible => gameObject.activeSelf;

        public void Initialize(GameManager game)
        {
            Game = game;
            _group = GetComponent<CanvasGroup>();
            OnInitialize();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            if (_group) _group.alpha = 0f;
            OnShow();
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            OnHide();
            gameObject.SetActive(false);
        }

        protected virtual void OnInitialize() { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        void LateUpdate()
        {
            if (_group && _group.alpha < 1f)
                _group.alpha = Mathf.MoveTowards(_group.alpha, 1f, Time.unscaledDeltaTime * fadeSpeed);
        }

        protected static string FormatTime(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
