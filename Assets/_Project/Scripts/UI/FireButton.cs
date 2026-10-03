using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Ricochet.UI
{
    /// <summary>Hold-to-fire button. Reports pressed/released; the HUD forwards that to the LaserBlaster.</summary>
    public class FireButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] RectTransform visual;
        [SerializeField, Range(0.5f, 1f)] float pressedScale = 0.9f;

        int _pointers;

        public bool IsHeld => _pointers > 0;
        public event Action<bool> HeldChanged;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointers++;
            if (_pointers == 1) SetHeld(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_pointers == 0) return;
            _pointers--;
            if (_pointers == 0) SetHeld(false);
        }

        /// <summary>Called when the HUD hides so the gun can't keep firing.</summary>
        public void ForceRelease()
        {
            if (_pointers == 0) return;
            _pointers = 0;
            SetHeld(false);
        }

        void OnDisable() => ForceRelease();

        void SetHeld(bool held)
        {
            if (visual) visual.localScale = Vector3.one * (held ? pressedScale : 1f);
            HeldChanged?.Invoke(held);
        }
    }
}
