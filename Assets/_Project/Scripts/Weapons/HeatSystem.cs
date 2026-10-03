using System;
using UnityEngine;

namespace Ricochet.Weapons
{
    /// <summary>
    /// Heat instead of ammo. Every shot adds heat, heat cools over time.
    /// Reaching 100% overheats the gun and locks firing for a short time.
    /// Plain C# class (not a MonoBehaviour) because it has no scene presence; LaserBlaster ticks it.
    /// </summary>
    public class HeatSystem
    {
        readonly Func<float> _coolPerSecond;
        readonly Func<float> _lockTime;
        float _lockTimer;

        public float Heat01 { get; private set; }
        public bool IsOverheated => _lockTimer > 0f;

        public event Action<float> HeatChanged;
        public event Action<bool> OverheatChanged;

        public HeatSystem(Func<float> coolPerSecond, Func<float> lockTime)
        {
            _coolPerSecond = coolPerSecond;
            _lockTime = lockTime;
        }

        public void Reset()
        {
            Heat01 = 0f;
            bool was = IsOverheated;
            _lockTimer = 0f;
            HeatChanged?.Invoke(Heat01);
            if (was) OverheatChanged?.Invoke(false);
        }

        public void AddHeat(float amount)
        {
            if (IsOverheated) return;
            Heat01 = Mathf.Clamp01(Heat01 + amount);
            HeatChanged?.Invoke(Heat01);

            if (Heat01 >= 1f)
            {
                _lockTimer = _lockTime();
                OverheatChanged?.Invoke(true);
            }
        }

        public void Tick(float deltaTime)
        {
            if (Heat01 <= 0f && !IsOverheated) return;

            if (IsOverheated)
            {
                _lockTimer -= deltaTime;
                // During the lock the bar drains from full to empty so the player can see when it's back.
                Heat01 = Mathf.Clamp01(_lockTimer / Mathf.Max(0.01f, _lockTime()));
                if (_lockTimer <= 0f)
                {
                    _lockTimer = 0f;
                    Heat01 = 0f;
                    OverheatChanged?.Invoke(false);
                }
            }
            else
            {
                Heat01 = Mathf.Max(0f, Heat01 - _coolPerSecond() * deltaTime);
            }
            HeatChanged?.Invoke(Heat01);
        }
    }
}
