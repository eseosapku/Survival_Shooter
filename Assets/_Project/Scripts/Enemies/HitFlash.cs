using UnityEngine;

namespace Ricochet.Enemies
{
    /// <summary>
    /// Flashes every renderer of an enemy white when hit, and can tint it (blue = frozen).
    /// Uses a MaterialPropertyBlock so no material copies are created (cheap, and pooled enemies stay clean).
    /// Works with any URP Lit/Unlit material, so it keeps working after real models are swapped in.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(0.01f)] float flashDuration = 0.12f;
        [SerializeField] Color flashColor = new Color(4f, 4f, 4f, 1f);

        Renderer[] _renderers;
        Color[][] _baseColors;
        MaterialPropertyBlock _block;
        float _flashTimer;
        Color _tint = Color.white;
        bool _dirty;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColors = new Color[_renderers.Length][];
            for (int r = 0; r < _renderers.Length; r++)
            {
                var mats = _renderers[r].sharedMaterials;
                _baseColors[r] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                    _baseColors[r][m] = mats[m] && mats[m].HasProperty(BaseColorId) ? mats[m].GetColor(BaseColorId) : Color.white;
            }
        }

        public void Flash()
        {
            _flashTimer = flashDuration;
            _dirty = true;
        }

        public void SetTint(Color tint)
        {
            _tint = tint;
            _dirty = true;
        }

        public void ResetVisuals()
        {
            _flashTimer = 0f;
            _tint = Color.white;
            _dirty = true;
            Apply(0f);
        }

        void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer -= Time.deltaTime;
                Apply(Mathf.Clamp01(_flashTimer / flashDuration));
            }
            else if (_dirty)
            {
                Apply(0f);
                _dirty = false;
            }
        }

        void Apply(float flash01)
        {
            if (_renderers == null) return;
            for (int r = 0; r < _renderers.Length; r++)
            {
                var rend = _renderers[r];
                if (!rend) continue;
                for (int m = 0; m < _baseColors[r].Length; m++)
                {
                    Color c = _baseColors[r][m] * _tint;
                    c.a = _baseColors[r][m].a;
                    rend.GetPropertyBlock(_block, m);
                    _block.SetColor(BaseColorId, Color.Lerp(c, flashColor, flash01));
                    rend.SetPropertyBlock(_block, m);
                }
            }
        }
    }
}
