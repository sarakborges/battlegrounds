using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Taxonomy;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Preparation;

public sealed class PreparationEffectExecutionTests
{
    [Fact]
    public void Deploy_OnPlayEffectsMutateAuthoritativeUnitAndResource()
    {
        var played = new UnitDefinition(
            new UnitId("played"),
            "Played",
            1,
            1,
            2,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [
                        new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Self), 2, 3),
                        new AddResourceEffectDefinition(2),
                    ]),
            ]);

        var setup = CreateStartedMatch([played], [played]);
        var player = setup.Match.Players[0];

        AcquireById(setup.Engine, setup.Match, player.Id, played.Id);
        Assert.Equal(0, player.Resource);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        var unit = Assert.Single(player.Field);
        Assert.Equal(3, unit.Attack);
        Assert.Equal(5, unit.Health);
        Assert.Equal(2, player.Resource);
    }

    [Fact]
    public void DamageDeathAndSummon_AreProcessedInFifoOrderAndFreeFieldSlot()
    {
        var token = new UnitDefinition(new UnitId("token"), "Token", 1, 1, 1);
        var victim = new UnitDefinition(
            new UnitId("victim"),
            "Victim",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDeath,
                    [new SummonUnitEffectDefinition(token.Id)]),
            ]);
        var source = new UnitDefinition(
            new UnitId("source"),
            "Source",
            1,
            1,
            5,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [new DealDamageEffectDefinition(new EffectTargetSelector(EffectTargetScope.Friendly), 1)]),
            ]);

        var setup = CreateStartedMatch([victim, source, token], [victim, source], fieldCapacity: 2, startingResource: 10, acquireCost: 0);
        var player = setup.Match.Players[0];

        AcquireById(setup.Engine, setup.Match, player.Id, victim.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        AcquireById(setup.Engine, setup.Match, player.Id, source.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        Assert.Equal(2, player.Field.Count);
        Assert.DoesNotContain(player.Field, unit => unit.Definition.Id == victim.Id);
        var generated = Assert.Single(player.Field, unit => unit.Definition.Id == token.Id);
        Assert.Equal(UnitInstanceOrigin.Generated, generated.Origin);
        Assert.Equal(1, setup.Pool.GetAvailableCopies(victim.Id));
    }

    [Fact]
    public void TriggerEvent_CanActivateFriendlyOnDeathWithoutDestroyingItDuringPreparation()
    {
        var token = new UnitDefinition(new UnitId("token"), "Token", 1, 1, 1);
        var victim = new UnitDefinition(
            new UnitId("victim"),
            "Victim",
            1,
            1,
            3,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDeath,
                    [new SummonUnitEffectDefinition(token.Id)]),
            ]);
        var source = new UnitDefinition(
            new UnitId("source"),
            "Source",
            1,
            1,
            3,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [
                        new TriggerEventEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Friendly),
                            NativeTriggerKeys.OnDeath),
                    ]),
            ]);

        var setup = CreateStartedMatch([victim, source, token], [victim, source], startingResource: 10, acquireCost: 0);
        var player = setup.Match.Players[0];
        AcquireById(setup.Engine, setup.Match, player.Id, victim.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        AcquireById(setup.Engine, setup.Match, player.Id, source.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        Assert.Contains(player.Field, unit => unit.Definition.Id == victim.Id);
        Assert.Contains(player.Field, unit => unit.Definition.Id == token.Id);
    }

    [Fact]
    public void DestroyUnit_CanKillFilteredFriendlyAndResolveOnDeathDuringPreparation()
    {
        var sacrificeTag = new TagDefinition(new TagId("sacrifice"), "Sacrifice");
        var token = new UnitDefinition(new UnitId("token"), "Token", 1, 1, 1);
        var victim = new UnitDefinition(
            new UnitId("victim"),
            "Victim",
            1,
            1,
            3,
            tags: [sacrificeTag],
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnDeath,
                    [new SummonUnitEffectDefinition(token.Id)]),
            ]);
        var source = new UnitDefinition(
            new UnitId("source"),
            "Source",
            1,
            1,
            3,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [
                        new DestroyUnitEffectDefinition(
                            new EffectTargetSelector(
                                EffectTargetScope.Friendly,
                                requiredTagId: sacrificeTag.Id)),
                    ]),
            ]);

        var setup = CreateStartedMatch([victim, source, token], [victim, source], startingResource: 10, acquireCost: 0);
        var player = setup.Match.Players[0];
        AcquireById(setup.Engine, setup.Match, player.Id, victim.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        AcquireById(setup.Engine, setup.Match, player.Id, source.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        Assert.DoesNotContain(player.Field, unit => unit.Definition.Id == victim.Id);
        Assert.Contains(player.Field, unit => unit.Definition.Id == source.Id);
        Assert.Contains(player.Field, unit => unit.Definition.Id == token.Id);
        Assert.Equal(1, setup.Pool.GetAvailableCopies(victim.Id));
    }

    [Fact]
    public void OnPlayAddBehavior_PersistsIntoCombatSnapshot()
    {
        var ward = new BehaviorDefinition(
            new BehaviorId("ward"),
            "Ward",
            NativeBehaviorKeys.DamageBarrier);
        var behaviorCatalog = new BehaviorCatalog([ward]);
        var played = new UnitDefinition(
            new UnitId("played"),
            "Played",
            1,
            1,
            2,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [
                        new AddBehaviorEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Self),
                            ward.Id),
                    ]),
            ]);

        var setup = CreateStartedMatch([played], [played], behaviorCatalog: behaviorCatalog);
        var player = setup.Match.Players[0];
        AcquireById(setup.Engine, setup.Match, player.Id, played.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        var unit = Assert.Single(player.Field);
        Assert.Equal(ward.Id, Assert.Single(unit.Behaviors).Id);

        var input = CombatInput.FromFields(player.Id, player.Field, setup.Match.Players[1].Id, setup.Match.Players[1].Field);
        Assert.Equal(ward.Id, Assert.Single(input.Left.Units[0].Behaviors).Id);
    }

    [Fact]
    public void GeneratedUnit_ReleaseDoesNotReturnCopyToPool()
    {
        var token = new UnitDefinition(new UnitId("token"), "Token", 1, 1, 1);
        var source = new UnitDefinition(
            new UnitId("source"),
            "Source",
            1,
            1,
            2,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [new SummonUnitEffectDefinition(token.Id)]),
            ]);

        var setup = CreateStartedMatch([source, token], [source], startingResource: 10, acquireCost: 0);
        var player = setup.Match.Players[0];
        AcquireById(setup.Engine, setup.Match, player.Id, source.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        var tokenIndex = player.Field
            .Select((unit, index) => (unit, index))
            .Single(pair => pair.unit.Definition.Id == token.Id)
            .index;
        Assert.True(setup.Engine.Execute(setup.Match, new ReleaseUnitCommand(player.Id, tokenIndex)).Succeeded);
        Assert.Equal(0, setup.Pool.GetAvailableCopies(token.Id));
    }

    [Fact]
    public void Acquire_OnAcquireRunsAfterUnitEntersReserveAndPaymentIsApplied()
    {
        var acquired = new UnitDefinition(
            new UnitId("acquired"),
            "Acquired",
            1,
            1,
            2,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnAcquire,
                    [
                        new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Self), 2, 3),
                        new AddResourceEffectDefinition(1),
                    ]),
            ]);

        var setup = CreateStartedMatch([acquired], [acquired]);
        var player = setup.Match.Players[0];

        AcquireById(setup.Engine, setup.Match, player.Id, acquired.Id);

        var unit = Assert.Single(player.Reserve);
        Assert.Equal(acquired.Id, unit.Definition.Id);
        Assert.Equal(3, unit.Attack);
        Assert.Equal(5, unit.Health);
        Assert.Equal(1, player.Resource);
        Assert.Empty(player.Field);
    }

    [Fact]
    public void Release_OnReleaseRunsAfterSourceLeavesFieldAndBaseRewardIsApplied()
    {
        var released = new UnitDefinition(
            new UnitId("released"),
            "Released",
            1,
            2,
            2,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnRelease,
                    [
                        new ModifyStatsEffectDefinition(new EffectTargetSelector(EffectTargetScope.Friendly), 1, 2),
                        new AddResourceEffectDefinition(new SourceStatEffectValueExpression(EffectStat.Attack)),
                    ]),
            ]);
        var ally = new UnitDefinition(new UnitId("ally"), "Ally", 1, 1, 1);

        var setup = CreateStartedMatch([released, ally], [released, ally], startingResource: 0, acquireCost: 0);
        var player = setup.Match.Players[0];
        AcquireById(setup.Engine, setup.Match, player.Id, released.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        AcquireById(setup.Engine, setup.Match, player.Id, ally.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        var releasedUnit = player.Field.Single(unit => unit.Definition.Id == released.Id);
        var releasedIndex = Enumerable.Range(0, player.Field.Count).Single(index => player.Field[index].Id == releasedUnit.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new ReleaseUnitCommand(player.Id, releasedIndex)).Succeeded);

        var remaining = Assert.Single(player.Field);
        Assert.Equal(ally.Id, remaining.Definition.Id);
        Assert.Equal(2, remaining.Attack);
        Assert.Equal(3, remaining.Health);
        Assert.Equal(2, releasedUnit.Attack);
        Assert.Equal(2, releasedUnit.Health);
        Assert.Equal(3, player.Resource);
        Assert.Equal(1, setup.Pool.GetAvailableCopies(released.Id));
    }

    [Fact]
    public void EndPreparation_TurnEndPendingChoiceCannotMarkPlayerReady()
    {
        var option = new UnitDefinition(new UnitId("option"), "Option", 1, 1, 1);
        var source = new UnitDefinition(
            new UnitId("source"),
            "Source",
            1,
            1,
            2,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnTurnEnd,
                    [new GenerateUnitChoiceEffectDefinition(new UnitDefinitionQuery(excludeSource: true), optionCount: 1)]),
            ]);

        var setup = CreateStartedMatch([source, option], [source, option], startingResource: 10, acquireCost: 0);
        var player = setup.Match.Players[0];
        AcquireById(setup.Engine, setup.Match, player.Id, source.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        var error = Assert.Throws<InvalidOperationException>(() =>
            setup.Engine.Execute(setup.Match, new EndPreparationCommand(player.Id)));

        Assert.Contains("pending choice", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(player.PendingChoice);
        Assert.False(player.IsReadyForCombat);
        Assert.Equal(MatchPhase.Preparation, setup.Match.Phase);
    }

    [Fact]
    public void OnPlay_AdjustUpgradeCostMutatesCurrentPlayerEconomy()
    {
        var source = new UnitDefinition(
            new UnitId("economist"),
            "Economist",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [new AdjustUpgradeCostEffectDefinition(-2)]),
            ]);

        var setup = CreateStartedMatch([source], [source], startingResource: 10, acquireCost: 0);
        var player = setup.Match.Players[0];
        var before = Assert.IsType<int>(player.UpgradeCost);
        AcquireById(setup.Engine, setup.Match, player.Id, source.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);
        Assert.Equal(Math.Max(0, before - 2), player.UpgradeCost);
    }

    [Fact]
    public void OnPlay_RefreshOfferEffectRerollsWithoutRefreshCostAndClearsFreeze()
    {
        var source = new UnitDefinition(
            new UnitId("refresh-source"),
            "Refresh Source",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(NativeTriggerKeys.OnPlay, [new RefreshOfferEffectDefinition()]),
            ]);
        var a = new UnitDefinition(new UnitId("a"), "A", 1, 1, 1);
        var b = new UnitDefinition(new UnitId("b"), "B", 1, 1, 1);
        var c = new UnitDefinition(new UnitId("c"), "C", 1, 1, 1);

        var setup = CreateStartedMatch([source, a, b, c], [source, a, b, c], startingResource: 10, acquireCost: 0);
        var player = setup.Match.Players[0];
        AcquireById(setup.Engine, setup.Match, player.Id, source.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new FreezeOfferCommand(player.Id)).Succeeded);
        var resourceBefore = player.Resource;

        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        Assert.Equal(resourceBefore, player.Resource);
        Assert.False(player.IsOfferFrozen);
        Assert.Equal(3, player.PlayableOffer.Count);
    }

    [Fact]
    public void AcquireDiscount_AppliesToNextUnitAndOnAcquireCanGrantTheFollowingDiscount()
    {
        var grant = new UnitDefinition(
            new UnitId("grant"),
            "Grant",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(NativeTriggerKeys.OnAcquire, [new AddAcquireDiscountEffectDefinition(2)]),
            ]);
        var plain = new UnitDefinition(new UnitId("plain"), "Plain", 1, 1, 1);
        var setup = CreateStartedMatch([grant, plain], [grant, plain], startingResource: 10, acquireCost: 3);
        var player = setup.Match.Players[0];
        player.AddAcquireDiscount(1);

        AcquireById(setup.Engine, setup.Match, player.Id, grant.Id);
        Assert.Equal(8, player.Resource);
        Assert.Equal(2, player.NextAcquireDiscount);

        AcquireById(setup.Engine, setup.Match, player.Id, plain.Id);
        Assert.Equal(7, player.Resource);
        Assert.Equal(0, player.NextAcquireDiscount);
    }

    [Fact]
    public void TierUpgrade_CountedPreparationEventCanQueueUnitChoiceReward()
    {
        var option = new UnitDefinition(new UnitId("milestone-option"), "Milestone Option", 1, 2, 2);
        var source = new UnitDefinition(
            new UnitId("milestone-source"),
            "Milestone Source",
            1,
            1,
            3,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.AfterEventCount,
                    [new GenerateUnitChoiceEffectDefinition(new UnitDefinitionQuery(excludeSource: true), optionCount: 1)],
                    count: 1,
                    counter: new EffectHistoryQuery(NativeGameEventKeys.TierUpgraded, EffectHistoryScope.Match)),
            ]);

        var setup = CreateStartedMatch([source, option], [source, option], startingResource: 10, acquireCost: 0);
        var player = setup.Match.Players[0];
        AcquireById(setup.Engine, setup.Match, player.Id, source.Id);
        Assert.True(setup.Engine.Execute(setup.Match, new DeployUnitCommand(player.Id, 0)).Succeeded);

        Assert.True(setup.Engine.Execute(setup.Match, new UpgradeTierCommand(player.Id)).Succeeded);

        Assert.Equal(2, player.Tier);
        Assert.NotNull(player.PendingChoice);
    }

    private static void AcquireById(
        PreparationEngine engine,
        MatchState match,
        PlayerId playerId,
        UnitId unitId)
    {
        var player = match.Players.Single(candidate => candidate.Id == playerId);
        var slot = player.Offer
            .Select((definition, index) => (definition, index))
            .First(pair => pair.definition.Id == unitId)
            .index;
        Assert.True(engine.Execute(match, new AcquireUnitCommand(playerId, slot)).Succeeded);
    }

    private static Setup CreateStartedMatch(
        IReadOnlyList<UnitDefinition> catalogDefinitions,
        IReadOnlyList<UnitDefinition> pooledDefinitions,
        int fieldCapacity = 7,
        int startingResource = 3,
        int acquireCost = 3,
        BehaviorCatalog? behaviorCatalog = null)
    {
        var catalog = new UnitCatalog(catalogDefinitions);
        var pool = new ScriptedUnitPool(pooledDefinitions);
        var rules = new PreparationRules(
            startingResource,
            resourcePerRound: 0,
            maximumResource: 10,
            acquireCost,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity,
            reserveCapacity: 10,
            maximumTier: 2,
            offerSizesByTier: [pooledDefinitions.Count, pooledDefinitions.Count],
            initialUpgradeCostsByTier: [1]);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 8));
        var engine = new PreparationEngine(
            rules,
            pool,
            new SeededRandomSource(7),
            catalog,
            behaviorCatalog ?? new BehaviorCatalog(Array.Empty<BehaviorDefinition>()));
        engine.BeginPreparation(match);
        return new Setup(match, engine, pool);
    }

    private sealed record Setup(MatchState Match, PreparationEngine Engine, ScriptedUnitPool Pool);

    private sealed class ScriptedUnitPool : IUnitPool
    {
        private readonly UnitDefinition[] _offer;
        private readonly Dictionary<UnitId, int> _returned = [];

        public ScriptedUnitPool(IEnumerable<UnitDefinition> offer)
        {
            _offer = offer.ToArray();
        }

        public IReadOnlyList<UnitDefinition> DrawOffer(int maximumTier, int count, IRandomSource randomSource) =>
            _offer.Take(count).ToArray();

        public IReadOnlyList<UnitDefinition> ExchangeOffer(
            IReadOnlyCollection<UnitDefinition> returnedUnits,
            int maximumTier,
            int count,
            IRandomSource randomSource) =>
            _offer.Take(count).ToArray();

        public void ReturnUnit(UnitDefinition definition)
        {
            _returned[definition.Id] = GetAvailableCopies(definition.Id) + 1;
        }

        public int GetAvailableCopies(UnitId unitId) =>
            _returned.TryGetValue(unitId, out var copies) ? copies : 0;
    }
}
