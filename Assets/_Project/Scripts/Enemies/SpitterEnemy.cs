using Ricochet.Audio;
using UnityEngine;

namespace Ricochet.Enemies
{
    /// <summary>
    /// Ranged enemy. Walks until it is 3 m from the player, then stops and spits pooled acid globs
    /// at the player's head every 2.5 s. Longer range than the Walker, takes more hits to kill.
    /// </summary>
    public class SpitterEnemy : Enemy
    {
        [SerializeField] Transform mouth;
        [SerializeField] Transform mouthGlow;

        public override EnemyType Type => EnemyType.Spitter;

        protected override void TickBehaviour(float dt)
        {
            FacePlayer(dt);
            float shootDistance = stats.AttackRange;

            if (DistanceToPlayer > shootDistance)
            {
                MoveTowardPlayer(dt, shootDistance * 0.95f);
                return;
            }

            // In range: stand still and shoot.
            if (mouthGlow)
            {
                // Mouth swells as the next shot gets close: a readable "about to fire" tell.
                float charge = AttackCharge01;
                mouthGlow.localScale = Vector3.one * (0.7f + charge * charge * 0.6f);
            }

            if (CooldownReady) Attack();
        }

        protected override void Attack()
        {
            ResetCooldown();
            PlayAttackAnimation();

            Vector3 origin = mouth ? mouth.position : transform.position + Vector3.up * 1.1f;
            Vector3 dir = (Ctx.Player.TargetPoint - origin).normalized;
            Ctx.Factory.SpawnAcid(origin, dir, stats.ProjectileSpeed, ScaledDamage);
            AudioManager.Instance?.PlayAt(SoundId.SpitterShoot, origin);
        }
    }
}
