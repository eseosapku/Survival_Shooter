namespace Ricochet.Audio
{
    /// <summary>Every sound the game can play. Gameplay code asks for an id, never for a clip.</summary>
    public enum SoundId
    {
        // Required by the brief
        PlayerShoot,
        PlayerDeath,
        EnemySpawn,
        SpitterShoot,
        WalkerAttackHit,

        // Extra
        LaserBounce,
        EnemyHit,
        EnemyDeath,
        PlayerHurt,
        CardPickup,
        MirrorPlace,
        Overheat,
        CountdownBeep,
        CountdownGo,
        RoundWin,
        UIClick,
        Music
    }
}
