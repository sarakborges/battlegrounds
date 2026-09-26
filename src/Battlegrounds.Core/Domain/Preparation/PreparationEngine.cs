using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Preparation;

public sealed class PreparationEngine
{
    private readonly PreparationRules _rules;
    private readonly IUnitPool _unitPool;
    private readonly IRandomSource _randomSource;

    public PreparationEngine(
        PreparationRules rules,
        IUnitPool unitPool,
        IRandomSource randomSource)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _unitPool = unitPool ?? throw new ArgumentNullException(nameof(unitPool));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
    }

    public void BeginPreparation(MatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.Phase is not (MatchPhase.Setup or MatchPhase.Combat))
        {
            throw new InvalidOperationException($"Cannot begin preparation from {match.Phase}.");
        }

        var offers = match.Players.ToDictionary(
            player => player.Id,
            PrepareNextOffer);

        match.BeginPreparation();

        foreach (var player in match.Players)
        {
            player.BeginPreparation(match.Round, _rules);
            player.ReplaceOffer(offers[player.Id]);
        }

        match.MarkChanged();
    }

    public PreparationCommandResult Execute(MatchState match, IPreparationCommand command)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(command);

        if (match.Phase != MatchPhase.Preparation)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.MatchNotInPreparation);
        }

        if (!match.TryGetPlayer(command.PlayerId, out var player))
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.PlayerNotFound);
        }

        if (player.IsReadyForCombat)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.PlayerAlreadyReady);
        }

        var result = command switch
        {
            AcquireUnitCommand acquire => AcquireUnit(match, player, acquire),
            ReleaseUnitCommand release => ReleaseUnit(player, release),
            DeployUnitCommand deploy => DeployUnit(player, deploy),
            RefreshOfferCommand => RefreshOffer(player),
            UpgradeTierCommand => UpgradeTier(player),
            FreezeOfferCommand => FreezeOffer(player),
            UnfreezeOfferCommand => UnfreezeOffer(player),
            EndPreparationCommand => EndPreparation(player),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.GetType().Name, "Unsupported preparation command."),
        };

        if (!result.Succeeded)
        {
            return result;
        }

        if (match.Players.All(candidate => candidate.IsReadyForCombat))
        {
            match.BeginCombat();
        }

        match.MarkChanged();
        return result;
    }

    private PreparationCommandResult AcquireUnit(
        MatchState match,
        PlayerState player,
        AcquireUnitCommand command)
    {
        if (command.OfferSlot < 0 || command.OfferSlot >= player.Offer.Count)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidOfferSlot);
        }

        if (player.Reserve.Count >= _rules.ReserveCapacity)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.ReserveFull);
        }

        if (!player.CanAfford(_rules.AcquireCost))
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);
        }

        var definition = player.TakeOfferedUnit(command.OfferSlot);
        var unit = match.CreateUnit(definition);
        player.AddToReserve(unit);
        player.SpendResource(_rules.AcquireCost);

        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult ReleaseUnit(PlayerState player, ReleaseUnitCommand command)
    {
        if (command.FieldSlot < 0 || command.FieldSlot >= player.Field.Count)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidFieldSlot);
        }

        var unit = player.Field[command.FieldSlot];
        _unitPool.ReturnUnit(unit.Definition);
        player.RemoveFromField(command.FieldSlot);
        player.GainResource(_rules.ReleaseValue, _rules.MaximumResource);

        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult DeployUnit(PlayerState player, DeployUnitCommand command)
    {
        if (command.ReserveSlot < 0 || command.ReserveSlot >= player.Reserve.Count)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidReserveSlot);
        }

        if (player.Field.Count >= _rules.FieldCapacity)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.FieldFull);
        }

        player.DeployFromReserve(command.ReserveSlot);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult RefreshOffer(PlayerState player)
    {
        if (!player.CanAfford(_rules.RefreshCost))
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);
        }

        var offer = _unitPool.ExchangeOffer(
            player.Offer.ToArray(),
            player.Tier,
            _rules.GetOfferSize(player.Tier),
            _randomSource);

        player.SpendResource(_rules.RefreshCost);
        player.ReplaceOffer(ValidateOffer(offer, player.Tier));
        player.ClearOfferFrozen();

        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult UpgradeTier(PlayerState player)
    {
        if (player.Tier >= _rules.MaximumTier || player.UpgradeCost is null)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.MaximumTier);
        }

        if (!player.CanAfford(player.UpgradeCost.Value))
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);
        }

        player.UpgradeTier(_rules);
        return PreparationCommandResult.Success();
    }

    private static PreparationCommandResult FreezeOffer(PlayerState player)
    {
        if (player.IsOfferFrozen)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.OfferAlreadyFrozen);
        }

        player.SetOfferFrozen(true);
        return PreparationCommandResult.Success();
    }

    private static PreparationCommandResult UnfreezeOffer(PlayerState player)
    {
        if (!player.IsOfferFrozen)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.OfferNotFrozen);
        }

        player.SetOfferFrozen(false);
        return PreparationCommandResult.Success();
    }

    private static PreparationCommandResult EndPreparation(PlayerState player)
    {
        player.MarkReadyForCombat();
        return PreparationCommandResult.Success();
    }

    private IReadOnlyList<UnitDefinition> PrepareNextOffer(PlayerState player)
    {
        var expectedCount = _rules.GetOfferSize(player.Tier);

        if (!player.IsOfferFrozen)
        {
            var replacement = _unitPool.ExchangeOffer(
                player.Offer.ToArray(),
                player.Tier,
                expectedCount,
                _randomSource);

            return ValidateOffer(replacement, player.Tier);
        }

        if (player.Offer.Count > expectedCount)
        {
            throw new InvalidOperationException("Frozen offer exceeds the configured offer size.");
        }

        var missingCount = expectedCount - player.Offer.Count;
        if (missingCount == 0)
        {
            return player.Offer.ToArray();
        }

        var additions = _unitPool.DrawOffer(player.Tier, missingCount, _randomSource);
        var combined = player.Offer.Concat(additions).ToArray();
        return ValidateOffer(combined, player.Tier);
    }

    private IReadOnlyList<UnitDefinition> ValidateOffer(
        IReadOnlyList<UnitDefinition> offer,
        int tier)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var expectedCount = _rules.GetOfferSize(tier);
        if (offer.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Unit pool returned {offer.Count} units; expected {expectedCount}.");
        }

        if (offer.Any(unit => unit is null || unit.Tier > tier))
        {
            throw new InvalidOperationException("Unit pool returned an ineligible unit.");
        }

        return offer;
    }
}
