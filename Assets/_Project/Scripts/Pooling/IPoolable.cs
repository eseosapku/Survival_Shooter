namespace Ricochet.Pooling
{
    /// <summary>
    /// Contract for anything that lives in an <see cref="ObjectPool{T}"/>.
    /// OnSpawned must reset ALL runtime state so a reused object behaves like a fresh one.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>Called right after the object is taken from the pool and activated.</summary>
        void OnSpawned();

        /// <summary>Called right before the object is deactivated and returned to the pool.</summary>
        void OnDespawned();
    }
}
