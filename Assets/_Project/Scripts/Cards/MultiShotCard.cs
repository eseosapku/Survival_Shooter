using UnityEngine;

namespace Ricochet.Cards
{
    /// <summary>Spread tier +1 (Single → Twin → Tri → Quad) for 12 seconds.</summary>
    public class MultiShotCard : AbilityCard
    {
        [SerializeField, Min(1f)] float duration = 12f;

        public override void Activate(PlayerContext context) => context.Blaster.AddSpreadTier(1, duration);
    }
}
