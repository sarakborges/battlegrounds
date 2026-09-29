using Battlegrounds.Core.Domain.Playables;
using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Preparation;

public sealed class ActionPlayableTests
{
    [Fact]
    public void Preparation_CanAcquireAndPlayActionThroughGenericPlayableSurface()
    {
        var unit = new UnitDefinition(new UnitId("unit"), "Unit", 1, 2, 2);
        var action = new ActionDefinition(
            new ActionId("training"),
            "Training",
            1,
            2,
            [new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Selected), 1, 1)]);
        var units = new UnitCatalog([unit]);
        var actions = new ActionCatalog([action]);
        var rules = new PreparationRules(
            startingResource: 10,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [2, 2],
            initialUpgradeCostsByTier: [5],
            actionOfferSizesByTier: [1, 1]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 10)]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, actions);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));

        engine.BeginPreparation(match);
        var player = match.Players.Single(value => value.Id == new PlayerId(0));
        Assert.Equal(2, player.PlayableOffer.Count);

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        var fieldUnit = Assert.Single(player.Field);

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.Single(player.ActionReserve);
        Assert.True(engine.Execute(match, new PlayActionCommand(player.Id, 0, fieldUnit.Id)).Succeeded);

        Assert.Empty(player.ActionReserve);
        Assert.Equal(3, fieldUnit.Attack);
        Assert.Equal(3, fieldUnit.Health);
        Assert.Equal(5, player.Resource);
    }

    [Fact]
    public void Preparation_ActionAcquiredAndPlayedDispatchAfterEventCountListeners()
    {
        var listener = new UnitDefinition(
            new UnitId("listener"),
            "Listener",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.AfterEventCount,
                    [new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Self), 1, 0)],
                    count: 1,
                    counter: new EffectHistoryQuery(NativeGameEventKeys.ActionAcquired, EffectHistoryScope.Turn)),
                new TriggerDefinition(
                    NativeTriggerKeys.AfterEventCount,
                    [new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Self), 0, 1)],
                    count: 1,
                    counter: new EffectHistoryQuery(NativeGameEventKeys.ActionPlayed, EffectHistoryScope.Turn)),
            ]);
        var action = new ActionDefinition(
            new ActionId("spark"),
            "Spark",
            1,
            0,
            [new AddResourceEffectDefinition(1)]);
        var units = new UnitCatalog([listener]);
        var actions = new ActionCatalog([action]);
        var rules = new PreparationRules(
            startingResource: 10,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost: 0,
            releaseValue: 1,
            refreshCost: 0,
            fieldCapacity: 7,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [2, 2],
            initialUpgradeCostsByTier: [5],
            actionOfferSizesByTier: [1, 1]);
        var pool = new UnitPool(units, [new UnitPoolEntry(listener.Id, 10)]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, actions);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        var field = Assert.Single(player.Field);
        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        Assert.Equal(2, field.Attack);
        Assert.Equal(1, field.Health);

        Assert.True(engine.Execute(match, new PlayActionCommand(player.Id, 0)).Succeeded);
        Assert.Equal(2, field.Attack);
        Assert.Equal(2, field.Health);
    }

    [Fact]
    public void AcquireDiscount_AppliesToActionAndIsConsumed()
    {
        var unit = new UnitDefinition(new UnitId("unit"), "Unit", 1, 1, 1);
        var action = new ActionDefinition(new ActionId("action"), "Action", 1, 4, [new AddResourceEffectDefinition(1)]);
        var units = new UnitCatalog([unit]);
        var actions = new ActionCatalog([action]);
        var rules = new PreparationRules(10, 0, 10, 3, 1, 1, 7, 10, 2, [2, 2], [5], [1, 1]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 10)]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, actions);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];
        player.AddAcquireDiscount(3);
        var actionEntry = player.PlayableOffer.Single(entry => entry.Kind == PlayableKind.Action);
        Assert.Equal(1, player.GetAcquireCost(actionEntry, rules));

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, actionEntry.Slot)).Succeeded);
        Assert.Equal(9, player.Resource);
        Assert.Equal(0, player.NextAcquireDiscount);
    }

    [Fact]
    public void FreezeOfferSlot_PreservesSelectedActionDuringRefresh()
    {
        var unit = new UnitDefinition(new UnitId("unit"), "Unit", 1, 1, 1);
        var a = new ActionDefinition(new ActionId("a"), "A", 1, 0, [new AddResourceEffectDefinition(1)]);
        var b = new ActionDefinition(new ActionId("b"), "B", 1, 0, [new AddResourceEffectDefinition(1)]);
        var c = new ActionDefinition(new ActionId("c"), "C", 1, 0, [new AddResourceEffectDefinition(1)]);
        var units = new UnitCatalog([unit]);
        var actions = new ActionCatalog([a, b, c]);
        var rules = new PreparationRules(10, 0, 10, 3, 1, 0, 7, 10, 2, [1, 1], [5], [2, 2]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 20)]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, actions);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];
        var frozenAction = player.ActionOffer[1];
        var playableSlot = player.Offer.Count + 1;

        Assert.True(engine.Execute(match, new FreezeOfferSlotCommand(player.Id, playableSlot)).Succeeded);
        Assert.True(engine.Execute(match, new RefreshOfferCommand(player.Id)).Succeeded);

        Assert.Same(frozenAction, player.ActionOffer[1]);
        Assert.True(player.PlayableOffer[player.Offer.Count + 1].IsFrozen);
    }

    [Fact]
    public void MutateOffer_ActionCanAddRemoveAndReplaceUnitSlotsAndClearsFreeze()
    {
        var a = new UnitDefinition(new UnitId("a"), "A", 1, 1, 1);
        var b = new UnitDefinition(new UnitId("b"), "B", 1, 1, 1);
        var units = new UnitCatalog([a, b]);
        var add = new ActionDefinition(new ActionId("add"), "Add", 1, 0,
            [new MutateOfferEffectDefinition(OfferMutationOperation.Add, PlayableKind.Unit)]);
        var remove = new ActionDefinition(new ActionId("remove"), "Remove", 1, 0,
            [new MutateOfferEffectDefinition(OfferMutationOperation.Remove, PlayableKind.Unit, OfferSlotSelection.Leftmost)]);
        var replace = new ActionDefinition(new ActionId("replace"), "Replace", 1, 0,
            [new MutateOfferEffectDefinition(OfferMutationOperation.Replace, PlayableKind.Unit, OfferSlotSelection.Rightmost)]);
        var actions = new ActionCatalog([add, remove, replace]);
        var rules = new PreparationRules(10, 0, 10, 0, 1, 0, 7, 10, 2, [4, 4], [5], [1, 1]);
        var pool = new UnitPool(units, [new UnitPoolEntry(a.Id, 20), new UnitPoolEntry(b.Id, 20)]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, actions);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];
        Assert.True(engine.Execute(match, new FreezeOfferCommand(player.Id)).Succeeded);
        var initialCount = player.Offer.Count;

        PlayOfferedAction(engine, match, player, add.Id);
        Assert.Equal(initialCount + 1, player.Offer.Count);
        Assert.False(player.IsOfferFrozen);

        player.ReplaceActionOffer([remove]);
        PlayOfferedAction(engine, match, player, remove.Id);
        Assert.Equal(initialCount, player.Offer.Count);
        Assert.False(player.IsOfferFrozen);

        var beforeReplace = player.Offer.Select(unit => unit.Id).ToArray();
        player.ReplaceActionOffer([replace]);
        PlayOfferedAction(engine, match, player, replace.Id);
        Assert.Equal(initialCount, player.Offer.Count);
        Assert.False(player.IsOfferFrozen);
        Assert.Equal(beforeReplace.Length, player.Offer.Count);
    }

    [Fact]
    public void ReturnUnitToReserve_PreservesUnitAndAllowsOnPlayAgainWithoutRelease()
    {
        var unit = new UnitDefinition(
            new UnitId("recall-target"),
            "Recall Target",
            1,
            2,
            2,
            triggers:
            [
                new TriggerDefinition(NativeTriggerKeys.OnPlay, [new AddResourceEffectDefinition(1)]),
                new TriggerDefinition(NativeTriggerKeys.OnRelease, [new AddResourceEffectDefinition(5)]),
            ]);
        var retreat = new ActionDefinition(
            new ActionId("retreat"),
            "Retreat",
            1,
            0,
            [new ReturnUnitToReserveEffectDefinition(new EffectTargetSelector(EffectTargetScope.Selected))]);
        var units = new UnitCatalog([unit]);
        var actions = new ActionCatalog([retreat]);
        var rules = new PreparationRules(0, 0, 10, 0, 1, 0, 7, 10, 2, [1, 1], [5], [1, 1]);
        var pool = new UnitPool(units, [new UnitPoolEntry(unit.Id, 10)]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), units, null, null, actions);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));
        engine.BeginPreparation(match);
        var player = match.Players[0];

        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, 0)).Succeeded);
        var pooled = Assert.Single(player.Reserve);
        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        Assert.Equal(1, player.Resource);
        var poolCopiesBeforeReturn = pool.GetAvailableCopies(unit.Id);

        var actionEntry = player.PlayableOffer.Single(entry => entry.Kind == PlayableKind.Action);
        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, actionEntry.Slot)).Succeeded);
        var actionReserveSlot = player.PlayableReserve.Single(entry => entry.Kind == PlayableKind.Action).Slot;
        Assert.True(engine.Execute(match, new PlayActionCommand(player.Id, actionReserveSlot, pooled.Id)).Succeeded);

        Assert.Empty(player.Field);
        Assert.Same(pooled, Assert.Single(player.Reserve));
        Assert.Equal(poolCopiesBeforeReturn, pool.GetAvailableCopies(unit.Id));
        Assert.Equal(1, player.Resource);

        Assert.True(engine.Execute(match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        Assert.Same(pooled, Assert.Single(player.Field));
        Assert.Equal(2, player.Resource);
    }

    private static void PlayOfferedAction(PreparationEngine engine, MatchState match, PlayerState player, ActionId actionId)
    {
        var entry = player.PlayableOffer.Single(value => value.Kind == PlayableKind.Action && value.Action!.Id == actionId);
        Assert.True(engine.Execute(match, new AcquirePlayableCommand(player.Id, entry.Slot)).Succeeded);
        var reserveSlot = player.PlayableReserve.Single(value => value.Kind == PlayableKind.Action && value.Action!.Definition.Id == actionId).Slot;
        Assert.True(engine.Execute(match, new PlayActionCommand(player.Id, reserveSlot)).Succeeded);
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
