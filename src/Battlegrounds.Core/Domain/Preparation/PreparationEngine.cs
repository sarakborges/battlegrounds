using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Preparation;

public sealed class PreparationEngine
{
    private readonly PreparationRules _rules;
    private readonly IUnitPool _unitPool;
    private readonly IRandomSource _randomSource;
    private readonly PreparationEffectEngine _effectEngine;
    private readonly PowerCatalog? _powerCatalog;

    public PreparationEngine(
        PreparationRules rules,
        IUnitPool unitPool,
        IRandomSource randomSource)
        : this(rules, unitPool, randomSource, unitCatalog: null, behaviorCatalog: null, powerCatalog: null)
    {
    }

    public PreparationEngine(
        PreparationRules rules,
        IUnitPool unitPool,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog,
        BehaviorCatalog? behaviorCatalog)
        : this(rules, unitPool, randomSource, unitCatalog, behaviorCatalog, powerCatalog: null)
    {
    }

    public PreparationEngine(
        PreparationRules rules,
        IUnitPool unitPool,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog,
        BehaviorCatalog? behaviorCatalog,
        PowerCatalog? powerCatalog)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _unitPool = unitPool ?? throw new ArgumentNullException(nameof(unitPool));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _powerCatalog = powerCatalog;
        _effectEngine = new PreparationEffectEngine(
            _rules,
            _unitPool,
            _randomSource,
            unitCatalog,
            behaviorCatalog,
            powerCatalog);
    }

    public void BeginPreparation(
        MatchState match,
        IReadOnlyDictionary<PlayerId, int>? resourceAdjustments = null)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.Phase is not (MatchPhase.Setup or MatchPhase.Combat))
            throw new InvalidOperationException($"Cannot begin preparation from {match.Phase}.");

        var isMatchStart = match.Phase == MatchPhase.Setup;
        var activePlayers = match.Players.Where(player => !player.IsEliminated).ToArray();
        var offers = activePlayers.ToDictionary(player => player.Id, PrepareNextOffer);

        match.BeginPreparation();

        foreach (var player in activePlayers)
        {
            player.Leader?.BeginTurn();
            player.BeginPreparation(match.Round, _rules);
            if (resourceAdjustments is not null && resourceAdjustments.TryGetValue(player.Id, out var adjustment))
                player.AdjustResource(adjustment, _rules.MaximumResource);
            player.ReplaceOffer(offers[player.Id]);
        }

        if (isMatchStart)
        {
            foreach (var player in activePlayers)
                _effectEngine.ProcessPowerEvent(match, player, NativeTriggerKeys.OnMatchStart);
        }

        foreach (var player in activePlayers)
            _effectEngine.ProcessTurnEvent(match, player, NativeTriggerKeys.OnTurnStart);

        match.MarkChanged();
    }

    public PreparationCommandResult Execute(MatchState match, IPreparationCommand command)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(command);

        if (match.Phase != MatchPhase.Preparation)
            return PreparationCommandResult.Failure(PreparationFailureCode.MatchNotInPreparation);
        if (!match.TryGetPlayer(command.PlayerId, out var player))
            return PreparationCommandResult.Failure(PreparationFailureCode.PlayerNotFound);
        if (player.IsEliminated)
            return PreparationCommandResult.Failure(PreparationFailureCode.PlayerEliminated);
        if (player.IsReadyForCombat)
            return PreparationCommandResult.Failure(PreparationFailureCode.PlayerAlreadyReady);
        if (player.PendingChoice is not null && command is not ResolveUnitChoiceCommand)
            return PreparationCommandResult.Failure(PreparationFailureCode.PendingChoiceMustBeResolved);
        if (player.PendingChoice is null && command is ResolveUnitChoiceCommand)
            return PreparationCommandResult.Failure(PreparationFailureCode.NoPendingChoice);

        var result = command switch
        {
            AcquireUnitCommand acquire => AcquireUnit(match, player, acquire),
            ReleaseUnitCommand release => ReleaseUnit(match, player, release),
            DeployUnitCommand deploy => DeployUnit(match, player, deploy),
            RefreshOfferCommand => RefreshOffer(match, player),
            UpgradeTierCommand => UpgradeTier(match, player),
            UsePowerCommand usePower => UsePower(match, player, usePower),
            ResolveUnitChoiceCommand resolveChoice => ResolveUnitChoice(match, player, resolveChoice),
            FreezeOfferCommand => FreezeOffer(player),
            UnfreezeOfferCommand => UnfreezeOffer(player),
            EndPreparationCommand => EndPreparation(match, player),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.GetType().Name, "Unsupported preparation command."),
        };

        if (!result.Succeeded) return result;

        if (match.Players.Where(candidate => !candidate.IsEliminated).All(candidate => candidate.IsReadyForCombat))
            match.BeginCombat();

        match.MarkChanged();
        return result;
    }

    private PreparationCommandResult AcquireUnit(MatchState match, PlayerState player, AcquireUnitCommand command)
    {
        if (command.OfferSlot < 0 || command.OfferSlot >= player.Offer.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidOfferSlot);
        if (player.Reserve.Count >= _rules.ReserveCapacity)
            return PreparationCommandResult.Failure(PreparationFailureCode.ReserveFull);
        if (!player.CanAfford(_rules.AcquireCost))
            return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);

        var definition = player.TakeOfferedUnit(command.OfferSlot);
        var unit = match.CreateUnit(definition, UnitInstanceOrigin.Pooled);
        player.AddToReserve(unit);
        player.SpendResource(_rules.AcquireCost);
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.UnitAcquired, definition);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult ReleaseUnit(MatchState match, PlayerState player, ReleaseUnitCommand command)
    {
        if (command.FieldSlot < 0 || command.FieldSlot >= player.Field.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidFieldSlot);

        var unit = player.Field[command.FieldSlot];
        if (unit.Origin == UnitInstanceOrigin.Pooled)
            _unitPool.ReturnUnit(unit.Definition);

        player.RemoveFromField(command.FieldSlot);
        player.GainResource(_rules.ReleaseValue, _rules.MaximumResource);
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.UnitReleased, unit.Definition);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult DeployUnit(MatchState match, PlayerState player, DeployUnitCommand command)
    {
        if (command.ReserveSlot < 0 || command.ReserveSlot >= player.Reserve.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidReserveSlot);
        if (player.Field.Count >= _rules.FieldCapacity)
            return PreparationCommandResult.Failure(PreparationFailureCode.FieldFull);

        var unit = player.DeployFromReserve(command.ReserveSlot);
        _effectEngine.ProcessPlayedUnit(match, player, unit);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult RefreshOffer(MatchState match, PlayerState player)
    {
        if (!player.CanAfford(_rules.RefreshCost))
            return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);

        var offer = _unitPool.ExchangeOffer(
            player.Offer.ToArray(),
            player.Tier,
            _rules.GetOfferSize(player.Tier),
            _randomSource);

        player.SpendResource(_rules.RefreshCost);
        player.ReplaceOffer(ValidateOffer(offer, player.Tier));
        player.ClearOfferFrozen();
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.OfferRefreshed);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult UpgradeTier(MatchState match, PlayerState player)
    {
        if (player.Tier >= _rules.MaximumTier || player.UpgradeCost is null)
            return PreparationCommandResult.Failure(PreparationFailureCode.MaximumTier);
        if (!player.CanAfford(player.UpgradeCost.Value))
            return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);

        player.UpgradeTier(_rules);
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.TierUpgraded);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult UsePower(MatchState match, PlayerState player, UsePowerCommand command)
    {
        var leader = player.Leader;
        if (_powerCatalog is null || leader?.CurrentPowerId is not PowerId powerId ||
            !_powerCatalog.TryGet(powerId, out var power))
            return PreparationCommandResult.Failure(PreparationFailureCode.PowerUnavailable);
        if (power.Activation is null)
            return PreparationCommandResult.Failure(PreparationFailureCode.PowerNotActivatable);
        if (!leader.CanUse(power))
            return PreparationCommandResult.Failure(PreparationFailureCode.PowerUsageLimitReached);

        var activation = power.Activation;
        if (!player.CanAfford(activation.Cost))
            return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);

        var activationTrigger = power.FindTrigger(NativeTriggerKeys.OnActivate)
            ?? throw new InvalidOperationException($"Power '{power.Id}' has no onActivate trigger.");
        var selectedSelectors = activationTrigger.Effects
            .Select(GetTargetSelector)
            .Where(selector => selector?.Scope == EffectTargetScope.Selected)
            .Cast<EffectTargetSelector>()
            .ToArray();

        if (selectedSelectors.Length > 0)
        {
            if (command.TargetUnitInstanceId is null ||
                !TryGetFieldUnit(match, command.TargetUnitInstanceId.Value, out var selected) ||
                selectedSelectors.Any(selector => !MatchesSelector(selected, selector)))
                return PreparationCommandResult.Failure(PreparationFailureCode.InvalidPowerTarget);
        }
        else if (command.TargetUnitInstanceId is not null)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidPowerTarget);
        }

        player.SpendResource(activation.Cost);
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.PowerActivated);
        _effectEngine.ProcessPower(match, player, power, command.TargetUnitInstanceId);
        leader.RecordUse(power.Id);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult ResolveUnitChoice(
        MatchState match,
        PlayerState player,
        ResolveUnitChoiceCommand command)
    {
        var choice = player.PendingChoice;
        if (choice is null)
            return PreparationCommandResult.Failure(PreparationFailureCode.NoPendingChoice);
        if (choice.Id != command.ChoiceId || command.OptionIndex < 0 || command.OptionIndex >= choice.Options.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidChoice);
        if (player.Reserve.Count >= _rules.ReserveCapacity)
            return PreparationCommandResult.Failure(PreparationFailureCode.ReserveFull);

        var definition = player.ResolveUnitChoice(command.ChoiceId, command.OptionIndex);
        player.AddToReserve(match.CreateUnit(definition, UnitInstanceOrigin.Generated));
        return PreparationCommandResult.Success();
    }

    private static EffectTargetSelector? GetTargetSelector(EffectDefinition effect) =>
        effect switch
        {
            ModifyStatsEffectDefinition value => value.Target,
            DealDamageEffectDefinition value => value.Target,
            DestroyUnitEffectDefinition value => value.Target,
            TriggerEventEffectDefinition value => value.Target,
            AddBehaviorEffectDefinition value => value.Target,
            RemoveBehaviorEffectDefinition value => value.Target,
            _ => null,
        };

    private static bool TryGetFieldUnit(MatchState match, UnitInstanceId instanceId, out UnitInstance unit)
    {
        foreach (var player in match.Players)
        {
            if (player.TryGetFieldUnit(instanceId, out unit)) return unit.IsAlive;
        }
        unit = null!;
        return false;
    }

    private static bool MatchesSelector(UnitInstance unit, EffectTargetSelector selector) =>
        (selector.RequiredTypeId is null || unit.Definition.Types.Any(type => type.Id == selector.RequiredTypeId.Value)) &&
        (selector.RequiredTagId is null || unit.Definition.Tags.Any(tag => tag.Id == selector.RequiredTagId.Value));

    private static PreparationCommandResult FreezeOffer(PlayerState player)
    {
        if (player.IsOfferFrozen)
            return PreparationCommandResult.Failure(PreparationFailureCode.OfferAlreadyFrozen);
        player.SetOfferFrozen(true);
        return PreparationCommandResult.Success();
    }

    private static PreparationCommandResult UnfreezeOffer(PlayerState player)
    {
        if (!player.IsOfferFrozen)
            return PreparationCommandResult.Failure(PreparationFailureCode.OfferNotFrozen);
        player.SetOfferFrozen(false);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult EndPreparation(MatchState match, PlayerState player)
    {
        _effectEngine.ProcessTurnEvent(match, player, NativeTriggerKeys.OnTurnEnd);
        player.MarkReadyForCombat();
        return PreparationCommandResult.Success();
    }

    private IReadOnlyList<UnitDefinition> PrepareNextOffer(PlayerState player)
    {
        var expectedCount = _rules.GetOfferSize(player.Tier);
        if (!player.IsOfferFrozen)
        {
            var replacement = _unitPool.ExchangeOffer(player.Offer.ToArray(), player.Tier, expectedCount, _randomSource);
            return ValidateOffer(replacement, player.Tier);
        }
        if (player.Offer.Count > expectedCount)
            throw new InvalidOperationException("Frozen offer exceeds the configured offer size.");

        var missingCount = expectedCount - player.Offer.Count;
        if (missingCount == 0) return player.Offer.ToArray();

        var additions = _unitPool.DrawOffer(player.Tier, missingCount, _randomSource);
        return ValidateOffer(player.Offer.Concat(additions).ToArray(), player.Tier);
    }

    private IReadOnlyList<UnitDefinition> ValidateOffer(IReadOnlyList<UnitDefinition> offer, int tier)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var expectedCount = _rules.GetOfferSize(tier);
        if (offer.Count != expectedCount)
            throw new InvalidOperationException($"Unit pool returned {offer.Count} units; expected {expectedCount}.");
        if (offer.Any(unit => unit is null || unit.Tier > tier))
            throw new InvalidOperationException("Unit pool returned an ineligible unit.");
        return offer;
    }
}
