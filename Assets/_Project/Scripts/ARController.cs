using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Ricochet
{
    /// <summary>
    /// Everything AR in one place:
    /// 1. CUSTOM PLANE TRACKER: styles every plane ARCore detects (it listens to ARPlaneManager.trackablesChanged).
    ///    Floors get the texture with the player's full name (layer ARFloor); walls get a neon grid
    ///    (layer ReflectiveWall, their MeshCollider is what lasers bounce off). Ceilings/tilted planes are hidden.
    /// 2. TAP-TO-PLACE: a reticle follows the floor at the screen centre; the first tap that isn't on UI places the
    ///    beacon, anchored to the plane. Only ONE arena can ever exist. Afterwards new floors stop being detected.
    /// </summary>
    public class ARController : MonoBehaviour
    {
        [Header("AR Foundation")]
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARAnchorManager anchorManager;
        [SerializeField] Camera arCamera;

        [Header("Custom plane visuals")]
        [SerializeField] Material floorMaterial;
        [SerializeField] Material wallMaterial;
        [SerializeField] Color floorLineColor = new Color(0.2f, 1f, 1f, 0.9f);
        [SerializeField] Color wallLineColor = new Color(1f, 0.25f, 0.9f, 0.9f);
        [SerializeField, Range(0f, 1f)] float dimmedAlpha = 0.25f;

        [Header("Placement")]
        [SerializeField] Transform reticle;
        [SerializeField] GameObject arenaPrefab;
        [SerializeField, Min(0f)] float minPlaneSize = 0.6f;
        [SerializeField, Min(0.5f)] float fallbackRadius = 4f;

        static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock _block;
        int _floorLayer, _wallLayer;
        bool _placementEnabled;
        bool _reticleValid;
        Pose _reticlePose;
        ARPlane _reticlePlane;
        Transform _spinner;

        public Arena Arena { get; private set; }
        public bool HasArena => Arena != null;
        public bool ReticleValid => _reticleValid;
        public int WallCount { get; private set; }

        public event Action<Arena> ArenaPlaced;
        public event Action<bool> ReticleValidChanged;
        public event Action<int> WallCountChanged;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            _floorLayer = LayerMask.NameToLayer("ARFloor");
            _wallLayer = LayerMask.NameToLayer("ReflectiveWall");
            if (reticle) reticle.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            TouchSimulation.Enable(); // the mouse acts as a finger in the editor
#endif
            planeManager.trackablesChanged.AddListener(OnPlanesChanged);
        }

        void OnDisable() => planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);

        // ---------------- Custom plane tracker ----------------

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            foreach (var plane in args.added) StylePlane(plane);
            foreach (var plane in args.updated) StylePlane(plane); // ARCore can refine alignment later
            RecountWalls();
        }

        void StylePlane(ARPlane plane)
        {
            bool floor = plane.alignment == PlaneAlignment.HorizontalUp;
            bool wall = plane.alignment == PlaneAlignment.Vertical;
            var mr = plane.GetComponent<MeshRenderer>();
            var line = plane.GetComponent<LineRenderer>();
            var col = plane.GetComponent<MeshCollider>();

            // ARPlaneMeshVisualizer toggles renderer.enabled every frame, so hide with forceRenderingOff.
            bool visible = floor || wall;
            if (mr) mr.forceRenderingOff = !visible;
            if (line) line.forceRenderingOff = !visible;
            if (col) col.enabled = visible;
            if (!visible) return;

            plane.gameObject.layer = floor ? _floorLayer : _wallLayer;
            var mat = floor ? floorMaterial : wallMaterial;
            if (mr && mr.sharedMaterial != mat) mr.sharedMaterial = mat;
            if (line)
            {
                line.startColor = line.endColor = floor ? floorLineColor : wallLineColor;
            }

            // After placement the floor is dimmed so the action stands out (colliders stay active).
            if (mr)
            {
                Color c = mat.GetColor(BaseColorId);
                if (floor && HasArena) c.a *= dimmedAlpha;
                mr.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, c);
                mr.SetPropertyBlock(_block);
            }
        }

        void RecountWalls()
        {
            int count = 0;
            foreach (var plane in planeManager.trackables)
                if (plane.alignment == PlaneAlignment.Vertical && plane.subsumedBy == null) count++;
            if (count == WallCount) return;
            WallCount = count;
            WallCountChanged?.Invoke(count);
        }

        // ---------------- Tap to place ----------------

        /// <summary>Called by the state machine: placement is only active in the Scanning state.</summary>
        public void SetPlacementEnabled(bool enabled)
        {
            _placementEnabled = enabled && !HasArena;
            if (_placementEnabled) return;
            if (reticle) reticle.gameObject.SetActive(false);
            SetReticleValid(false);
        }

        void Update()
        {
            if (_spinner) _spinner.Rotate(0f, 60f * Time.unscaledDeltaTime, 0f, Space.Self);
            if (!_placementEnabled) return;

            UpdateReticle();

            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase != TouchPhase.Began || UIHitTest.IsOverUI(touch.screenPosition)) continue;
                TryPlace();
                break;
            }
#if UNITY_EDITOR
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.bKey.wasPressedThisFrame) DebugPlaceArena();
#endif
        }

        void UpdateReticle()
        {
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool valid = false;
            if (raycastManager.Raycast(centre, s_Hits, TrackableType.PlaneWithinPolygon))
            {
                foreach (var hit in s_Hits)
                {
                    var plane = planeManager.GetPlane(hit.trackableId);
                    if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp) continue;
                    if (Mathf.Min(plane.size.x, plane.size.y) < minPlaneSize) continue;
                    _reticlePlane = plane;
                    _reticlePose = new Pose(hit.pose.position, FacePlayer(hit.pose.position));
                    valid = true;
                    break;
                }
            }

            if (reticle)
            {
                reticle.gameObject.SetActive(valid);
                if (valid) reticle.SetPositionAndRotation(_reticlePose.position, _reticlePose.rotation);
            }
            SetReticleValid(valid);
        }

        void SetReticleValid(bool valid)
        {
            if (valid == _reticleValid) return;
            _reticleValid = valid;
            ReticleValidChanged?.Invoke(valid);
        }

        Quaternion FacePlayer(Vector3 position)
        {
            Vector3 toCam = arCamera.transform.position - position;
            toCam.y = 0f;
            return toCam.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCam.normalized) : Quaternion.identity;
        }

        void TryPlace()
        {
            if (HasArena || !_reticleValid || _reticlePlane == null) return;

            // Anchored to the real floor so it stays put as ARCore refines its understanding of the room.
            ARAnchor anchor = anchorManager.AttachAnchor(_reticlePlane, _reticlePose);
            var root = Instantiate(arenaPrefab, _reticlePose.position, _reticlePose.rotation, anchor ? anchor.transform : null);
            if (!anchor) root.AddComponent<ARAnchor>();
            FinishPlacement(root.transform, _reticlePlane);
        }

#if UNITY_EDITOR
        /// <summary>Editor only: drops the arena in front of the camera without a detected plane.</summary>
        public void DebugPlaceArena()
        {
            if (HasArena) return;
            Vector3 fwd = arCamera.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            Vector3 pos = arCamera.transform.position + fwd * 1.2f + Vector3.down * 1.3f;
            var root = Instantiate(arenaPrefab, pos, FacePlayer(pos));
            FinishPlacement(root.transform, null);
        }
#endif

        void FinishPlacement(Transform root, ARPlane floor)
        {
            Arena = new Arena(root, floor, fallbackRadius, LayerMask.GetMask("ARFloor"));
            _spinner = root.Find("Model/Spinner");

            // Optional requirement: stop detecting new floors; keep finding walls for ricochets.
            planeManager.requestedDetectionMode = PlaneDetectionMode.Vertical;
            foreach (var plane in planeManager.trackables) StylePlane(plane); // dims the floors

            SetPlacementEnabled(false);
            ArenaPlaced?.Invoke(Arena);
        }
    }

    /// <summary>
    /// The placed play area. Answers spatial questions for gameplay: floor height, and whether a point
    /// is on the floor plane the player chose (fallback: within a radius of the beacon).
    /// </summary>
    public class Arena
    {
        readonly int _floorMask;

        public Transform Root { get; }
        public ARPlane FloorPlane { get; }
        public float FallbackRadius { get; }
        public Vector3 Center => Root.position;
        public float FloorHeight => Root.position.y;

        public Arena(Transform root, ARPlane floorPlane, float fallbackRadius, int floorMask)
        {
            Root = root;
            FloorPlane = floorPlane;
            FallbackRadius = fallbackRadius;
            _floorMask = floorMask;
        }

        public Vector3 ProjectToFloor(Vector3 p) => new Vector3(p.x, FloorHeight, p.z);

        /// <summary>Ray-casts down onto the ARFloor layer. Without a floor collider, uses the beacon radius.</summary>
        public bool IsOnFloor(Vector3 p)
        {
            var origin = new Vector3(p.x, FloorHeight + 0.5f, p.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 1f, _floorMask, QueryTriggerInteraction.Ignore))
                return Mathf.Abs(hit.point.y - FloorHeight) < 0.15f;
            return FloorPlane == null && IsWithinFallback(p);
        }

        public bool IsWithinFallback(Vector3 p)
        {
            Vector3 d = p - Center;
            d.y = 0f;
            return d.sqrMagnitude <= FallbackRadius * FallbackRadius;
        }
    }
}
