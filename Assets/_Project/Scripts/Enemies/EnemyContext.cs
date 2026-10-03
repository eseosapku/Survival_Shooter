using Ricochet.AR;
using Ricochet.Core;
using Ricochet.Player;

namespace Ricochet.Enemies
{
    /// <summary>What an enemy needs to know about the world, handed to it by the factory when it spawns.</summary>
    public class EnemyContext
    {
        public PlayerController Player;
        public IDamageable PlayerTarget;
        public ArenaContext Arena;
        public EnemyFactory Factory;
        public float DamageMultiplier = 1f;
    }
}
