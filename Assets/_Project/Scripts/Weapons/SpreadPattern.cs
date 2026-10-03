using System.Collections.Generic;
using UnityEngine;

namespace Ricochet.Weapons
{
    public enum SpreadTier
    {
        Single = 1,
        Twin = 2,
        Tri = 3,
        Quad = 4
    }

    /// <summary>Works out the directions of each bolt in a horizontal fan (bolts ~8 degrees apart).</summary>
    public static class SpreadPattern
    {
        public static void GetDirections(Vector3 forward, Vector3 up, int count, float angleBetween, List<Vector3> results)
        {
            results.Clear();
            count = Mathf.Clamp(count, 1, 4);
            float start = -angleBetween * (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float angle = start + angleBetween * i;
                results.Add(Quaternion.AngleAxis(angle, up) * forward);
            }
        }
    }
}
