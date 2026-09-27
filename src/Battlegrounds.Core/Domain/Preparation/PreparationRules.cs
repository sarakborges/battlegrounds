namespace Battlegrounds.Core.Domain.Preparation;

public sealed class PreparationRules
{
    private readonly int[] _offerSizesByTier;
    private readonly int[] _actionOfferSizesByTier;
    private readonly int[] _initialUpgradeCostsByTier;

    public int StartingResource { get; }
    public int ResourcePerRound { get; }
    public int MaximumResource { get; }
    public int AcquireCost { get; }
    public int ReleaseValue { get; }
    public int RefreshCost { get; }
    public int FieldCapacity { get; }
    public int ReserveCapacity { get; }
    public int MaximumTier { get; }

    public PreparationRules(
        int startingResource,
        int resourcePerRound,
        int maximumResource,
        int acquireCost,
        int releaseValue,
        int refreshCost,
        int fieldCapacity,
        int reserveCapacity,
        int maximumTier,
        IReadOnlyList<int> offerSizesByTier,
        IReadOnlyList<int> initialUpgradeCostsByTier,
        IReadOnlyList<int>? actionOfferSizesByTier = null)
    {
        if (startingResource < 0) throw new ArgumentOutOfRangeException(nameof(startingResource));
        if (resourcePerRound < 0) throw new ArgumentOutOfRangeException(nameof(resourcePerRound));
        if (maximumResource < startingResource) throw new ArgumentOutOfRangeException(nameof(maximumResource));
        if (acquireCost < 0) throw new ArgumentOutOfRangeException(nameof(acquireCost));
        if (releaseValue < 0) throw new ArgumentOutOfRangeException(nameof(releaseValue));
        if (refreshCost < 0) throw new ArgumentOutOfRangeException(nameof(refreshCost));
        if (fieldCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(fieldCapacity));
        if (reserveCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(reserveCapacity));
        if (maximumTier <= 1) throw new ArgumentOutOfRangeException(nameof(maximumTier));
        ArgumentNullException.ThrowIfNull(offerSizesByTier);
        ArgumentNullException.ThrowIfNull(initialUpgradeCostsByTier);

        if (offerSizesByTier.Count != maximumTier)
            throw new ArgumentException("Offer size must be defined for every tier.", nameof(offerSizesByTier));
        if (initialUpgradeCostsByTier.Count != maximumTier - 1)
            throw new ArgumentException("Upgrade cost must be defined for every non-maximum tier.", nameof(initialUpgradeCostsByTier));
        if (offerSizesByTier.Any(size => size <= 0))
            throw new ArgumentException("Offer sizes must be positive.", nameof(offerSizesByTier));
        for (var index = 1; index < offerSizesByTier.Count; index++)
        {
            if (offerSizesByTier[index] < offerSizesByTier[index - 1])
                throw new ArgumentException("Offer sizes cannot decrease at higher tiers.", nameof(offerSizesByTier));
        }
        if (initialUpgradeCostsByTier.Any(cost => cost < 0))
            throw new ArgumentException("Upgrade costs cannot be negative.", nameof(initialUpgradeCostsByTier));

        var actionSizes = actionOfferSizesByTier?.ToArray() ?? new int[maximumTier];
        if (actionSizes.Length != maximumTier)
            throw new ArgumentException("Action offer size must be defined for every tier.", nameof(actionOfferSizesByTier));
        for (var index = 0; index < actionSizes.Length; index++)
        {
            if (actionSizes[index] < 0 || actionSizes[index] > offerSizesByTier[index])
                throw new ArgumentException("Action offer size must be between zero and total offer size.", nameof(actionOfferSizesByTier));
        }

        StartingResource = startingResource;
        ResourcePerRound = resourcePerRound;
        MaximumResource = maximumResource;
        AcquireCost = acquireCost;
        ReleaseValue = releaseValue;
        RefreshCost = refreshCost;
        FieldCapacity = fieldCapacity;
        ReserveCapacity = reserveCapacity;
        MaximumTier = maximumTier;
        _offerSizesByTier = offerSizesByTier.ToArray();
        _actionOfferSizesByTier = actionSizes;
        _initialUpgradeCostsByTier = initialUpgradeCostsByTier.ToArray();
    }

    public int GetResourceForRound(int round)
    {
        if (round <= 0) throw new ArgumentOutOfRangeException(nameof(round));
        return Math.Min(MaximumResource, StartingResource + ((round - 1) * ResourcePerRound));
    }

    public int GetOfferSize(int tier)
    {
        ValidateTier(tier);
        return _offerSizesByTier[tier - 1];
    }

    public int GetActionOfferSize(int tier)
    {
        ValidateTier(tier);
        return _actionOfferSizesByTier[tier - 1];
    }

    public int GetUnitOfferSize(int tier) => GetOfferSize(tier) - GetActionOfferSize(tier);

    public int? GetInitialUpgradeCost(int tier)
    {
        ValidateTier(tier);
        return tier == MaximumTier ? null : _initialUpgradeCostsByTier[tier - 1];
    }

    private void ValidateTier(int tier)
    {
        if (tier < 1 || tier > MaximumTier) throw new ArgumentOutOfRangeException(nameof(tier));
    }
}
