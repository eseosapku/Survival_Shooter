using Ricochet.Core;
using UnityEngine;

namespace Ricochet.Cards
{
    /// <summary>Slows every enemy within 2 m of the player by 70% for 4 seconds.</summary>
    public class FreezeCard : AbilityCard
    {
        [SerializeField, Min(0.1f)] float radius = 2f;
        [SerializeField, Range(0f, 1f)] float slowFactor = 0.3f;
        [SerializeField, Min(0.1f)] float duration = 4f;

        public override void Activate(PlayerContext context)
        {
            Vector3 centre = context.Player.Position;
            context.Enemies.ForEachAlive(enemy =>
            {
                if (context.Player.HorizontalDistanceTo(enemy.transform.position) <= radius)
                    enemy.ApplySlow(slowFactor, duration);
            });
            VfxManager.Instance?.Ring(new Vector3(centre.x, transform.position.y, centre.z), radius, new Color(0.5f, 0.85f, 1f), 40);
        }
    }
}
