namespace Ricochet.Cards
{
    /// <summary>+1 mirror charge. The HUD's mirror button places it.</summary>
    public class MirrorCard : AbilityCard
    {
        public override void Activate(PlayerContext context) => context.Placer.AddMirrorCharge();
    }
}
