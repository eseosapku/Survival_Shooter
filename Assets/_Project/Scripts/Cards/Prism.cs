using UnityEngine;

namespace Ricochet.Cards
{
    /// <summary>
    /// A crystal standing on the floor (layer Prism). A laser passing through it splits into 3 bolts (+/-25 degrees).
    /// The split itself is done by LaserBolt; this class only adds a slow spin so it reads as "magic".
    /// </summary>
    public class Prism : PlacedGadget
    {
        [SerializeField] Transform spinner;

        protected override void Update()
        {
            base.Update();
            if (spinner) spinner.Rotate(0f, 45f * Time.deltaTime, 0f, Space.Self);
        }
    }
}
