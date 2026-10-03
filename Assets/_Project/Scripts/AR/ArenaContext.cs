using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Ricochet.AR
{
    /// <summary>
    /// Lives on the placed arena root (the beacon). Answers spatial questions for gameplay:
    /// how high the floor is, and whether a point is on the floor plane the player chose.
    /// </summary>
    public class ArenaContext : MonoBehaviour
    {
        [SerializeField] Transform beacon;
        [SerializeField] Transform spinner;
        [SerializeField, Min(0.5f)] float fallbackRadius = 4f;
        [SerializeField] LayerMask floorMask;

        public ARPlane FloorPlane { get; private set; }
        public Transform Beacon => beacon ? beacon : transform;
        public Vector3 Center => transform.position;
        public float FloorHeight => transform.position.y;

        public void Initialize(ARPlane floorPlane)
        {
            FloorPlane = floorPlane;
        }

        void Update()
        {
            if (spinner) spinner.Rotate(0f, 60f * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        /// <summary>Same point, snapped to the floor height.</summary>
        public Vector3 ProjectToFloor(Vector3 p) => new Vector3(p.x, FloorHeight, p.z);

        /// <summary>
        /// True if the point is over a detected floor plane (ray-cast down against the ARFloor layer).
        /// If no floor collider is available (e.g. editor debug placement), falls back to a radius around the beacon.
        /// </summary>
        public bool IsOnFloor(Vector3 p)
        {
            Vector3 origin = new Vector3(p.x, FloorHeight + 0.5f, p.z);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 1f, floorMask, QueryTriggerInteraction.Ignore))
                return Mathf.Abs(hit.point.y - FloorHeight) < 0.15f;

            return FloorPlane == null && IsWithinFallback(p);
        }

        public bool IsWithinFallback(Vector3 p)
        {
            Vector3 d = p - Center;
            d.y = 0f;
            return d.sqrMagnitude <= fallbackRadius * fallbackRadius;
        }

        public float FallbackRadius => fallbackRadius;
    }
}
