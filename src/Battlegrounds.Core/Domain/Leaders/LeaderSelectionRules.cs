namespace Battlegrounds.Core.Domain.Leaders;

public sealed class LeaderSelectionRules
{
    public int OfferSize { get; }
    public LeaderOfferPolicy OfferPolicy { get; }

    public LeaderSelectionRules(int offerSize, LeaderOfferPolicy offerPolicy)
    {
        if (offerSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offerSize));
        }

        OfferSize = offerSize;
        OfferPolicy = offerPolicy;
    }
}
