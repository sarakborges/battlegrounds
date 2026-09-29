using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Choices;
using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Playables;
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
    private readonly UnitCatalog? _unitCatalog;
    private readonly PowerCatalog? _powerCatalog;
    private readonly ActionCatalog? _actionCatalog;
    private readonly UnitCombineCatalog? _combineCatalog;

    public PreparationEngine(PreparationRules rules, IUnitPool unitPool, IRandomSource randomSource)
        : this(rules, unitPool, randomSource, null, null, null, null, null) { }

    public PreparationEngine(
        PreparationRules rules,
        IUnitPool unitPool,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog,
        BehaviorCatalog? behaviorCatalog)
        : this(rules, unitPool, randomSource, unitCatalog, behaviorCatalog, null, null, null) { }

    public PreparationEngine(
        PreparationRules rules,
        IUnitPool unitPool,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog,
        BehaviorCatalog? behaviorCatalog,
        PowerCatalog? powerCatalog,
        ActionCatalog? actionCatalog = null,
        UnitCombineCatalog? combineCatalog = null)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _unitPool = unitPool ?? throw new ArgumentNullException(nameof(unitPool));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _unitCatalog = unitCatalog;
        _powerCatalog = powerCatalog;
        _actionCatalog = actionCatalog;
        _combineCatalog = combineCatalog;
        _effectEngine = new PreparationEffectEngine(
            _rules, _unitPool, _randomSource, unitCatalog, behaviorCatalog, powerCatalog, actionCatalog, RefreshOfferFromEffect, MutateOfferFromEffect);
    }

    public void BeginPreparation(MatchState match, IReadOnlyDictionary<PlayerId, int>? resourceAdjustments = null)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (match.Phase is not (MatchPhase.Setup or MatchPhase.Combat))
            throw new InvalidOperationException($"Cannot begin preparation from {match.Phase}.");

        var isMatchStart = match.Phase == MatchPhase.Setup;
        var activePlayers = match.Players.Where(player => !player.IsEliminated).ToArray();
        var offers = activePlayers.ToDictionary(player => player.Id, PrepareNextOffers);
        match.BeginPreparation();

        foreach (var player in activePlayers)
        {
            player.Leader?.BeginTurn();
            player.BeginPreparation(match.Round, _rules);
            if (resourceAdjustments is not null && resourceAdjustments.TryGetValue(player.Id, out var adjustment))
                player.AdjustResource(adjustment, _rules.MaximumResource);
            player.ReplaceOffer(offers[player.Id].Units);
            player.ReplaceActionOffer(offers[player.Id].Actions);
        }

        if (isMatchStart)
            foreach (var player in activePlayers) _effectEngine.ProcessPowerEvent(match, player, NativeTriggerKeys.OnMatchStart);
        foreach (var player in activePlayers) _effectEngine.ProcessTurnEvent(match, player, NativeTriggerKeys.OnTurnStart);
        match.MarkChanged();
    }

    internal void ReclaimEliminatedPlayerPoolCopies(PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        foreach (var definition in player.ReleasePoolClaimsAfterElimination())
            _unitPool.ReturnUnit(definition);
    }

    public PreparationCommandResult Execute(MatchState match, IPreparationCommand command)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(command);
        if (match.Phase != MatchPhase.Preparation) return PreparationCommandResult.Failure(PreparationFailureCode.MatchNotInPreparation);
        if (!match.TryGetPlayer(command.PlayerId, out var player)) return PreparationCommandResult.Failure(PreparationFailureCode.PlayerNotFound);
        if (player.IsEliminated) return PreparationCommandResult.Failure(PreparationFailureCode.PlayerEliminated);
        if (player.IsReadyForCombat) return PreparationCommandResult.Failure(PreparationFailureCode.PlayerAlreadyReady);
        if (player.PendingChoice is not null && command is not ResolveUnitChoiceCommand && command is not ResolveActionChoiceCommand)
            return PreparationCommandResult.Failure(PreparationFailureCode.PendingChoiceMustBeResolved);
        if (player.PendingChoice is null && command is ResolveUnitChoiceCommand or ResolveActionChoiceCommand)
            return PreparationCommandResult.Failure(PreparationFailureCode.NoPendingChoice);

        var result = command switch
        {
            AcquireUnitCommand acquire => AcquireUnit(match, player, acquire),
            AcquirePlayableCommand acquire => AcquirePlayable(match, player, acquire),
            ReleaseUnitCommand release => ReleaseUnit(match, player, release),
            DeployUnitCommand deploy => DeployUnit(match, player, deploy),
            ReorderFieldCommand reorder => ReorderField(player, reorder),
            PlayActionCommand playAction => PlayAction(match, player, playAction),
            CombineUnitsCommand combine => CombineUnits(match, player, combine),
            RefreshOfferCommand => RefreshOffer(match, player),
            UpgradeTierCommand => UpgradeTier(match, player),
            UsePowerCommand usePower => UsePower(match, player, usePower),
            ResolveUnitChoiceCommand resolveChoice => ResolveUnitChoice(match, player, resolveChoice),
            ResolveActionChoiceCommand resolveChoice => ResolveActionChoice(match, player, resolveChoice),
            FreezeOfferCommand => FreezeOffer(player),
            UnfreezeOfferCommand => UnfreezeOffer(player),
            FreezeOfferSlotCommand freezeSlot => FreezeOfferSlot(player, freezeSlot),
            UnfreezeOfferSlotCommand unfreezeSlot => UnfreezeOfferSlot(player, unfreezeSlot),
            EndPreparationCommand => EndPreparation(match, player),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.GetType().Name, "Unsupported preparation command."),
        };

        if (!result.Succeeded) return result;
        if (match.Players.Where(candidate => !candidate.IsEliminated).All(candidate => candidate.IsReadyForCombat)) match.BeginCombat();
        match.MarkChanged();
        return result;
    }

    private PreparationCommandResult AcquirePlayable(MatchState match, PlayerState player, AcquirePlayableCommand command)
    {
        if (!player.TryResolvePlayableOfferSlot(command.OfferSlot, out var entry))
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidOfferSlot);
        if (entry.Kind == PlayableKind.Unit)
            return AcquireUnit(match, player, new AcquireUnitCommand(player.Id, command.OfferSlot));
        return AcquireAction(match, player, command.OfferSlot - player.Offer.Count);
    }

    private PreparationCommandResult AcquireUnit(MatchState match, PlayerState player, AcquireUnitCommand command)
    {
        if (command.OfferSlot < 0 || command.OfferSlot >= player.Offer.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidOfferSlot);
        if (player.PlayableReserveCount >= _rules.ReserveCapacity)
            return PreparationCommandResult.Failure(PreparationFailureCode.ReserveFull);
        var acquireCost = player.GetUnitAcquireCost(_rules);
        if (!player.CanAfford(acquireCost))
            return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);

        var definition = player.TakeOfferedUnit(command.OfferSlot);
        var unit = match.CreateUnit(definition, UnitInstanceOrigin.Pooled);
        player.AddToReserve(unit);
        player.SpendResource(acquireCost);
        player.ConsumeAcquireDiscount();
        _effectEngine.ProcessAcquiredUnit(match, player, unit);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult AcquireAction(MatchState match, PlayerState player, int actionSlot)
    {
        if (actionSlot < 0 || actionSlot >= player.ActionOffer.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidOfferSlot);
        if (player.PlayableReserveCount >= _rules.ReserveCapacity)
            return PreparationCommandResult.Failure(PreparationFailureCode.ReserveFull);
        var definition = player.ActionOffer[actionSlot];
        var acquireCost = player.GetActionAcquireCost(definition);
        if (!player.CanAfford(acquireCost))
            return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);

        player.TakeOfferedAction(actionSlot);
        player.AddActionToReserve(match.CreateAction(definition));
        player.SpendResource(acquireCost);
        player.ConsumeAcquireDiscount();
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.ActionAcquired);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult ReleaseUnit(MatchState match, PlayerState player, ReleaseUnitCommand command)
    {
        if (command.FieldSlot < 0 || command.FieldSlot >= player.Field.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidFieldSlot);
        var unit = player.Field[command.FieldSlot];
        player.RemoveFromField(command.FieldSlot);
        if (unit.ReleasePoolReturnDefinition() is UnitDefinition poolDefinition) _unitPool.ReturnUnit(poolDefinition);
        player.GainResource(_rules.ReleaseValue, _rules.MaximumResource);
        _effectEngine.ProcessReleasedUnit(match, player, unit);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult DeployUnit(MatchState match, PlayerState player, DeployUnitCommand command)
    {
        if (command.ReserveSlot < 0 || command.ReserveSlot >= player.Reserve.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidReserveSlot);
        if (player.Field.Count >= _rules.FieldCapacity)
            return PreparationCommandResult.Failure(PreparationFailureCode.FieldFull);

        var unit = player.Reserve[command.ReserveSlot];
        var selectedSelectors = unit.Definition.Triggers
            .Where(trigger => trigger.Event == NativeTriggerKeys.OnPlay)
            .SelectMany(trigger => trigger.Effects)
            .Select(GetTargetSelector)
            .Where(selector => selector?.Scope == EffectTargetScope.Selected)
            .Cast<EffectTargetSelector>()
            .ToArray();
        if (!IsValidSelectedTarget(match, player, command.TargetUnitInstanceId, selectedSelectors, unit.Id))
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidDeployTarget);

        unit = player.DeployFromReserve(command.ReserveSlot);
        _effectEngine.ProcessPlayedUnit(match, player, unit, command.TargetUnitInstanceId);
        return PreparationCommandResult.Success();
    }

    private static PreparationCommandResult ReorderField(PlayerState player, ReorderFieldCommand command)
    {
        if (command.UnitInstanceIds is null ||
            command.UnitInstanceIds.Count != player.Field.Count ||
            command.UnitInstanceIds.Distinct().Count() != player.Field.Count)
        {
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidFieldOrder);
        }

        var currentIds = player.Field.Select(unit => unit.Id).ToHashSet();
        if (!currentIds.SetEquals(command.UnitInstanceIds))
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidFieldOrder);

        player.ReorderField(command.UnitInstanceIds);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult PlayAction(MatchState match, PlayerState player, PlayActionCommand command)
    {
        if (!player.TryResolvePlayableReserveSlot(command.ReserveSlot, out var entry) || entry.Kind != PlayableKind.Action)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidReserveSlot);
        var actionSlot = command.ReserveSlot - player.Reserve.Count;
        var action = player.ActionReserve[actionSlot];
        var selectedSelectors = action.Definition.Effects
            .Select(GetTargetSelector)
            .Where(selector => selector?.Scope == EffectTargetScope.Selected)
            .Cast<EffectTargetSelector>()
            .ToArray();

        if (!IsValidSelectedTarget(match, player, command.TargetUnitInstanceId, selectedSelectors))
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidActionTarget);

        action = player.RemoveActionFromReserve(actionSlot);
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.ActionPlayed);
        _effectEngine.ProcessAction(match, player, action, command.TargetUnitInstanceId);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult CombineUnits(MatchState match, PlayerState player, CombineUnitsCommand command)
    {
        if (_combineCatalog is null || _unitCatalog is null || !_combineCatalog.TryGet(command.CombineId, out var combine))
            return PreparationCommandResult.Failure(PreparationFailureCode.CombineUnavailable);
        if (command.UnitInstanceIds is null ||
            command.UnitInstanceIds.Count != combine.RequiredCopies ||
            command.UnitInstanceIds.Distinct().Count() != combine.RequiredCopies)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidCombineUnits);

        var selected = new List<(UnitInstance Unit, bool IsReserve)>(combine.RequiredCopies);
        foreach (var instanceId in command.UnitInstanceIds)
        {
            if (!player.TryGetOwnedUnit(instanceId, out var unit, out var isReserve) ||
                !unit.IsAlive ||
                unit.Definition.Id != combine.SourceUnitId)
                return PreparationCommandResult.Failure(PreparationFailureCode.InvalidCombineUnits);
            selected.Add((unit, isReserve));
        }

        var reserveSources = selected.Count(value => value.IsReserve);
        var reserveCountAfterCombine = player.PlayableReserveCount - reserveSources + 1;
        if (reserveCountAfterCombine > _rules.ReserveCapacity)
            return PreparationCommandResult.Failure(PreparationFailureCode.ReserveFull);

        var resultDefinition = _unitCatalog.GetRequired(combine.ResultUnitId);
        var inheritedModifiers = combine.InheritPersistentModifiers
            ? selected
                .SelectMany(value => value.Unit.Modifiers)
                .Where(modifier => modifier.Duration == UnitModifierDuration.Persistent)
                .GroupBy(modifier => modifier.Key, StringComparer.Ordinal)
                .Select(group => new
                {
                    Key = group.Key,
                    Attack = group.Sum(modifier => modifier.AttackDelta),
                    Health = group.Sum(modifier => modifier.HealthDelta),
                })
                .Where(modifier => modifier.Attack != 0 || modifier.Health != 0)
                .OrderBy(modifier => modifier.Key, StringComparer.Ordinal)
                .ToArray()
            : [];
        foreach (var value in selected)
        {
            var removed = player.RemoveOwnedUnit(value.Unit.Id);
            if (removed.PoolReturnDefinition is not null)
                _unitPool.ReturnUnit(removed.PoolReturnDefinition);
        }

        var result = match.CreateUnit(resultDefinition, UnitInstanceOrigin.Generated);
        foreach (var modifier in inheritedModifiers)
            result.ApplyModifier(modifier.Key, modifier.Attack, modifier.Health, UnitModifierDuration.Persistent);
        player.AddToReserve(result);
        _effectEngine.ProcessCombinedUnit(match, player, result);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult RefreshOffer(MatchState match, PlayerState player)
    {
        if (!player.CanAfford(_rules.RefreshCost)) return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);
        player.SpendResource(_rules.RefreshCost);
        RefreshOfferFromEffect(player);
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.OfferRefreshed);
        return PreparationCommandResult.Success();
    }

    private void RefreshOfferFromEffect(PlayerState player)
    {
        if (player.HasFrozenOfferSlots && !player.IsOfferFrozen)
        {
            var preserved = PreparePartiallyFrozenOffer(player);
            player.ReplaceOffer(preserved.Units);
            player.ReplaceActionOffer(preserved.Actions);
            return;
        }

        var unitOffer = _unitPool.ExchangeOffer(player.Offer.ToArray(), player.Tier, _rules.GetUnitOfferSize(player.Tier), _randomSource);
        var actionOffer = DrawActionOffer(player.Tier, _rules.GetActionOfferSize(player.Tier));
        player.ReplaceOffer(ValidateUnitOffer(unitOffer, player.Tier));
        player.ReplaceActionOffer(actionOffer);
        player.ClearOfferFrozen();
    }

    private void MutateOfferFromEffect(
        PlayerState player,
        OfferMutationOperation operation,
        PlayableKind playableKind,
        OfferSlotSelection selection)
    {
        player.ClearOfferFrozen();
        if (playableKind == PlayableKind.Unit)
        {
            MutateUnitOfferFromEffect(player, operation, selection);
            return;
        }

        MutateActionOfferFromEffect(player, operation, selection);
    }

    private void MutateUnitOfferFromEffect(PlayerState player, OfferMutationOperation operation, OfferSlotSelection selection)
    {
        if (operation == OfferMutationOperation.Add)
        {
            var added = _unitPool.DrawOffer(player.Tier, 1, _randomSource);
            if (added.Count > 0) player.AddOfferedUnit(ValidateEffectOfferUnit(added[0], player.Tier));
            return;
        }
        if (player.Offer.Count == 0) return;

        var index = ResolveOfferMutationIndex(player.Offer.Count, selection);
        if (operation == OfferMutationOperation.Remove)
        {
            _unitPool.ReturnUnit(player.TakeOfferedUnit(index));
            return;
        }

        var current = player.Offer[index];
        var replacement = _unitPool.ExchangeOffer([current], player.Tier, 1, _randomSource);
        if (replacement.Count == 0)
        {
            player.TakeOfferedUnit(index);
            return;
        }
        player.ReplaceOfferedUnit(index, ValidateEffectOfferUnit(replacement[0], player.Tier));
    }

    private void MutateActionOfferFromEffect(PlayerState player, OfferMutationOperation operation, OfferSlotSelection selection)
    {
        if (operation == OfferMutationOperation.Add)
        {
            var added = DrawActionOffer(player.Tier, 1, player.ActionOffer.Select(action => action.Id));
            if (added.Count > 0) player.AddOfferedAction(added[0]);
            return;
        }
        if (player.ActionOffer.Count == 0) return;

        var index = ResolveOfferMutationIndex(player.ActionOffer.Count, selection);
        if (operation == OfferMutationOperation.Remove)
        {
            player.TakeOfferedAction(index);
            return;
        }

        var replacement = DrawActionOffer(player.Tier, 1, player.ActionOffer.Select(action => action.Id));
        if (replacement.Count > 0) player.ReplaceOfferedAction(index, replacement[0]);
    }

    private int ResolveOfferMutationIndex(int count, OfferSlotSelection selection) =>
        selection switch
        {
            OfferSlotSelection.Leftmost => 0,
            OfferSlotSelection.Rightmost => count - 1,
            OfferSlotSelection.Random => _randomSource.NextInt(0, count),
            _ => throw new ArgumentOutOfRangeException(nameof(selection)),
        };

    private static UnitDefinition ValidateEffectOfferUnit(UnitDefinition unit, int tier)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit.Tier > tier) throw new InvalidOperationException("Unit pool returned an ineligible unit.");
        return unit;
    }

    private PreparationCommandResult UpgradeTier(MatchState match, PlayerState player)
    {
        if (player.Tier >= _rules.MaximumTier || player.UpgradeCost is null) return PreparationCommandResult.Failure(PreparationFailureCode.MaximumTier);
        if (!player.CanAfford(player.UpgradeCost.Value)) return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);
        player.UpgradeTier(_rules);
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.TierUpgraded);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult UsePower(MatchState match, PlayerState player, UsePowerCommand command)
    {
        var leader = player.Leader;
        if (_powerCatalog is null || leader?.CurrentPowerId is not PowerId powerId || !_powerCatalog.TryGet(powerId, out var power))
            return PreparationCommandResult.Failure(PreparationFailureCode.PowerUnavailable);
        if (power.Activation is null) return PreparationCommandResult.Failure(PreparationFailureCode.PowerNotActivatable);
        if (!leader.CanUse(power)) return PreparationCommandResult.Failure(PreparationFailureCode.PowerUsageLimitReached);
        var activation = power.Activation;
        if (!player.CanAfford(activation.Cost)) return PreparationCommandResult.Failure(PreparationFailureCode.InsufficientResource);

        var activationTrigger = power.FindTrigger(NativeTriggerKeys.OnActivate)
            ?? throw new InvalidOperationException($"Power '{power.Id}' has no onActivate trigger.");
        var selectedSelectors = activationTrigger.Effects.Select(GetTargetSelector)
            .Where(selector => selector?.Scope == EffectTargetScope.Selected).Cast<EffectTargetSelector>().ToArray();
        if (!IsValidSelectedTarget(match, player, command.TargetUnitInstanceId, selectedSelectors))
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidPowerTarget);

        player.SpendResource(activation.Cost);
        _effectEngine.ProcessGameEvent(match, player, NativeGameEventKeys.PowerActivated);
        _effectEngine.ProcessPower(match, player, power, command.TargetUnitInstanceId);
        leader.RecordUse(power.Id);
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult ResolveUnitChoice(MatchState match, PlayerState player, ResolveUnitChoiceCommand command)
    {
        if (player.PendingChoice is not PendingUnitChoice choice || choice.Id != command.ChoiceId ||
            command.OptionIndex < 0 || command.OptionIndex >= choice.Options.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidChoice);
        if (player.PlayableReserveCount >= _rules.ReserveCapacity) return PreparationCommandResult.Failure(PreparationFailureCode.ReserveFull);
        var definition = player.ResolveUnitChoice(command.ChoiceId, command.OptionIndex);
        player.AddToReserve(match.CreateUnit(definition, UnitInstanceOrigin.Generated));
        return PreparationCommandResult.Success();
    }

    private PreparationCommandResult ResolveActionChoice(MatchState match, PlayerState player, ResolveActionChoiceCommand command)
    {
        if (player.PendingChoice is not PendingActionChoice choice || choice.Id != command.ChoiceId ||
            command.OptionIndex < 0 || command.OptionIndex >= choice.Options.Count)
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidChoice);
        if (player.PlayableReserveCount >= _rules.ReserveCapacity) return PreparationCommandResult.Failure(PreparationFailureCode.ReserveFull);
        var definition = player.ResolveActionChoice(command.ChoiceId, command.OptionIndex);
        player.AddActionToReserve(match.CreateAction(definition));
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
            TransformUnitEffectDefinition value => value.Target,
            CopyUnitToReserveEffectDefinition value => value.Target,
            ReturnUnitToReserveEffectDefinition value => value.Target,
            ApplyUnitModifierEffectDefinition value => value.Target,
            RemoveUnitModifierEffectDefinition value => value.Target,
            _ => null,
        };

    private static bool IsValidSelectedTarget(
        MatchState match,
        PlayerState player,
        UnitInstanceId? targetInstanceId,
        IReadOnlyList<EffectTargetSelector> selectors,
        UnitInstanceId? excludedReserveInstanceId = null)
    {
        if (selectors.Count == 0) return targetInstanceId is null;
        if (targetInstanceId is null) return false;
        var zones = selectors.Select(selector => selector.Zone).Distinct().ToArray();
        if (zones.Length != 1) return false;
        if (zones[0] == EffectTargetZone.Reserve && excludedReserveInstanceId == targetInstanceId) return false;
        if (!TryGetSelectedTargetUnit(match, player, targetInstanceId.Value, zones[0], out var selected)) return false;
        return selectors.All(selector => MatchesSelector(selected, selector));
    }

    private static bool TryGetSelectedTargetUnit(
        MatchState match,
        PlayerState player,
        UnitInstanceId instanceId,
        EffectTargetZone zone,
        out UnitInstance unit)
    {
        if (zone == EffectTargetZone.Reserve)
        {
            if (player.TryGetOwnedUnit(instanceId, out unit, out var isReserve) && isReserve) return unit.IsAlive;
            unit = null!;
            return false;
        }

        foreach (var candidate in match.Players)
            if (candidate.TryGetFieldUnit(instanceId, out unit)) return unit.IsAlive;
        unit = null!;
        return false;
    }

    private static bool MatchesSelector(UnitInstance unit, EffectTargetSelector selector) =>
        (selector.RequiredTypeId is null || unit.Definition.Types.Any(type => type.Id == selector.RequiredTypeId.Value)) &&
        (selector.RequiredTagId is null || unit.Definition.Tags.Any(tag => tag.Id == selector.RequiredTagId.Value));

    private static PreparationCommandResult FreezeOffer(PlayerState player)
    {
        if (player.IsOfferFrozen) return PreparationCommandResult.Failure(PreparationFailureCode.OfferAlreadyFrozen);
        player.SetOfferFrozen(true);
        return PreparationCommandResult.Success();
    }

    private static PreparationCommandResult UnfreezeOffer(PlayerState player)
    {
        if (!player.HasFrozenOfferSlots) return PreparationCommandResult.Failure(PreparationFailureCode.OfferNotFrozen);
        player.ClearOfferFrozen();
        return PreparationCommandResult.Success();
    }

    private static PreparationCommandResult FreezeOfferSlot(PlayerState player, FreezeOfferSlotCommand command)
    {
        if (!player.TryResolvePlayableOfferSlot(command.OfferSlot, out _))
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidOfferSlot);
        return player.FreezeOfferSlot(command.OfferSlot)
            ? PreparationCommandResult.Success()
            : PreparationCommandResult.Failure(PreparationFailureCode.OfferSlotAlreadyFrozen);
    }

    private static PreparationCommandResult UnfreezeOfferSlot(PlayerState player, UnfreezeOfferSlotCommand command)
    {
        if (!player.TryResolvePlayableOfferSlot(command.OfferSlot, out _))
            return PreparationCommandResult.Failure(PreparationFailureCode.InvalidOfferSlot);
        return player.UnfreezeOfferSlot(command.OfferSlot)
            ? PreparationCommandResult.Success()
            : PreparationCommandResult.Failure(PreparationFailureCode.OfferSlotNotFrozen);
    }

    private PreparationCommandResult EndPreparation(MatchState match, PlayerState player)
    {
        _effectEngine.ProcessTurnEvent(match, player, NativeTriggerKeys.OnTurnEnd);
        player.MarkReadyForCombat();
        return PreparationCommandResult.Success();
    }

    private (IReadOnlyList<UnitDefinition> Units, IReadOnlyList<ActionDefinition> Actions) PrepareNextOffers(PlayerState player)
    {
        var unitCount = _rules.GetUnitOfferSize(player.Tier);
        var actionCount = _rules.GetActionOfferSize(player.Tier);
        if (!player.IsOfferFrozen)
        {
            if (player.HasFrozenOfferSlots) return PreparePartiallyFrozenOffer(player);
            var freshUnits = _unitPool.ExchangeOffer(player.Offer.ToArray(), player.Tier, unitCount, _randomSource);
            return (ValidateUnitOffer(freshUnits, player.Tier), DrawActionOffer(player.Tier, actionCount));
        }

        if (player.Offer.Count > unitCount || player.ActionOffer.Count > actionCount)
            throw new InvalidOperationException("Frozen offer exceeds the configured playable offer composition.");
        var frozenUnits = player.Offer.Concat(_unitPool.DrawOffer(player.Tier, unitCount - player.Offer.Count, _randomSource)).ToArray();
        var frozenActions = player.ActionOffer.Concat(DrawActionOffer(
            player.Tier,
            actionCount - player.ActionOffer.Count,
            player.ActionOffer.Select(action => action.Id))).ToArray();
        return (ValidateUnitOffer(frozenUnits, player.Tier), frozenActions);
    }

    private (IReadOnlyList<UnitDefinition> Units, IReadOnlyList<ActionDefinition> Actions) PreparePartiallyFrozenOffer(PlayerState player)
    {
        var frozenUnitSlots = Enumerable.Range(0, player.Offer.Count).Where(player.IsUnitOfferSlotFrozen).ToHashSet();
        var frozenActionSlots = Enumerable.Range(0, player.ActionOffer.Count).Where(player.IsActionOfferSlotFrozen).ToHashSet();
        var unitCount = Math.Max(_rules.GetUnitOfferSize(player.Tier), frozenUnitSlots.Count == 0 ? 0 : frozenUnitSlots.Max() + 1);
        var actionCount = Math.Max(_rules.GetActionOfferSize(player.Tier), frozenActionSlots.Count == 0 ? 0 : frozenActionSlots.Max() + 1);

        var returningUnits = player.Offer.Where((_, index) => !frozenUnitSlots.Contains(index)).ToArray();
        var freshUnits = _unitPool.ExchangeOffer(returningUnits, player.Tier, unitCount - frozenUnitSlots.Count, _randomSource);
        var units = MergeFrozenOfferSlots(player.Offer, frozenUnitSlots, ValidateUnitOffer(freshUnits, player.Tier), unitCount);

        var frozenActionIds = frozenActionSlots.Select(index => player.ActionOffer[index].Id).ToArray();
        var freshActions = DrawActionOffer(player.Tier, actionCount - frozenActionSlots.Count, frozenActionIds);
        var actions = MergeFrozenOfferSlots(player.ActionOffer, frozenActionSlots, freshActions, actionCount);
        return (units, actions);
    }

    private static IReadOnlyList<T> MergeFrozenOfferSlots<T>(
        IReadOnlyList<T> current,
        IReadOnlySet<int> frozenSlots,
        IReadOnlyList<T> fresh,
        int targetCount)
    {
        var result = new T[targetCount];
        var freshIndex = 0;
        for (var slot = 0; slot < targetCount; slot++)
        {
            if (slot < current.Count && frozenSlots.Contains(slot)) result[slot] = current[slot];
            else result[slot] = fresh[freshIndex++];
        }
        if (freshIndex != fresh.Count) throw new InvalidOperationException("Offer refresh produced an unexpected number of fresh entries.");
        return result;
    }

    private IReadOnlyList<ActionDefinition> DrawActionOffer(
        int maximumTier,
        int count,
        IEnumerable<ActionId>? excludedIds = null)
    {
        if (count == 0) return [];
        var catalog = _actionCatalog ?? throw new InvalidOperationException("Action offer slots require an ActionCatalog.");
        var excluded = new HashSet<ActionId>(excludedIds ?? []);
        var remaining = catalog.All.Where(action => action.Tier <= maximumTier && !excluded.Contains(action.Id)).ToList();
        if (remaining.Count < count)
            throw new InvalidOperationException($"Action catalog has {remaining.Count} eligible definitions but {count} are required.");
        var result = new List<ActionDefinition>(count);
        while (result.Count < count)
        {
            var index = _randomSource.NextInt(0, remaining.Count);
            result.Add(remaining[index]);
            remaining.RemoveAt(index);
        }
        return result;
    }

    private IReadOnlyList<UnitDefinition> ValidateUnitOffer(IReadOnlyList<UnitDefinition> offer, int tier)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var expectedCount = _rules.GetUnitOfferSize(tier);
        if (offer.Count != expectedCount) throw new InvalidOperationException($"Unit pool returned {offer.Count} units; expected {expectedCount}.");
        if (offer.Any(unit => unit is null || unit.Tier > tier)) throw new InvalidOperationException("Unit pool returned an ineligible unit.");
        return offer;
    }
}
