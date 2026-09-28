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
