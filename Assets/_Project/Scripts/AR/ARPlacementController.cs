using System;
using System.Collections.Generic;
using Ricochet.Core;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Ricochet.AR
{
    /// <summary>
    /// Tap-to-place. A reticle follows the floor at the centre of the screen; the first tap that is not on UI
    /// places the arena (beacon) there, anchored to the floor plane. Only ONE arena can ever be placed.
    /// After placement, horizontal detection stops (walls keep being found) and the floors are dimmed.
    /// </summary>
    public class ARPlacementController : MonoBehaviour
    {
        [Header("AR")]
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARAnchorManager anchorManager;
        [SerializeField] Camera arCamera;

        [Header("Placement")]
        [SerializeField] Transform reticle;
        [SerializeField] ArenaContext arenaPrefab;
        [Tooltip("Smallest side a floor plane must have before the beacon may be placed on it (metres).")]
        [SerializeField, Min(0f)] float minPlaneSize = 0.6f;

        static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        bool _placementEnabled;
        bool _reticleValid;
        Pose _reticlePose;
        ARPlane _reticlePlane;

        /// <summary>Raised once, when the arena has been placed.</summary>
        public event Action<Transform, ARPlane> ArenaPlaced;

        /// <summary>Raised when the reticle starts/stops pointing at a valid floor (drives the scan instructions).</summary>
        public event Action<bool> ReticleValidChanged;

        public bool HasArena => Arena != null;
        public ArenaContext Arena { get; private set; }
        public bool ReticleValid => _reticleValid;

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            // Lets the mouse act as a finger in the editor (XR Simulation testing).
            TouchSimulation.Enable();
#endif
        }

        void Start()
        {
            if (reticle) reticle.gameObject.SetActive(false);
        }

        /// <summary>Called by the game state machine: placement is only active in the Scanning state.</summary>
        public void SetPlacementEnabled(bool enabled)
        {
            _placementEnabled = enabled && !HasArena;
            if (!_placementEnabled)
            {
                if (reticle) reticle.gameObject.SetActive(false);
                SetReticleValid(false);
            }
        }

        void Update()
        {
            if (!_placementEnabled) return;

            UpdateReticle();

            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase != TouchPhase.Began) continue;
                if (UIHitTest.IsOverUI(touch.screenPosition)) continue;
                TryPlace();
                break;
            }

#if UNITY_EDITOR
            // Editor convenience: press B to drop the arena in front of the camera without a detected plane.
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
            Vector3 toCamera = arCamera.transform.position - position;
            toCamera.y = 0f;
            return toCamera.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCamera.normalized, Vector3.up) : Quaternion.identity;
        }

        void TryPlace()
        {
            if (HasArena || !_reticleValid || _reticlePlane == null) return;

            // Anchor to the plane so the arena stays put as ARCore refines its map.
            ARAnchor anchor = anchorManager.AttachAnchor(_reticlePlane, _reticlePose);
            Transform parent = anchor != null ? anchor.transform : null;

            var arena = Instantiate(arenaPrefab, _reticlePose.position, _reticlePose.rotation, parent);
            if (anchor == null) arena.gameObject.AddComponent<ARAnchor>();
            FinishPlacement(arena, _reticlePlane);
        }

#if UNITY_EDITOR
        /// <summary>Editor-only: drops the arena in front of the camera without a detected plane.</summary>
        public void DebugPlaceArena()
        {
            if (HasArena) return;
            Vector3 fwd = arCamera.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            Vector3 pos = arCamera.transform.position + fwd * 1.2f + Vector3.down * 1.3f;
            var arena = Instantiate(arenaPrefab, pos, FacePlayer(pos));
            FinishPlacement(arena, null);
        }
#endif

        void FinishPlacement(ArenaContext arena, ARPlane floor)
        {
            arena.Initialize(floor);
            Arena = arena;

            // Optional requirement: stop finding new floors, keep finding walls for ricochets.
            planeManager.requestedDetectionMode = PlaneDetectionMode.Vertical;
            ARPlaneStyler.SetFloorsDimmed(true);

            SetPlacementEnabled(false);
            ArenaPlaced?.Invoke(arena.transform, floor);
        }
    }
}
