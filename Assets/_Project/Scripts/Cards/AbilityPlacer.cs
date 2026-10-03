using System;
using Ricochet.AR;
using Ricochet.Audio;
using Ricochet.Pooling;
using UnityEngine;

namespace Ricochet.Cards
{
    /// <summary>
    /// Holds the player's mirror/prism charges and places them on the floor where the crosshair points,
    /// facing the camera. Gadgets are pooled and expire on their own.
    /// </summary>
    public class AbilityPlacer : MonoBehaviour
    {
        [SerializeField] Camera aimCamera;
        [SerializeField] Mirror mirrorPrefab;
        [SerializeField] Prism prismPrefab;
        [SerializeField] Transform poolRoot;
        [SerializeField, Min(1)] int poolSize = 4;
        [SerializeField] LayerMask floorMask;
        [SerializeField, Min(0.5f)] float maxPlaceDistance = 4f;
        [SerializeField, Min(0.3f)] float fallbackDistance = 1.2f;

        ObjectPool<Mirror> _mirrors;
        ObjectPool<Prism> _prisms;
        ArenaContext _arena;

        public int MirrorCharges { get; private set; }
        public int PrismCharges { get; private set; }

        /// <summary>(mirrorCharges, prismCharges)</summary>
        public event Action<int, int> ChargesChanged;

        void Awake()
        {
            ObjectPool<Mirror> mirrors = null;
            mirrors = new ObjectPool<Mirror>(mirrorPrefab, poolSize, poolRoot, m => m.BindRelease(x => mirrors.Release((Mirror)x)));
            _mirrors = mirrors;

            ObjectPool<Prism> prisms = null;
            prisms = new ObjectPool<Prism>(prismPrefab, poolSize, poolRoot, p => p.BindRelease(x => prisms.Release((Prism)x)));
            _prisms = prisms;
        }

        public void SetArena(ArenaContext arena) => _arena = arena;

        public void ResetCharges()
        {
            MirrorCharges = 0;
            PrismCharges = 0;
            Notify();
        }

        public void AddMirrorCharge()
        {
            MirrorCharges++;
            Notify();
        }

        public void AddPrismCharge()
        {
            PrismCharges++;
            Notify();
        }

        public bool TryPlaceMirror()
        {
            if (MirrorCharges <= 0 || !TryGetPlacement(out var pos, out var rot)) return false;
            MirrorCharges--;
            _mirrors.Get(pos, rot);
            AudioManager.Instance?.PlayAt(SoundId.MirrorPlace, pos);
            Notify();
            return true;
        }

        public bool TryPlacePrism()
        {
            if (PrismCharges <= 0 || !TryGetPlacement(out var pos, out var rot)) return false;
            PrismCharges--;
            _prisms.Get(pos, rot);
            AudioManager.Instance?.PlayAt(SoundId.MirrorPlace, pos);
            Notify();
            return true;
        }

        public void ClearPlaced()
        {
            _mirrors.ReleaseAll();
            _prisms.ReleaseAll();
        }

        /// <summary>Floor point under the crosshair (falls back to 1.2 m ahead), rotated to face the camera.</summary>
        bool TryGetPlacement(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (_arena == null) return false;

            Transform cam = aimCamera.transform;
            float floorY = _arena.FloorHeight;

            if (Physics.Raycast(cam.position, cam.forward, out var hit, maxPlaceDistance, floorMask, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
            }
            else if (cam.forward.y < -0.05f && (cam.position.y - floorY) / -cam.forward.y <= maxPlaceDistance)
            {
                // Intersect the view ray with the floor height.
                float t = (floorY - cam.position.y) / cam.forward.y;
                position = cam.position + cam.forward * t;
            }
            else
            {
                Vector3 fwd = cam.forward;
                fwd.y = 0f;
                position = cam.position + fwd.normalized * fallbackDistance;
            }
            position.y = floorY;

            Vector3 toCam = cam.position - position;
            toCam.y = 0f;
            rotation = toCam.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCam.normalized) : Quaternion.identity;
            return true;
        }

        void Notify() => ChargesChanged?.Invoke(MirrorCharges, PrismCharges);
    }
}
