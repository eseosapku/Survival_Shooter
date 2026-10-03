using Ricochet.Enemies;
using Ricochet.Player;
using Ricochet.Weapons;

namespace Ricochet.Cards
{
    /// <summary>Everything a card might need to change when it is picked up.</summary>
    public class PlayerContext
    {
        public PlayerController Player { get; }
        public PlayerHealth Health { get; }
        public LaserBlaster Blaster { get; }
        public AbilityPlacer Placer { get; }
        public EnemyFactory Enemies { get; }

        public PlayerContext(PlayerController player, PlayerHealth health, LaserBlaster blaster, AbilityPlacer placer, EnemyFactory enemies)
        {
            Player = player;
            Health = health;
            Blaster = blaster;
            Placer = placer;
            Enemies = enemies;
        }
    }
}
