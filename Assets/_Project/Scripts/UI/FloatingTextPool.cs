using Ricochet.Core;
using Ricochet.Pooling;
using UnityEngine;

namespace Ricochet.UI
{
    /// <summary>
    /// Listens for awarded points and shows "+150" at the kill position, plus "x2 RICOCHET!" for bank shots.
    /// Texts come from an ObjectPool, like everything else spawned during play.
    /// </summary>
    public class FloatingTextPool : MonoBehaviour
    {
        [SerializeField] FloatingText prefab;
        [SerializeField, Min(1)] int poolSize = 12;
        [SerializeField] Color pointsColor = new Color(0.3f, 1f, 1f);
        [SerializeField] Color ricochetColor = new Color(1f, 0.25f, 0.85f);

        ObjectPool<FloatingText> _pool;
        GameManager _game;

        public void Initialize(GameManager game)
        {
            _game = game;
            ObjectPool<FloatingText> pool = null;
            pool = new ObjectPool<FloatingText>(prefab, poolSize, transform, t => t.BindRelease(x => pool.Release(x)));
            _pool = pool;
            _game.Score.PointsAwarded += OnPointsAwarded;
        }

        void OnDestroy()
        {
            if (_game) _game.Score.PointsAwarded -= OnPointsAwarded;
        }

        void OnPointsAwarded(int points, float multiplier, Vector3 worldPosition)
        {
            var cam = _game.Player.Camera;
            Spawn($"+{points}", pointsColor, 64f, worldPosition, cam);
            if (multiplier > 1f)
                Spawn($"x{multiplier:0.#} RICOCHET!", ricochetColor, 56f, worldPosition + Vector3.up * 0.25f, cam);
        }

        void Spawn(string text, Color color, float size, Vector3 world, Camera cam)
        {
            var t = _pool.Get(Vector3.zero, Quaternion.identity);
            t.Show(text, color, size, world, cam);
        }
    }
}
