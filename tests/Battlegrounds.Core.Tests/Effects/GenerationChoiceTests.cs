using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Taxonomy;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Effects;

public sealed class GenerationChoiceTests
{
    [Fact]
    public void Runtime_GeneratesUniqueFilteredChoiceOptionsDeterministically()
    {
        var organic = new UnitTypeDefinition(new UnitTypeId("organic"), "Organic");
        var source = new UnitDefinition(
            new UnitId("source"),
            "Source",
            1,
            1,
            1,
            types: [organic],
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [
                        new GenerateUnitChoiceEffectDefinition(
                            new UnitDefinitionQuery(requiredTypeId: organic.Id, excludeSource: true),
                            optionCount: 2),
                    ]),
            ]);
        var alpha = new UnitDefinition(new UnitId("alpha"), "Alpha", 1, 1, 1, types: [organic]);
        var beta = new UnitDefinition(new UnitId("beta"), "Beta", 1, 1, 1, types: [organic]);
        var gamma = new UnitDefinition(new UnitId("gamma"), "Gamma", 1, 1, 1, types: [organic]);
        var other = new UnitDefinition(new UnitId("other"), "Other", 1, 1, 1);
        var catalog = new UnitCatalog([source, alpha, beta, gamma, other]);
        var runtimeSource = new FakeUnit(new UnitInstanceId(1), new PlayerId(0), source);
        var world = new FakeWorld(runtimeSource);
        var runtime = new GameEffectRuntime(world, new MinimumRandomSource(), catalog, null);

        runtime.Process(new GameEffectEvent(NativeTriggerKeys.OnPlay, runtimeSource));

        Assert.Collection(
            world.ChoiceOptions,
            option => Assert.Equal(alpha.Id, option.Id),
            option => Assert.Equal(beta.Id, option.Id));
        Assert.DoesNotContain(world.ChoiceOptions, option => option.Id == source.Id);
        Assert.All(world.ChoiceOptions, option => Assert.Contains(option.Types, type => type.Id == organic.Id));
    }

    [Fact]
    public void Preparation_BlocksOtherCommandsUntilCurrentChoiceIsResolved()
    {
        var poolUnit = new UnitDefinition(new UnitId("pool-unit"), "Pool Unit", 1, 1, 1);
        var generated = new UnitDefinition(new UnitId("generated"), "Generated", 1, 2, 2);
        var catalog = new UnitCatalog([poolUnit, generated]);
        var rules = new PreparationRules(
            startingResource: 3,
            resourcePerRound: 1,
            maximumResource: 10,
            acquireCost: 3,
            releaseValue: 1,
            refreshCost: 1,
            fieldCapacity: 7,
            reserveCapacity: 2,
            maximumTier: 2,
            offerSizesByTier: [1, 1],
            initialUpgradeCostsByTier: [5]);
        var pool = new UnitPool(catalog, [new UnitPoolEntry(poolUnit.Id, 10)]);
        var engine = new PreparationEngine(rules, pool, new MinimumRandomSource(), catalog, null);
        var match = MatchState.Create([new PlayerId(0), new PlayerId(1)], new MatchRules(2, 2));

        engine.BeginPreparation(match);
        var player = match.Players.Single(value => value.Id == new PlayerId(0));
        Assert.True(player.QueueUnitChoice([generated]));
        var choice = Assert.IsType<Battlegrounds.Core.Domain.Choices.PendingUnitChoice>(player.PendingChoice);

        var blocked = engine.Execute(match, new RefreshOfferCommand(player.Id));
        Assert.False(blocked.Succeeded);
        Assert.Equal(PreparationFailureCode.PendingChoiceMustBeResolved, blocked.FailureCode);

        var resolved = engine.Execute(match, new ResolveUnitChoiceCommand(player.Id, choice.Id, 0));

        Assert.True(resolved.Succeeded);
        Assert.Null(player.PendingChoice);
        var reserveUnit = Assert.Single(player.Reserve);
        Assert.Equal(generated.Id, reserveUnit.Definition.Id);
        Assert.Equal(UnitInstanceOrigin.Generated, reserveUnit.Origin);
    }

    private sealed class FakeUnit : IEffectRuntimeUnit
    {
        public UnitInstanceId InstanceId { get; }
        public PlayerId OwnerPlayerId { get; }
        public UnitDefinition Definition { get; }
        public bool IsAlive => true;

        public FakeUnit(UnitInstanceId instanceId, PlayerId ownerPlayerId, UnitDefinition definition)
        {
            InstanceId = instanceId;
            OwnerPlayerId = ownerPlayerId;
            Definition = definition;
        }
    }

    private sealed class FakeWorld : IEffectRuntimeWorld, IGenerationChoiceRuntimeWorld
    {
        private readonly FakeUnit _source;
        private readonly EffectHistoryState _history = new();

        public IReadOnlyList<UnitDefinition> ChoiceOptions { get; private set; } = [];
        public IReadOnlyList<IEffectRuntimeUnit> Units => [_source];

        public FakeWorld(FakeUnit source)
        {
            _source = source;
            _history.BeginTurn();
        }

        public bool TryGetUnit(UnitInstanceId instanceId, out IEffectRuntimeUnit unit)
        {
            unit = _source;
            return instanceId == _source.InstanceId;
        }

        public IReadOnlyList<IEffectRuntimeUnit> GetHistoryEventListeners(PlayerId playerId) => [_source];
        public void ModifyStats(IEffectRuntimeUnit unit, int attackDelta, int healthDelta) => throw new NotSupportedException();
        public bool TryConsumeBehavior(IEffectRuntimeUnit unit, NativeBehaviorKey handler) => false;
        public bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior) => false;
        public bool RemoveBehavior(IEffectRuntimeUnit unit, BehaviorId behaviorId) => false;
        public void TakeDamage(IEffectRuntimeUnit unit, int amount) => throw new NotSupportedException();
        public void Destroy(IEffectRuntimeUnit unit) => throw new NotSupportedException();
        public IReadOnlyList<IEffectRuntimeUnit> Summon(IEffectRuntimeUnit source, UnitDefinition definition, int count) => [];
        public int GenerateUnitToReserve(PlayerId playerId, UnitDefinition definition, int count) => count;

        public bool QueueUnitChoice(PlayerId playerId, IReadOnlyList<UnitDefinition> options)
        {
            ChoiceOptions = options.ToArray();
            return true;
        }

        public void AdjustResource(PlayerId playerId, int amount) { }
        public EffectHistorySnapshot GetHistory(PlayerId playerId) => _history.Snapshot();
        public void RecordEvent(PlayerId playerId, NativeGameEventKey @event, UnitDefinition? unit = null) => _history.RecordEvent(@event, unit);
        public int GetTriggerActivationCount(PlayerId playerId, EffectSourceKey source, int triggerIndex, EffectHistoryScope scope) => 0;
        public void RecordTriggerActivation(PlayerId playerId, EffectSourceKey source, int triggerIndex, EffectHistoryScope scope) { }
        public IReadOnlyList<IEffectRuntimeUnit> ExtractDeadUnits() => [];
        public IEffectRuntimeUnit? TryRevive(IEffectRuntimeUnit deadUnit) => null;
        public void FinalizeDeath(IEffectRuntimeUnit deadUnit) { }
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
