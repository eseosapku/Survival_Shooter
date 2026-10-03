using Ricochet.Audio;
using Ricochet.Core;
using UnityEngine;

namespace Ricochet.Enemies
{
    /// <summary>
    /// Melee enemy. Walks straight at the player; within claw range (0.8 m) it winds up and swipes.
    /// Damage only lands if the player is still close when the swipe connects.
    /// </summary>
    public class WalkerEnemy : Enemy
    {
        [SerializeField, Min(0f)] float windUp = 0.3f;

        float _windUpTimer = -1f;

        public override EnemyType Type => EnemyType.Walker;

        public override void OnSpawned()
        {
            base.OnSpawned();
            _windUpTimer = -1f;
        }

        protected override void TickBehaviour(float dt)
        {
            FacePlayer(dt);
            float distance = DistanceToPlayer;

            if (_windUpTimer >= 0f)
            {
                _windUpTimer -= dt;
                if (_windUpTimer < 0f && distance <= stats.AttackRange * 1.25f)
                    LandHit();
                return;
            }

            MoveTowardPlayer(dt, stats.AttackRange * 0.75f);

            if (distance <= stats.AttackRange && CooldownReady)
                Attack();
        }

        protected override void Attack()
        {
            ResetCooldown();
            _windUpTimer = windUp;
            PlayAttackAnimation();
        }

        void LandHit()
        {
            Vector3 hitPoint = Ctx.Player.TargetPoint;
            Ctx.PlayerTarget.TakeDamage(new DamageInfo(ScaledDamage, hitPoint, Ctx.Player.Position - transform.position));
            // Required sound: melee enemy damaging the player.
            AudioManager.Instance?.Play(SoundId.WalkerAttackHit);
        }
    }
}
