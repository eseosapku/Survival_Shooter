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
    public class ARController : MonoBehaviour
    {
        [Header("AR Foundation")]
        [SerializeField] ARRaycastManager raycastManager;
        [SerializeField] ARPlaneManager planeManager;
        [SerializeField] ARAnchorManager anchorManager;
        [SerializeField] Camera arCamera;

        [Header("Plane look")]
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

        static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock block;
        int floorLayer, wallLayer;
        bool placing;
        bool canPlace;
        Pose placePose;
        ARPlane placePlane;
        Transform spinner;

        public Arena Arena { get; private set; }
        public bool HasArena => Arena != null;
        public bool CanPlace => canPlace;
        public int WallCount { get; private set; }

        public event Action<Arena> ArenaPlaced;
        public event Action<bool> CanPlaceChanged;
        public event Action<int> WallsChanged;
        public event Action<Vector2> Tapped;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            floorLayer = LayerMask.NameToLayer("ARFloor");
            wallLayer = LayerMask.NameToLayer("ReflectiveWall");
            if (reticle) reticle.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
            TouchSimulation.Enable();
#endif
            planeManager.trackablesChanged.AddListener(OnPlanesChanged);
        }

        void OnDisable() => planeManager.trackablesChanged.RemoveListener(OnPlanesChanged);

        void OnPlanesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            foreach (var plane in args.added) Style(plane);
            foreach (var plane in args.updated) Style(plane);
            CountWalls();
        }

        void Style(ARPlane plane)
        {
            bool floor = plane.alignment == PlaneAlignment.HorizontalUp;
            bool wall = plane.alignment == PlaneAlignment.Vertical;
            var mesh = plane.GetComponent<MeshRenderer>();
            var line = plane.GetComponent<LineRenderer>();
            var col = plane.GetComponent<MeshCollider>();

            bool visible = floor || wall;
            if (mesh) mesh.forceRenderingOff = !visible;
            if (line) line.forceRenderingOff = !visible;
            if (col) col.enabled = visible;
            if (!visible) return;

            plane.gameObject.layer = floor ? floorLayer : wallLayer;
            var mat = floor ? floorMaterial : wallMaterial;
            if (line) line.startColor = line.endColor = floor ? floorLineColor : wallLineColor;
            if (!mesh) return;

            if (mesh.sharedMaterial != mat) mesh.sharedMaterial = mat;
            Color c = mat.GetColor(ColorId);
            if (floor && HasArena) c.a *= dimmedAlpha;
            mesh.GetPropertyBlock(block);
            block.SetColor(ColorId, c);
            mesh.SetPropertyBlock(block);
        }

        void CountWalls()
        {
            int count = 0;
            foreach (var plane in planeManager.trackables)
                if (plane.alignment == PlaneAlignment.Vertical && plane.subsumedBy == null) count++;
            if (count == WallCount) return;
            WallCount = count;
            WallsChanged?.Invoke(count);
        }

        public void EnablePlacement(bool on)
        {
            placing = on && !HasArena;
            if (placing) return;
            if (reticle) reticle.gameObject.SetActive(false);
            SetCanPlace(false);
        }

        void Update()
        {
            if (spinner) spinner.Rotate(0f, 60f * Time.unscaledDeltaTime, 0f, Space.Self);
            if (placing) UpdateReticle();

            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase != TouchPhase.Began || TouchUI.IsOverUI(touch.screenPosition)) continue;
                if (placing) Place();
                else Tapped?.Invoke(touch.screenPosition);
                break;
            }
#if UNITY_EDITOR
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (placing && kb != null && kb.bKey.wasPressedThisFrame) DebugPlace();
#endif
        }

        void UpdateReticle()
        {
            var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool valid = false;
            if (raycastManager.Raycast(centre, hits, TrackableType.PlaneWithinPolygon))
            {
                foreach (var hit in hits)
                {
                    var plane = planeManager.GetPlane(hit.trackableId);
                    if (plane == null || plane.alignment != PlaneAlignment.HorizontalUp) continue;
                    if (Mathf.Min(plane.size.x, plane.size.y) < minPlaneSize) continue;
                    placePlane = plane;
                    placePose = new Pose(hit.pose.position, FaceCamera(hit.pose.position));
                    valid = true;
                    break;
                }
            }

            if (reticle)
            {
                reticle.gameObject.SetActive(valid);
                if (valid) reticle.SetPositionAndRotation(placePose.position, placePose.rotation);
            }
            SetCanPlace(valid);
        }

        void SetCanPlace(bool value)
        {
            if (value == canPlace) return;
            canPlace = value;
            CanPlaceChanged?.Invoke(value);
        }

        Quaternion FaceCamera(Vector3 position)
        {
            Vector3 toCam = arCamera.transform.position - position;
            toCam.y = 0f;
            return toCam.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCam.normalized) : Quaternion.identity;
        }

        void Place()
        {
            if (HasArena || !canPlace || placePlane == null) return;
            ARAnchor anchor = anchorManager.AttachAnchor(placePlane, placePose);
            var root = Instantiate(arenaPrefab, placePose.position, placePose.rotation, anchor ? anchor.transform : null);
            if (!anchor) root.AddComponent<ARAnchor>();
            FinishPlacing(root.transform, placePlane);
        }

#if UNITY_EDITOR
        public void DebugPlace()
        {
            if (HasArena) return;
            Vector3 fwd = arCamera.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
            Vector3 pos = arCamera.transform.position + fwd * 1.2f + Vector3.down * 1.3f;
            var root = Instantiate(arenaPrefab, pos, FaceCamera(pos));
            FinishPlacing(root.transform, null);
        }
#endif

        void FinishPlacing(Transform root, ARPlane floor)
        {
            Arena = new Arena(root, floor, fallbackRadius, LayerMask.GetMask("ARFloor"));
            spinner = root.Find("Model/Spinner");
            planeManager.requestedDetectionMode = PlaneDetectionMode.Vertical;
            foreach (var plane in planeManager.trackables) Style(plane);
            EnablePlacement(false);
            ArenaPlaced?.Invoke(Arena);
        }
    }

    public class Arena
    {
        readonly int floorMask;

        public Transform Root { get; }
        public ARPlane Floor { get; }
        public float Radius { get; }
        public Vector3 Center => Root.position;
        public float Height => Root.position.y;

        public Arena(Transform root, ARPlane floor, float radius, int floorMask)
        {
            Root = root;
            Floor = floor;
            Radius = radius;
            this.floorMask = floorMask;
        }

        public Vector3 ToFloor(Vector3 p) => new Vector3(p.x, Height, p.z);

        public bool IsOnFloor(Vector3 p)
        {
            var origin = new Vector3(p.x, Height + 0.5f, p.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 1f, floorMask, QueryTriggerInteraction.Ignore))
                return Mathf.Abs(hit.point.y - Height) < 0.15f;
            return Floor == null && InRange(p);
        }

        public bool InRange(Vector3 p)
        {
            Vector3 d = p - Center;
            d.y = 0f;
            return d.sqrMagnitude <= Radius * Radius;
        }
    }
}
