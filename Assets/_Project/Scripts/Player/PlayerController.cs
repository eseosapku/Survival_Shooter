using UnityEngine;

namespace Ricochet.Player
{
    /// <summary>
    /// The AR camera IS the player. This component sits on the camera and gives gameplay a single place to ask
    /// "where is the player?" (head position, where to aim at it, its position on the floor).
    /// The hurtbox is a child sphere (r = 0.25 m, layer PlayerHurtbox) with a kinematic Rigidbody.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] Camera playerCamera;
        [SerializeField] SphereCollider hurtbox;

        public Camera Camera => playerCamera;
        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;

        /// <summary>The point enemies aim at (centre of the hurtbox).</summary>
        public Vector3 TargetPoint => hurtbox ? hurtbox.transform.TransformPoint(hurtbox.center) : transform.position;

        /// <summary>Horizontal distance from the player to a world point (height ignored).</summary>
        public float HorizontalDistanceTo(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
