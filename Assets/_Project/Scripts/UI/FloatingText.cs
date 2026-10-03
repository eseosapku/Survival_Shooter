using System;
using Ricochet.Pooling;
using TMPro;
using UnityEngine;

namespace Ricochet.UI
{
    /// <summary>
    /// A pooled piece of screen text pinned to a world position ("+150", "x2 RICOCHET!").
    /// It rises and fades, then returns itself to the pool.
    /// </summary>
    public class FloatingText : MonoBehaviour, IPoolable
    {
        [SerializeField] TMP_Text label;
        [SerializeField, Min(0.1f)] float duration = 1.1f;
        [SerializeField] float riseMetres = 0.35f;

        Action<FloatingText> _release;
        RectTransform _rect;
        RectTransform _parent;
        Camera _camera;
        Vector3 _world;
        float _age;

        public void BindRelease(Action<FloatingText> release) => _release = release;

        void Awake() => _rect = (RectTransform)transform;

        public void OnSpawned()
        {
            _age = 0f;
            label.alpha = 1f;
            _rect.localScale = Vector3.one;
        }

        public void OnDespawned() { }

        public void Show(string text, Color color, float size, Vector3 worldPosition, Camera cam)
        {
            label.text = text;
            label.color = color;
            label.fontSize = size;
            _world = worldPosition;
            _camera = cam;
            _parent = (RectTransform)transform.parent;
            UpdatePosition();
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            float t = _age / duration;
            label.alpha = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
            _rect.localScale = Vector3.one * (1f + Mathf.Max(0f, 0.25f - t) * 2f);
            UpdatePosition();
            if (t >= 1f) _release?.Invoke(this);
        }

        void UpdatePosition()
        {
            if (!_camera) return;
            Vector3 world = _world + Vector3.up * (riseMetres * Mathf.Clamp01(_age / duration));
            Vector3 screen = _camera.WorldToScreenPoint(world);
            bool visible = screen.z > 0f;
            label.enabled = visible;
            if (!visible) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_parent, screen, null, out var local);
            _rect.anchoredPosition = local;
        }
    }
}
