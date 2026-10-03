using UnityEngine;

namespace Ricochet.Cards
{
    /// <summary>Restores 30 HP.</summary>
    public class MedKitCard : AbilityCard
    {
        [SerializeField, Min(1f)] float healAmount = 30f;

        public override void Activate(PlayerContext context) => context.Health.Heal(healAmount);
    }
}
