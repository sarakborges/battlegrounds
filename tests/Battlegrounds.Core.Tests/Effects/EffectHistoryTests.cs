using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Taxonomy;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Effects;

public sealed class EffectHistoryTests
{
    [Fact]
    public void History_TracksTurnAndMatchCountsWithTaxonomyFilters()
    {
        var organic = new UnitTypeDefinition(new UnitTypeId("organic"), "Organic");
        var starter = new TagDefinition(new TagId("starter"), "Starter");
        var unit = new UnitDefinition(
            new UnitId("unit"),
            "Unit",
            1,
            1,
            1,
            types: [organic],
            tags: [starter]);
        var history = new EffectHistoryState();

        history.BeginTurn();
        history.RecordEvent(NativeGameEventKeys.UnitAcquired, unit);
        history.RecordEvent(NativeGameEventKeys.UnitAcquired, unit);
        var snapshot = history.Snapshot();

        Assert.Equal(2, snapshot.GetEventCount(new EffectHistoryQuery(
            NativeGameEventKeys.UnitAcquired,
            EffectHistoryScope.Turn)));
        Assert.Equal(2, snapshot.GetEventCount(new EffectHistoryQuery(
            NativeGameEventKeys.UnitAcquired,
            EffectHistoryScope.Match,
            organic.Id,
            starter.Id)));
        Assert.Equal(0, snapshot.GetEventCount(new EffectHistoryQuery(
            NativeGameEventKeys.UnitAcquired,
            EffectHistoryScope.Combat)));

        history.BeginTurn();
        snapshot = history.Snapshot();
        Assert.Equal(0, snapshot.GetEventCount(new EffectHistoryQuery(
            NativeGameEventKeys.UnitAcquired,
            EffectHistoryScope.Turn)));
        Assert.Equal(2, snapshot.GetEventCount(new EffectHistoryQuery(
            NativeGameEventKeys.UnitAcquired,
            EffectHistoryScope.Match)));
    }

    [Fact]
    public void Pipeline_EventCountCanParticipateInDynamicValueCondition()
    {
        var history = new EffectHistoryState();
        history.BeginTurn();
        history.RecordEvent(NativeGameEventKeys.UnitAcquired);
        history.RecordEvent(NativeGameEventKeys.UnitAcquired);
        history.RecordEvent(NativeGameEventKeys.UnitAcquired);
        var source = Unit("source");
        var trigger = new TriggerDefinition(
            NativeTriggerKeys.OnTurnEnd,
            [new AddResourceEffectDefinition(1)],
            conditions:
            [
                new ValueConditionDefinition(
                    new EventCountEffectValueExpression(new EffectHistoryQuery(
                        NativeGameEventKeys.UnitAcquired,
                        EffectHistoryScope.Turn)),
                    EffectComparison.GreaterThanOrEqual,
                    new ConstantEffectValueExpression(3)),
            ]);
        source = new UnitDefinition(
            source.Id,
            source.Name,
            source.Tier,
            source.BaseAttack,
            source.BaseHealth,
            triggers: [trigger]);
        var context = new EffectResolutionContext(
            new UnitInstanceId(1),
            new PlayerId(0),
            [new EffectUnitSnapshot(new UnitInstanceId(1), new PlayerId(0), true)],
            selectedTargetInstanceId: null,
            history.Snapshot());

        var resolved = new EffectPipeline().ResolveTrigger(
            source,
            trigger,
            context,
            new MinimumRandomSource());

        Assert.Single(resolved);
        Assert.Equal(3, new EffectPipeline().EvaluateValue(
            new EventCountEffectValueExpression(new EffectHistoryQuery(
                NativeGameEventKeys.UnitAcquired,
                EffectHistoryScope.Turn)),
            context));
    }

    [Fact]
    public void TriggerActivationLimit_PersistsAcrossEventsAndResetsWithTurn()
    {
        var definition = new UnitDefinition(
            new UnitId("listener"),
            "Listener",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnSummon,
                    [new AddResourceEffectDefinition(1)],
                    activationLimit: new TriggerActivationLimit(EffectHistoryScope.Turn, 1)),
            ]);
        var unit = new FakeUnit(new UnitInstanceId(1), new PlayerId(0), definition);
        var world = new FakeWorld(unit);
        var runtime = new GameEffectRuntime(world, new MinimumRandomSource(), null, null);

        runtime.Process(new GameEffectEvent(NativeTriggerKeys.OnSummon, unit));
        runtime.Process(new GameEffectEvent(NativeTriggerKeys.OnSummon, unit));
        Assert.Equal(1, world.Resource);

        world.History.BeginTurn();
        runtime.Process(new GameEffectEvent(NativeTriggerKeys.OnSummon, unit));
        Assert.Equal(2, world.Resource);
    }

    [Fact]
    public void AfterEventCount_FiresAtEachThresholdAndResetsWithTurnScope()
    {
        var counter = new EffectHistoryQuery(
            NativeGameEventKeys.UnitAcquired,
            EffectHistoryScope.Turn);
        var definition = new UnitDefinition(
            new UnitId("listener"),
            "Listener",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.AfterEventCount,
                    [new AddResourceEffectDefinition(1)],
                    count: 2,
                    counter: counter),
            ]);
        var unit = new FakeUnit(new UnitInstanceId(1), new PlayerId(0), definition);
        var world = new FakeWorld(unit);
        var runtime = new GameEffectRuntime(world, new MinimumRandomSource(), null, null);

        runtime.RecordGameEvent(unit.OwnerPlayerId, NativeGameEventKeys.UnitAcquired);
        Assert.Equal(0, world.Resource);
        runtime.RecordGameEvent(unit.OwnerPlayerId, NativeGameEventKeys.UnitAcquired);
        Assert.Equal(1, world.Resource);
        runtime.RecordGameEvent(unit.OwnerPlayerId, NativeGameEventKeys.UnitAcquired);
        Assert.Equal(1, world.Resource);
        runtime.RecordGameEvent(unit.OwnerPlayerId, NativeGameEventKeys.UnitAcquired);
        Assert.Equal(2, world.Resource);

        world.History.BeginTurn();
        runtime.RecordGameEvent(unit.OwnerPlayerId, NativeGameEventKeys.UnitAcquired);
        runtime.RecordGameEvent(unit.OwnerPlayerId, NativeGameEventKeys.UnitAcquired);
        Assert.Equal(3, world.Resource);
    }

    [Fact]
    public void TriggerEvent_OnDeath_DoesNotCountAsAnActualUnitDeath()
    {
        var definition = new UnitDefinition(
            new UnitId("source"),
            "Source",
            1,
            1,
            1,
            triggers:
            [
                new TriggerDefinition(
                    NativeTriggerKeys.OnPlay,
                    [
                        new TriggerEventEffectDefinition(
                            new EffectTargetSelector(EffectTargetScope.Self),
                            NativeTriggerKeys.OnDeath),
                    ]),
                new TriggerDefinition(
                    NativeTriggerKeys.OnDeath,
                    [new AddResourceEffectDefinition(1)]),
            ]);
        var unit = new FakeUnit(new UnitInstanceId(1), new PlayerId(0), definition);
        var world = new FakeWorld(unit);
        var runtime = new GameEffectRuntime(world, new MinimumRandomSource(), null, null);

        runtime.Process(new GameEffectEvent(NativeTriggerKeys.OnPlay, unit));

        Assert.Equal(1, world.Resource);
        Assert.Equal(0, world.History.Snapshot().GetEventCount(new EffectHistoryQuery(
            NativeGameEventKeys.UnitDied,
            EffectHistoryScope.Match)));
    }

    private static UnitDefinition Unit(string id) =>
        new(new UnitId(id), id, 1, 1, 1);

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

    private sealed class FakeWorld : IEffectRuntimeWorld
    {
        private readonly FakeUnit _unit;
        public EffectHistoryState History { get; } = new();
        public int Resource { get; private set; }
        public IReadOnlyList<IEffectRuntimeUnit> Units => [_unit];

        public FakeWorld(FakeUnit unit)
        {
            _unit = unit;
            History.BeginTurn();
        }

        public bool TryGetUnit(UnitInstanceId instanceId, out IEffectRuntimeUnit unit)
        {
            unit = _unit;
            return instanceId == _unit.InstanceId;
        }

        public IReadOnlyList<IEffectRuntimeUnit> GetHistoryEventListeners(PlayerId playerId) => [_unit];
        public void ModifyStats(IEffectRuntimeUnit unit, int attackDelta, int healthDelta) => throw new NotSupportedException();
        public bool TryConsumeBehavior(IEffectRuntimeUnit unit, NativeBehaviorKey handler) => false;
        public bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior) => false;
        public bool RemoveBehavior(IEffectRuntimeUnit unit, BehaviorId behaviorId) => false;
        public void TakeDamage(IEffectRuntimeUnit unit, int amount) => throw new NotSupportedException();
        public void Destroy(IEffectRuntimeUnit unit) => throw new NotSupportedException();
        public IReadOnlyList<IEffectRuntimeUnit> Summon(IEffectRuntimeUnit source, UnitDefinition definition, int count) => [];
        public void AdjustResource(PlayerId playerId, int amount) => Resource += amount;
        public EffectHistorySnapshot GetHistory(PlayerId playerId) => History.Snapshot();
        public void RecordEvent(PlayerId playerId, NativeGameEventKey @event, UnitDefinition? unit = null) => History.RecordEvent(@event, unit);
        public int GetTriggerActivationCount(PlayerId playerId, EffectSourceKey source, int triggerIndex, EffectHistoryScope scope) =>
            History.GetTriggerActivationCount(source, triggerIndex, scope);
        public void RecordTriggerActivation(PlayerId playerId, EffectSourceKey source, int triggerIndex, EffectHistoryScope scope) =>
            History.RecordTriggerActivation(source, triggerIndex, scope);
        public IReadOnlyList<IEffectRuntimeUnit> ExtractDeadUnits() => [];
        public IEffectRuntimeUnit? TryRevive(IEffectRuntimeUnit deadUnit) => null;
        public void FinalizeDeath(IEffectRuntimeUnit deadUnit) { }
    }

    private sealed class MinimumRandomSource : IRandomSource
    {
        public int NextInt(int minInclusive, int maxExclusive) => minInclusive;
    }
}
