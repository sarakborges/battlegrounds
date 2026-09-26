using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Effects;

internal interface IEffectRuntimeUnit
{
    UnitInstanceId InstanceId { get; }
    PlayerId OwnerPlayerId { get; }
    UnitDefinition Definition { get; }
    EffectSourceKey SourceKey => EffectSourceKey.ForUnit(InstanceId);
    int Attack => Definition.BaseAttack;
    int Health => Definition.BaseHealth;
    bool IsAlive { get; }
}

internal interface IEffectRuntimeWorld
{
    IReadOnlyList<IEffectRuntimeUnit> Units { get; }

    bool TryGetUnit(UnitInstanceId instanceId, out IEffectRuntimeUnit unit);
    void ModifyStats(IEffectRuntimeUnit unit, int attackDelta, int healthDelta);
    bool TryConsumeBehavior(IEffectRuntimeUnit unit, NativeBehaviorKey handler);
    bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior);
    bool RemoveBehavior(IEffectRuntimeUnit unit, BehaviorId behaviorId);
    void TakeDamage(IEffectRuntimeUnit unit, int amount);
    void Destroy(IEffectRuntimeUnit unit);
    IReadOnlyList<IEffectRuntimeUnit> Summon(
        IEffectRuntimeUnit source,
        UnitDefinition definition,
        int count);
    void AdjustResource(PlayerId playerId, int amount);
    void SetPower(PlayerId playerId, PowerId powerId) =>
        throw new InvalidOperationException("This effect world does not support persistent power changes.");
    EffectHistorySnapshot GetHistory(PlayerId playerId);
    void RecordEvent(PlayerId playerId, NativeGameEventKey @event, UnitDefinition? unit = null);
    int GetTriggerActivationCount(
        PlayerId playerId,
        EffectSourceKey source,
        int triggerIndex,
        EffectHistoryScope scope);
    void RecordTriggerActivation(
        PlayerId playerId,
        EffectSourceKey source,
        int triggerIndex,
        EffectHistoryScope scope);
    IReadOnlyList<IEffectRuntimeUnit> ExtractDeadUnits();
    IEffectRuntimeUnit? TryRevive(IEffectRuntimeUnit deadUnit);
    void FinalizeDeath(IEffectRuntimeUnit deadUnit);
}

internal sealed record GameEffectEvent(
    NativeTriggerKey Event,
    IEffectRuntimeUnit Subject);

internal sealed class GameEffectRuntime
{
    private const int MaximumProcessedEvents = 10_000;

    private readonly IEffectRuntimeWorld _world;
    private readonly IRandomSource _randomSource;
    private readonly UnitCatalog? _unitCatalog;
    private readonly BehaviorCatalog? _behaviorCatalog;
    private readonly EffectPipeline _pipeline = new();
    private readonly Dictionary<(UnitInstanceId InstanceId, int TriggerIndex), int> _triggerProgress = [];
    private int _processedEvents;

    public GameEffectRuntime(
        IEffectRuntimeWorld world,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog,
        BehaviorCatalog? behaviorCatalog)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _unitCatalog = unitCatalog;
        _behaviorCatalog = behaviorCatalog;
    }

    public void Process(GameEffectEvent effectEvent) => Process([effectEvent]);

    public void Process(IEnumerable<GameEffectEvent> initialEvents)
    {
        ArgumentNullException.ThrowIfNull(initialEvents);
        _processedEvents = 0;

        var queue = new Queue<GameEffectEvent>();
        foreach (var effectEvent in initialEvents)
        {
            ValidateEvent(effectEvent);
            queue.Enqueue(effectEvent);
        }

        DrainEventQueue(queue);
        ResolveDeathWaves();
    }

    public void ProcessTrigger(
        IEffectRuntimeUnit source,
        TriggerDefinition trigger,
        UnitInstanceId? selectedTargetInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(trigger);
        _processedEvents = 0;

        var triggerIndex = FindTriggerIndex(source.Definition, trigger);
        var queue = new Queue<GameEffectEvent>();
        ResolveSpecificTrigger(source, triggerIndex, trigger, queue, selectedTargetInstanceId);
        DrainEventQueue(queue);
        ResolveDeathWaves();
    }

    private void DrainEventQueue(Queue<GameEffectEvent> queue)
    {
        while (queue.Count > 0)
        {
            CountProcessedEvent();
            var effectEvent = queue.Dequeue();
            foreach (var listener in ResolveListeners(effectEvent))
            {
                ResolveListener(effectEvent, listener, queue);
            }
        }
    }

    private IReadOnlyList<IEffectRuntimeUnit> ResolveListeners(GameEffectEvent effectEvent)
    {
        if (effectEvent.Event == NativeTriggerKeys.OnSummon)
        {
            return _world.Units
                .Where(unit => unit.OwnerPlayerId == effectEvent.Subject.OwnerPlayerId)
                .OrderBy(unit => unit.InstanceId.Value)
                .ToArray();
        }

        return [effectEvent.Subject];
    }

    private void ResolveListener(
        GameEffectEvent effectEvent,
        IEffectRuntimeUnit listener,
        Queue<GameEffectEvent> queue)
    {
        if (effectEvent.Event == NativeTriggerKeys.AfterFriendlyDeaths)
        {
            ResolveCountedDeathTriggers(listener, queue);
            return;
        }

        for (var index = 0; index < listener.Definition.Triggers.Count; index++)
        {
            var trigger = listener.Definition.Triggers[index];
            if (trigger.Event == effectEvent.Event)
            {
                ResolveSpecificTrigger(listener, index, trigger, queue);
            }
        }
    }

    private void ResolveSpecificTrigger(
        IEffectRuntimeUnit listener,
        int triggerIndex,
        TriggerDefinition trigger,
        Queue<GameEffectEvent> queue,
        UnitInstanceId? selectedTargetInstanceId = null)
    {
        if (trigger.ActivationLimit is TriggerActivationLimit activationLimit &&
            _world.GetTriggerActivationCount(
                listener.OwnerPlayerId,
                listener.SourceKey,
                triggerIndex,
                activationLimit.Scope) >= activationLimit.Maximum)
        {
            return;
        }

        var context = BuildContext(listener, selectedTargetInstanceId);
        var resolvedEffects = _pipeline.ResolveTrigger(
            listener.Definition,
            trigger,
            context,
            _randomSource);

        if (resolvedEffects.Count == 0)
        {
            return;
        }

        if (trigger.ActivationLimit is TriggerActivationLimit limit)
        {
            _world.RecordTriggerActivation(
                listener.OwnerPlayerId,
                listener.SourceKey,
                triggerIndex,
                limit.Scope);
        }

        foreach (var resolved in resolvedEffects)
        {
            ApplyEffect(listener, resolved, queue);
        }
    }

    private void ResolveCountedDeathTriggers(
        IEffectRuntimeUnit listener,
        Queue<GameEffectEvent> queue)
    {
        for (var index = 0; index < listener.Definition.Triggers.Count; index++)
        {
            var trigger = listener.Definition.Triggers[index];
            if (trigger.Event != NativeTriggerKeys.AfterFriendlyDeaths)
            {
                continue;
            }

            if (AdvanceTrigger(listener.InstanceId, index, trigger.Count!.Value))
            {
                ResolveSpecificTrigger(listener, index, trigger, queue);
            }
        }
    }

    private void ApplyEffect(
        IEffectRuntimeUnit source,
        ResolvedEffect resolved,
        Queue<GameEffectEvent> queue)
    {
        switch (resolved.Definition)
        {
            case ModifyStatsEffectDefinition modifyStats:
            {
                var context = BuildContext(source);
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    var attackDelta = _pipeline.EvaluateValue(modifyStats.AttackDelta, context, target.InstanceId);
                    var healthDelta = _pipeline.EvaluateValue(modifyStats.HealthDelta, context, target.InstanceId);
                    if (attackDelta != 0 || healthDelta != 0)
                    {
                        _world.ModifyStats(target, attackDelta, healthDelta);
                    }
                }
                break;
            }

            case DealDamageEffectDefinition dealDamage:
            {
                var context = BuildContext(source);
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    var amount = _pipeline.EvaluateValue(dealDamage.Amount, context, target.InstanceId);
                    if (amount <= 0)
                    {
                        continue;
                    }
                    if (_world.TryConsumeBehavior(target, NativeBehaviorKeys.DamageBarrier))
                    {
                        continue;
                    }

                    _world.TakeDamage(target, amount);
                    _world.RecordEvent(target.OwnerPlayerId, NativeGameEventKeys.UnitDamaged, target.Definition);
                    if (_world.TryConsumeBehavior(source, NativeBehaviorKeys.LethalFirstDamagePerCombat))
                    {
                        _world.Destroy(target);
                    }

                    queue.Enqueue(new GameEffectEvent(NativeTriggerKeys.OnDamage, target));
                }
                break;
            }

            case DestroyUnitEffectDefinition:
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    _world.Destroy(target);
                }
                break;

            case TriggerEventEffectDefinition triggerEvent:
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    queue.Enqueue(new GameEffectEvent(triggerEvent.Event, target));
                }
                break;

            case SummonUnitEffectDefinition summon:
            {
                var unitCatalog = _unitCatalog
                    ?? throw new InvalidOperationException("summonUnit requires a UnitCatalog in the effect runtime.");
                var count = _pipeline.EvaluateValue(summon.Count, BuildContext(source));
                if (count <= 0)
                {
                    break;
                }
                var definition = unitCatalog.GetRequired(summon.UnitId);
                foreach (var summoned in _world.Summon(source, definition, count))
                {
                    _world.RecordEvent(summoned.OwnerPlayerId, NativeGameEventKeys.UnitSummoned, summoned.Definition);
                    queue.Enqueue(new GameEffectEvent(NativeTriggerKeys.OnSummon, summoned));
                }
                break;
            }

            case AddBehaviorEffectDefinition addBehavior:
            {
                var behaviorCatalog = _behaviorCatalog
                    ?? throw new InvalidOperationException("addBehavior requires a BehaviorCatalog in the effect runtime.");
                var behavior = behaviorCatalog.GetRequired(addBehavior.BehaviorId);
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    _world.AddBehavior(target, behavior);
                }
                break;
            }

            case RemoveBehaviorEffectDefinition removeBehavior:
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    _world.RemoveBehavior(target, removeBehavior.BehaviorId);
                }
                break;

            case AddResourceEffectDefinition addResource:
            {
                var amount = _pipeline.EvaluateValue(addResource.Amount, BuildContext(source));
                if (amount != 0)
                {
                    _world.AdjustResource(source.OwnerPlayerId, amount);
                }
                break;
            }

            case SetPowerEffectDefinition setPower:
                _world.SetPower(source.OwnerPlayerId, setPower.PowerId);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(resolved),
                    resolved.Definition.Kind,
                    "Unsupported effect kind.");
        }
    }

    private IEnumerable<IEffectRuntimeUnit> GetCurrentTargets(
        IEnumerable<UnitInstanceId> targetInstanceIds)
    {
        foreach (var instanceId in targetInstanceIds)
        {
            if (_world.TryGetUnit(instanceId, out var unit))
            {
                yield return unit;
            }
        }
    }

    private void ResolveDeathWaves()
    {
        while (true)
        {
            var deadUnits = _world.ExtractDeadUnits()
                .OrderBy(unit => unit.InstanceId.Value)
                .ToArray();

            if (deadUnits.Length == 0)
            {
                return;
            }

            foreach (var deadUnit in deadUnits)
            {
                ResolveDeath(deadUnit);
            }
        }
    }

    private void ResolveDeath(IEffectRuntimeUnit deadUnit)
    {
        ClearTriggerProgress(deadUnit.InstanceId);
        _world.RecordEvent(deadUnit.OwnerPlayerId, NativeGameEventKeys.UnitDied, deadUnit.Definition);

        var activations = new List<DeathTriggerActivation>();
        AddOwnDeathTriggers(deadUnit, activations);
        AddFriendlyDeathTriggers(deadUnit, activations);

        foreach (var activation in activations
                     .OrderBy(value => value.Listener.InstanceId.Value)
                     .ThenBy(value => value.TriggerIndex))
        {
            var queue = new Queue<GameEffectEvent>();
            ResolveSpecificTrigger(
                activation.Listener,
                activation.TriggerIndex,
                activation.Trigger,
                queue);
            DrainEventQueue(queue);
        }

        var revived = _world.TryRevive(deadUnit);
        if (revived is not null)
        {
            var queue = new Queue<GameEffectEvent>();
            _world.RecordEvent(revived.OwnerPlayerId, NativeGameEventKeys.UnitSummoned, revived.Definition);
            queue.Enqueue(new GameEffectEvent(NativeTriggerKeys.OnSummon, revived));
            DrainEventQueue(queue);
            return;
        }

        _world.FinalizeDeath(deadUnit);
    }

    private static void AddOwnDeathTriggers(
        IEffectRuntimeUnit deadUnit,
        ICollection<DeathTriggerActivation> activations)
    {
        for (var index = 0; index < deadUnit.Definition.Triggers.Count; index++)
        {
            var trigger = deadUnit.Definition.Triggers[index];
            if (trigger.Event == NativeTriggerKeys.OnDeath)
            {
                activations.Add(new DeathTriggerActivation(deadUnit, index, trigger));
            }
        }
    }

    private void AddFriendlyDeathTriggers(
        IEffectRuntimeUnit deadUnit,
        ICollection<DeathTriggerActivation> activations)
    {
        foreach (var listener in _world.Units
                     .Where(unit => unit.OwnerPlayerId == deadUnit.OwnerPlayerId)
                     .OrderBy(unit => unit.InstanceId.Value))
        {
            for (var index = 0; index < listener.Definition.Triggers.Count; index++)
            {
                var trigger = listener.Definition.Triggers[index];
                if (trigger.Event != NativeTriggerKeys.AfterFriendlyDeaths)
                {
                    continue;
                }

                if (AdvanceTrigger(listener.InstanceId, index, trigger.Count!.Value))
                {
                    activations.Add(new DeathTriggerActivation(listener, index, trigger));
                }
            }
        }
    }

    private bool AdvanceTrigger(UnitInstanceId instanceId, int triggerIndex, int threshold)
    {
        var key = (instanceId, triggerIndex);
        var progress = _triggerProgress.GetValueOrDefault(key) + 1;
        if (progress < threshold)
        {
            _triggerProgress[key] = progress;
            return false;
        }

        _triggerProgress[key] = 0;
        return true;
    }

    private void ClearTriggerProgress(UnitInstanceId instanceId)
    {
        foreach (var key in _triggerProgress.Keys
                     .Where(key => key.InstanceId == instanceId)
                     .ToArray())
        {
            _triggerProgress.Remove(key);
        }
    }

    private EffectResolutionContext BuildContext(
        IEffectRuntimeUnit source,
        UnitInstanceId? selectedTargetInstanceId = null)
    {
        var positions = new Dictionary<PlayerId, int>();
        var snapshots = new List<EffectUnitSnapshot>();
        foreach (var unit in _world.Units)
        {
            var position = positions.GetValueOrDefault(unit.OwnerPlayerId);
            positions[unit.OwnerPlayerId] = position + 1;
            snapshots.Add(CreateSnapshot(unit, isSelectable: true, position));
        }

        if (snapshots.All(snapshot => snapshot.InstanceId != source.InstanceId))
        {
            snapshots.Add(CreateSnapshot(source, isSelectable: false, position: -1));
        }

        return new EffectResolutionContext(
            source.InstanceId,
            source.OwnerPlayerId,
            snapshots,
            selectedTargetInstanceId,
            _world.GetHistory(source.OwnerPlayerId));
    }

    private static EffectUnitSnapshot CreateSnapshot(
        IEffectRuntimeUnit unit,
        bool isSelectable,
        int position) =>
        new(
            unit.InstanceId,
            unit.OwnerPlayerId,
            unit.IsAlive,
            unit.Attack,
            unit.Health,
            position,
            isSelectable,
            unit.Definition.Types.Select(type => type.Id),
            unit.Definition.Tags.Select(tag => tag.Id));

    private static int FindTriggerIndex(UnitDefinition definition, TriggerDefinition trigger)
    {
        for (var index = 0; index < definition.Triggers.Count; index++)
        {
            if (ReferenceEquals(definition.Triggers[index], trigger) || definition.Triggers[index] == trigger)
            {
                return index;
            }
        }

        throw new ArgumentException("Trigger does not belong to the source definition.", nameof(trigger));
    }

    private void CountProcessedEvent()
    {
        _processedEvents++;
        if (_processedEvents > MaximumProcessedEvents)
        {
            throw new InvalidOperationException(
                "Effect runtime exceeded its internal safety budget. The mod likely contains a recursive effect loop.");
        }
    }

    private static void ValidateEvent(GameEffectEvent effectEvent)
    {
        ArgumentNullException.ThrowIfNull(effectEvent);
        if (!NativeTriggerKeys.IsSupported(effectEvent.Event))
        {
            throw new ArgumentException($"Unsupported trigger '{effectEvent.Event}'.", nameof(effectEvent));
        }
    }

    private sealed record DeathTriggerActivation(
        IEffectRuntimeUnit Listener,
        int TriggerIndex,
        TriggerDefinition Trigger);
}
