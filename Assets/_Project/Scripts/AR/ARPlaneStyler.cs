using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Ricochet.AR
{
    /// <summary>
    /// Custom plane tracker visual. Lives on the plane prefab (P_RicochetPlane) next to ARPlane.
    /// - Floors (HorizontalUp): name texture, layer ARFloor.
    /// - Walls (Vertical): cyan grid, layer ReflectiveWall; their MeshCollider is what lasers bounce off.
    /// - Anything else (ceilings, tilted): hidden and not collidable.
    /// Only appears once ARCore actually detects a plane, because AR Foundation creates this prefab per plane.
    /// </summary>
    [RequireComponent(typeof(ARPlane))]
    public class ARPlaneStyler : MonoBehaviour
    {
        public enum Kind { Unknown, Floor, Wall, Ignored }

        static readonly List<ARPlaneStyler> s_All = new List<ARPlaneStyler>();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>Raised when the number of detected walls changes (HUD shows "Walls: N").</summary>
        public static event Action<int> WallCountChanged;

        public static IReadOnlyList<ARPlaneStyler> All => s_All;
        public static int WallCount { get; private set; }
        static bool s_FloorsDimmed;

        [SerializeField] Material floorMaterial;
        [SerializeField] Material wallMaterial;
        [SerializeField] Color floorLineColor = new Color(0.2f, 1f, 1f, 0.9f);
        [SerializeField] Color wallLineColor = new Color(1f, 0.25f, 0.9f, 0.9f);
        [SerializeField, Range(0f, 1f)] float dimmedAlpha = 0.25f;
        [SerializeField] string floorLayer = "ARFloor";
        [SerializeField] string wallLayer = "ReflectiveWall";

        ARPlane _plane;
        MeshRenderer _renderer;
        LineRenderer _line;
        MeshCollider _collider;
        MaterialPropertyBlock _block;
        Color _baseColor = Color.white;
        bool _dimmed;

        public Kind PlaneKind { get; private set; } = Kind.Unknown;
        public ARPlane Plane => _plane;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_All.Clear();
            WallCount = 0;
            s_FloorsDimmed = false;
            WallCountChanged = null;
        }

        void Awake()
        {
            _plane = GetComponent<ARPlane>();
            _renderer = GetComponent<MeshRenderer>();
            _line = GetComponent<LineRenderer>();
            _collider = GetComponent<MeshCollider>();
            _block = new MaterialPropertyBlock();
        }

        void OnEnable()
        {
            s_All.Add(this);
            _plane.boundaryChanged += OnBoundaryChanged;
            Classify();
        }

        void OnDisable()
        {
            s_All.Remove(this);
            _plane.boundaryChanged -= OnBoundaryChanged;
            RecountWalls();
        }

        // Alignment can be refined by ARCore after creation, so re-check whenever the plane updates.
        void OnBoundaryChanged(ARPlaneBoundaryChangedEventArgs _) => Classify();

        void Classify()
        {
            Kind kind;
            switch (_plane.alignment)
            {
                case PlaneAlignment.HorizontalUp: kind = Kind.Floor; break;
                case PlaneAlignment.Vertical: kind = Kind.Wall; break;
                default: kind = Kind.Ignored; break;
            }
            if (kind == PlaneKind) return;
            PlaneKind = kind;
            ApplyStyle();
            RecountWalls();
        }

        void ApplyStyle()
        {
            bool visible = PlaneKind == Kind.Floor || PlaneKind == Kind.Wall;

            // ARPlaneMeshVisualizer toggles renderer.enabled every frame, so hide via forceRenderingOff instead.
            if (_renderer) _renderer.forceRenderingOff = !visible;
            if (_line) _line.forceRenderingOff = !visible;
            if (_collider) _collider.enabled = visible;
            if (!visible) return;

            bool floor = PlaneKind == Kind.Floor;
            int layer = LayerMask.NameToLayer(floor ? floorLayer : wallLayer);
            if (layer >= 0) gameObject.layer = layer;

            var mat = floor ? floorMaterial : wallMaterial;
            if (_renderer && mat)
            {
                _renderer.sharedMaterial = mat;
                _baseColor = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId) : Color.white;
            }
            if (_line)
            {
                var c = floor ? floorLineColor : wallLineColor;
                _line.startColor = c;
                _line.endColor = c;
            }
            _dimmed = floor && s_FloorsDimmed;
            ApplyDim();
        }

        /// <summary>Fades the visual (used for floors after the beacon is placed). Colliders stay active.</summary>
        public void SetDimmed(bool dimmed)
        {
            _dimmed = dimmed;
            ApplyDim();
        }

        void ApplyDim()
        {
            if (!_renderer) return;
            var c = _baseColor;
            if (_dimmed) c.a *= dimmedAlpha;
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            _renderer.SetPropertyBlock(_block);
            if (_line) _line.widthMultiplier = _dimmed ? 0.4f : 1f;
        }

        static void RecountWalls()
        {
            int count = 0;
            foreach (var s in s_All)
                if (s.PlaneKind == Kind.Wall && s._plane.subsumedBy == null)
                    count++;
            if (count == WallCount) return;
            WallCount = count;
            WallCountChanged?.Invoke(count);
        }

        /// <summary>Dims or restores every detected floor plane.</summary>
        public static void SetFloorsDimmed(bool dimmed)
        {
            s_FloorsDimmed = dimmed;
            foreach (var s in s_All)
                if (s.PlaneKind == Kind.Floor)
                    s.SetDimmed(dimmed);
        }
    }
}
