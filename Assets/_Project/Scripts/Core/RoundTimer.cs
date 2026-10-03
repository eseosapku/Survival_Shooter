using System;
using UnityEngine;

namespace Ricochet.Core
{
    /// <summary>Counts the round down. Raises TimeChanged once per displayed second (not every frame).</summary>
    public class RoundTimer
    {
        int _lastWholeSecond = -1;

        public float Duration { get; private set; }
        public float Elapsed { get; private set; }
        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        public float Progress01 => Duration > 0f ? Mathf.Clamp01(Elapsed / Duration) : 0f;
        public bool IsFinished => Elapsed >= Duration;

        /// <summary>Remaining seconds (whole numbers, rounded up).</summary>
        public event Action<float> TimeChanged;

        public void Reset(float duration)
        {
            Duration = duration;
            Elapsed = 0f;
            _lastWholeSecond = -1;
            Notify();
        }

        public void Tick(float deltaTime)
        {
            if (IsFinished) return;
            Elapsed = Mathf.Min(Duration, Elapsed + deltaTime);
            Notify();
        }

        void Notify()
        {
            int whole = Mathf.CeilToInt(Remaining);
            if (whole == _lastWholeSecond) return;
            _lastWholeSecond = whole;
            TimeChanged?.Invoke(whole);
        }
    }
}
