namespace Ricochet.Cards
{
    /// <summary>+1 prism charge. The HUD's prism button places it.</summary>
    public class PrismCard : AbilityCard
    {
        public override void Activate(PlayerContext context) => context.Placer.AddPrismCharge();
    }
}
