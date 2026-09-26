namespace Battlegrounds.Core.Domain.Recruitment;

public sealed class RecruitmentRules
{
    private readonly int[] _offerSizesByTier;
    private readonly int[] _initialUpgradeCostsByTier;

    public static RecruitmentRules Standard { get; } = new(
        startingGold: 3,
        goldPerRound: 1,
        maximumGold: 10,
        buyCost: 3,
        sellReward: 1,
        refreshCost: 1,
        boardCapacity: 7,
        handCapacity: 10,
        maximumTavernTier: 6,
        maximumFreezeTogglesPerRecruitment: 5,
        offerSizesByTier: [3, 4, 4, 5, 5, 6],
        initialUpgradeCostsByTier: [5, 7, 8, 11, 11]);

    public int StartingGold { get; }
    public int GoldPerRound { get; }
    public int MaximumGold { get; }
    public int BuyCost { get; }
    public int SellReward { get; }
    public int RefreshCost { get; }
    public int BoardCapacity { get; }
    public int HandCapacity { get; }
    public int MaximumTavernTier { get; }
    public int MaximumFreezeTogglesPerRecruitment { get; }

    public RecruitmentRules(
        int startingGold,
        int goldPerRound,
        int maximumGold,
        int buyCost,
        int sellReward,
        int refreshCost,
        int boardCapacity,
        int handCapacity,
        int maximumTavernTier,
        int maximumFreezeTogglesPerRecruitment,
        IReadOnlyList<int> offerSizesByTier,
        IReadOnlyList<int> initialUpgradeCostsByTier)
    {
        if (startingGold < 0) throw new ArgumentOutOfRangeException(nameof(startingGold));
        if (goldPerRound < 0) throw new ArgumentOutOfRangeException(nameof(goldPerRound));
        if (maximumGold < startingGold) throw new ArgumentOutOfRangeException(nameof(maximumGold));
        if (buyCost < 0) throw new ArgumentOutOfRangeException(nameof(buyCost));
        if (sellReward < 0) throw new ArgumentOutOfRangeException(nameof(sellReward));
        if (refreshCost < 0) throw new ArgumentOutOfRangeException(nameof(refreshCost));
        if (boardCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(boardCapacity));
        if (handCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(handCapacity));
        if (maximumTavernTier <= 1) throw new ArgumentOutOfRangeException(nameof(maximumTavernTier));
        if (maximumFreezeTogglesPerRecruitment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumFreezeTogglesPerRecruitment));
        }

        if (offerSizesByTier is null) throw new ArgumentNullException(nameof(offerSizesByTier));
        if (initialUpgradeCostsByTier is null) throw new ArgumentNullException(nameof(initialUpgradeCostsByTier));
        if (offerSizesByTier.Count != maximumTavernTier)
        {
            throw new ArgumentException("Offer size must be defined for every tavern tier.", nameof(offerSizesByTier));
        }

        if (initialUpgradeCostsByTier.Count != maximumTavernTier - 1)
        {
            throw new ArgumentException("Upgrade cost must be defined for every non-maximum tavern tier.", nameof(initialUpgradeCostsByTier));
        }

        if (offerSizesByTier.Any(size => size <= 0))
        {
            throw new ArgumentException("Offer sizes must be positive.", nameof(offerSizesByTier));
        }

        for (var index = 1; index < offerSizesByTier.Count; index++)
        {
            if (offerSizesByTier[index] < offerSizesByTier[index - 1])
            {
                throw new ArgumentException(
                    "Offer sizes cannot decrease at higher tavern tiers.",
                    nameof(offerSizesByTier));
            }
        }

        if (initialUpgradeCostsByTier.Any(cost => cost < 0))
        {
            throw new ArgumentException("Upgrade costs cannot be negative.", nameof(initialUpgradeCostsByTier));
        }

        StartingGold = startingGold;
        GoldPerRound = goldPerRound;
        MaximumGold = maximumGold;
        BuyCost = buyCost;
        SellReward = sellReward;
        RefreshCost = refreshCost;
        BoardCapacity = boardCapacity;
        HandCapacity = handCapacity;
        MaximumTavernTier = maximumTavernTier;
        MaximumFreezeTogglesPerRecruitment = maximumFreezeTogglesPerRecruitment;
        _offerSizesByTier = offerSizesByTier.ToArray();
        _initialUpgradeCostsByTier = initialUpgradeCostsByTier.ToArray();
    }

    public int GetGoldForRound(int round)
    {
        if (round <= 0) throw new ArgumentOutOfRangeException(nameof(round));
        return Math.Min(MaximumGold, StartingGold + ((round - 1) * GoldPerRound));
    }

    public int GetOfferSize(int tavernTier)
    {
        ValidateTavernTier(tavernTier);
        return _offerSizesByTier[tavernTier - 1];
    }

    public int? GetInitialUpgradeCost(int tavernTier)
    {
        ValidateTavernTier(tavernTier);
        return tavernTier == MaximumTavernTier ? null : _initialUpgradeCostsByTier[tavernTier - 1];
    }

    private void ValidateTavernTier(int tavernTier)
    {
        if (tavernTier < 1 || tavernTier > MaximumTavernTier)
        {
            throw new ArgumentOutOfRangeException(nameof(tavernTier));
        }
    }
}
