using Battlegrounds.Core.Domain.Actions;
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
    IReadOnlyList<IEffectRuntimeUnit> GetHistoryEventListeners(PlayerId playerId);
    void ModifyStats(IEffectRuntimeUnit unit, int attackDelta, int healthDelta);
    bool TryConsumeBehavior(IEffectRuntimeUnit unit, NativeBehaviorKey handler);
    bool AddBehavior(IEffectRuntimeUnit unit, BehaviorDefinition behavior);
    bool RemoveBehavior(IEffectRuntimeUnit unit, BehaviorId behaviorId);
    void TakeDamage(IEffectRuntimeUnit unit, int amount);
    void Destroy(IEffectRuntimeUnit unit);
    IReadOnlyList<IEffectRuntimeUnit> Summon(IEffectRuntimeUnit source, UnitDefinition definition, int count);
    void AdjustResource(PlayerId playerId, int amount);
    void SetPower(PlayerId playerId, PowerId powerId) =>
        throw new InvalidOperationException("This effect world does not support persistent power changes.");
    EffectHistorySnapshot GetHistory(PlayerId playerId);
    void RecordEvent(PlayerId playerId, NativeGameEventKey @event, UnitDefinition? unit = null);
    int GetTriggerActivationCount(PlayerId playerId, EffectSourceKey source, int triggerIndex, EffectHistoryScope scope);
    void RecordTriggerActivation(PlayerId playerId, EffectSourceKey source, int triggerIndex, EffectHistoryScope scope);
    void RecordResolvedTrigger(IEffectRuntimeUnit source, NativeTriggerKey trigger, int triggerIndex)
    {
    }
    IReadOnlyList<IEffectRuntimeUnit> ExtractDeadUnits();
    IEffectRuntimeUnit? TryRevive(IEffectRuntimeUnit deadUnit);
    void FinalizeDeath(IEffectRuntimeUnit deadUnit);
}

internal sealed record GameEffectEvent(NativeTriggerKey Event, IEffectRuntimeUnit Subject);

internal sealed class GameEffectRuntime
{
    private const int MaximumProcessedEvents = 10_000;
    private readonly IEffectRuntimeWorld _world;
    private readonly IRandomSource _randomSource;
    private readonly UnitCatalog? _unitCatalog;
    private readonly BehaviorCatalog? _behaviorCatalog;
    private readonly ActionCatalog? _actionCatalog;
    private readonly EffectPipeline _pipeline = new();
    private readonly Dictionary<(UnitInstanceId InstanceId, int TriggerIndex), int> _triggerProgress = [];
    private int _processedEvents;

    public GameEffectRuntime(
        IEffectRuntimeWorld world,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog,
        BehaviorCatalog? behaviorCatalog,
        ActionCatalog? actionCatalog = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _unitCatalog = unitCatalog;
        _behaviorCatalog = behaviorCatalog;
        _actionCatalog = actionCatalog;
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

    public void ProcessTrigger(IEffectRuntimeUnit source, TriggerDefinition trigger, UnitInstanceId? selectedTargetInstanceId = null)
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

    public void RecordGameEvent(PlayerId playerId, NativeGameEventKey @event, UnitDefinition? unit = null, bool resolveDeaths = true)
    {
        _processedEvents = 0;
        var queue = new Queue<GameEffectEvent>();
        RecordGameEventCore(playerId, @event, unit, queue);
        DrainEventQueue(queue);
        if (resolveDeaths) ResolveDeathWaves();
    }

    private void RecordGameEventCore(PlayerId playerId, NativeGameEventKey @event, UnitDefinition? unit, Queue<GameEffectEvent> queue)
    {
        var before = _world.GetHistory(playerId);
        _world.RecordEvent(playerId, @event, unit);
        var after = _world.GetHistory(playerId);
        foreach (var listener in _world.GetHistoryEventListeners(playerId))
        {
            for (var index = 0; index < listener.Definition.Triggers.Count; index++)
            {
                var trigger = listener.Definition.Triggers[index];
                if (trigger.Event != NativeTriggerKeys.AfterEventCount || trigger.Counter is not EffectHistoryQuery counter || counter.Event != @event)
                    continue;
                var threshold = trigger.Count!.Value;
                if (after.GetEventCount(counter) / threshold > before.GetEventCount(counter) / threshold)
                    ResolveSpecificTrigger(listener, index, trigger, queue);
            }
        }
    }

    private void DrainEventQueue(Queue<GameEffectEvent> queue)
    {
        while (queue.Count > 0)
        {
            CountProcessedEvent();
            var effectEvent = queue.Dequeue();
            foreach (var listener in ResolveListeners(effectEvent)) ResolveListener(effectEvent, listener, queue);
        }
    }

    private IReadOnlyList<IEffectRuntimeUnit> ResolveListeners(GameEffectEvent effectEvent)
    {
        if (effectEvent.Event == NativeTriggerKeys.OnSummon)
            return _world.Units.Where(unit => unit.OwnerPlayerId == effectEvent.Subject.OwnerPlayerId).OrderBy(unit => unit.InstanceId.Value).ToArray();
        return [effectEvent.Subject];
    }

    private void ResolveListener(GameEffectEvent effectEvent, IEffectRuntimeUnit listener, Queue<GameEffectEvent> queue)
    {
        if (effectEvent.Event == NativeTriggerKeys.AfterFriendlyDeaths)
        {
            ResolveCountedDeathTriggers(listener, queue);
            return;
        }
        for (var index = 0; index < listener.Definition.Triggers.Count; index++)
        {
            var trigger = listener.Definition.Triggers[index];
            if (trigger.Event == effectEvent.Event) ResolveSpecificTrigger(listener, index, trigger, queue);
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
            _world.GetTriggerActivationCount(listener.OwnerPlayerId, listener.SourceKey, triggerIndex, activationLimit.Scope) >= activationLimit.Maximum)
            return;

        var context = BuildContext(listener, selectedTargetInstanceId);
        var resolvedEffects = _pipeline.ResolveTrigger(listener.Definition, trigger, context, _randomSource);
        if (resolvedEffects.Count == 0) return;
        _world.RecordResolvedTrigger(listener, trigger.Event, triggerIndex);
        if (trigger.ActivationLimit is TriggerActivationLimit limit)
            _world.RecordTriggerActivation(listener.OwnerPlayerId, listener.SourceKey, triggerIndex, limit.Scope);
        foreach (var resolved in resolvedEffects) ApplyEffect(listener, resolved, queue);
    }

    private void ResolveCountedDeathTriggers(IEffectRuntimeUnit listener, Queue<GameEffectEvent> queue)
    {
        for (var index = 0; index < listener.Definition.Triggers.Count; index++)
        {
            var trigger = listener.Definition.Triggers[index];
            if (trigger.Event != NativeTriggerKeys.AfterFriendlyDeaths) continue;
            if (AdvanceTrigger(listener.InstanceId, index, trigger.Count!.Value)) ResolveSpecificTrigger(listener, index, trigger, queue);
        }
    }

    private void ApplyEffect(IEffectRuntimeUnit source, ResolvedEffect resolved, Queue<GameEffectEvent> queue)
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
                    if (attackDelta != 0 || healthDelta != 0) _world.ModifyStats(target, attackDelta, healthDelta);
                }
                break;
            }
            case DealDamageEffectDefinition dealDamage:
            {
                var context = BuildContext(source);
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    var amount = _pipeline.EvaluateValue(dealDamage.Amount, context, target.InstanceId);
                    if (amount <= 0 || _world.TryConsumeBehavior(target, NativeBehaviorKeys.DamageBarrier)) continue;
                    _world.TakeDamage(target, amount);
                    RecordGameEventCore(target.OwnerPlayerId, NativeGameEventKeys.UnitDamaged, target.Definition, queue);
                    if (_world.TryConsumeBehavior(source, NativeBehaviorKeys.LethalFirstDamagePerCombat)) _world.Destroy(target);
                    queue.Enqueue(new GameEffectEvent(NativeTriggerKeys.OnDamage, target));
                }
                break;
            }
            case DestroyUnitEffectDefinition:
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds)) _world.Destroy(target);
                break;
            case TriggerEventEffectDefinition triggerEvent:
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds)) queue.Enqueue(new GameEffectEvent(triggerEvent.Event, target));
                break;
            case SummonUnitEffectDefinition summon:
            {
                var unitCatalog = RequireUnitCatalog("summonUnit");
                var count = _pipeline.EvaluateValue(summon.Count, BuildContext(source));
                if (count <= 0) break;
                foreach (var summoned in _world.Summon(source, unitCatalog.GetRequired(summon.UnitId), count))
                {
                    RecordGameEventCore(summoned.OwnerPlayerId, NativeGameEventKeys.UnitSummoned, summoned.Definition, queue);
                    queue.Enqueue(new GameEffectEvent(NativeTriggerKeys.OnSummon, summoned));
                }
                break;
            }
            case GenerateUnitToReserveEffectDefinition generate:
            {
                var count = _pipeline.EvaluateValue(generate.Count, BuildContext(source));
                if (count > 0) GetGenerationWorld(generate.Kind).GenerateUnitToReserve(source.OwnerPlayerId, RequireUnitCatalog("generateUnitToReserve").GetRequired(generate.UnitId), count);
                break;
            }
            case GenerateUnitChoiceEffectDefinition choice:
            {
                var candidates = RequireUnitCatalog("generateUnitChoice").All.Where(candidate => choice.Query.Matches(candidate, source.Definition)).ToArray();
                var options = SelectRandomDefinitions(candidates, choice.OptionCount, _randomSource);
                if (options.Count > 0) GetGenerationWorld(choice.Kind).QueueUnitChoice(source.OwnerPlayerId, options);
                break;
            }
            case GenerateActionToReserveEffectDefinition generateAction:
            {
                var count = _pipeline.EvaluateValue(generateAction.Count, BuildContext(source));
                if (count > 0) GetGenerationWorld(generateAction.Kind).GenerateActionToReserve(source.OwnerPlayerId, RequireActionCatalog("generateActionToReserve").GetRequired(generateAction.ActionId), count);
                break;
            }
            case GenerateActionChoiceEffectDefinition actionChoice:
            {
                var candidates = RequireActionCatalog("generateActionChoice").All.Where(actionChoice.Query.Matches).ToArray();
                var options = SelectRandomDefinitions(candidates, actionChoice.OptionCount, _randomSource);
                if (options.Count > 0) GetGenerationWorld(actionChoice.Kind).QueueActionChoice(source.OwnerPlayerId, options);
                break;
            }
            case TransformUnitEffectDefinition transform:
            {
                var mutationWorld = GetPersistentMutationWorld(transform.Kind);
                var definition = RequireUnitCatalog("transformUnit").GetRequired(transform.UnitId);
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds)) mutationWorld.TransformUnit(target, definition);
                break;
            }
            case CopyUnitToReserveEffectDefinition copy:
            {
                var targets = GetCurrentTargets(resolved.TargetInstanceIds).ToArray();
                if (targets.Length > 0) GetPersistentMutationWorld(copy.Kind).CopyUnitsToReserve(source.OwnerPlayerId, targets);
                break;
            }
            case ApplyUnitModifierEffectDefinition modifier:
            {
                var mutationWorld = GetPersistentMutationWorld(modifier.Kind);
                var context = BuildContext(source);
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    var attack = _pipeline.EvaluateValue(modifier.AttackDelta, context, target.InstanceId);
                    var health = _pipeline.EvaluateValue(modifier.HealthDelta, context, target.InstanceId);
                    if (attack != 0 || health != 0) mutationWorld.ApplyModifier(target, modifier.ModifierKey, attack, health);
                }
                break;
            }
            case RemoveUnitModifierEffectDefinition removeModifier:
            {
                var mutationWorld = GetPersistentMutationWorld(removeModifier.Kind);
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds)) mutationWorld.RemoveModifier(target, removeModifier.ModifierKey);
                break;
            }
            case AddBehaviorEffectDefinition addBehavior:
            {
                var behaviorCatalog = _behaviorCatalog ?? throw new InvalidOperationException("addBehavior requires a BehaviorCatalog in the effect runtime.");
                var behavior = behaviorCatalog.GetRequired(addBehavior.BehaviorId);
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds)) _world.AddBehavior(target, behavior);
                break;
            }
            case RemoveBehaviorEffectDefinition removeBehavior:
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds)) _world.RemoveBehavior(target, removeBehavior.BehaviorId);
                break;
            case AddResourceEffectDefinition addResource:
            {
                var amount = _pipeline.EvaluateValue(addResource.Amount, BuildContext(source));
                if (amount != 0) _world.AdjustResource(source.OwnerPlayerId, amount);
                break;
            }
            case SetPowerEffectDefinition setPower:
                _world.SetPower(source.OwnerPlayerId, setPower.PowerId);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resolved), resolved.Definition.Kind, "Unsupported effect kind.");
        }
    }

    private UnitCatalog RequireUnitCatalog(string effect) =>
        _unitCatalog ?? throw new InvalidOperationException($"{effect} requires a UnitCatalog in the effect runtime.");

    private ActionCatalog RequireActionCatalog(string effect) =>
        _actionCatalog ?? throw new InvalidOperationException($"{effect} requires an ActionCatalog in the effect runtime.");

    private IGenerationChoiceRuntimeWorld GetGenerationWorld(NativeEffectKey kind) =>
        _world as IGenerationChoiceRuntimeWorld
        ?? throw new InvalidOperationException($"Effect '{kind}' is not supported by this effect world.");

    private IPersistentUnitMutationWorld GetPersistentMutationWorld(NativeEffectKey kind) =>
        _world as IPersistentUnitMutationWorld
        ?? throw new InvalidOperationException($"Effect '{kind}' is not supported by this effect world.");

    private static IReadOnlyList<T> SelectRandomDefinitions<T>(IReadOnlyList<T> candidates, int count, IRandomSource randomSource)
    {
        var remaining = candidates.ToList();
        var result = new List<T>(Math.Min(count, remaining.Count));
        while (remaining.Count > 0 && result.Count < count)
        {
            var index = randomSource.NextInt(0, remaining.Count);
            result.Add(remaining[index]);
            remaining.RemoveAt(index);
        }
        return result;
    }

    private IEnumerable<IEffectRuntimeUnit> GetCurrentTargets(IEnumerable<UnitInstanceId> targetInstanceIds)
    {
        foreach (var instanceId in targetInstanceIds)
            if (_world.TryGetUnit(instanceId, out var unit)) yield return unit;
    }

    private void ResolveDeathWaves()
    {
        while (true)
        {
            var deadUnits = _world.ExtractDeadUnits().OrderBy(unit => unit.InstanceId.Value).ToArray();
            if (deadUnits.Length == 0) return;
            foreach (var deadUnit in deadUnits) ResolveDeath(deadUnit);
        }
    }

    private void ResolveDeath(IEffectRuntimeUnit deadUnit)
    {
        ClearTriggerProgress(deadUnit.InstanceId);
        var historyQueue = new Queue<GameEffectEvent>();
        RecordGameEventCore(deadUnit.OwnerPlayerId, NativeGameEventKeys.UnitDied, deadUnit.Definition, historyQueue);
        DrainEventQueue(historyQueue);
        var activations = new List<DeathTriggerActivation>();
        AddOwnDeathTriggers(deadUnit, activations);
        AddFriendlyDeathTriggers(deadUnit, activations);
        foreach (var activation in activations.OrderBy(value => value.Listener.InstanceId.Value).ThenBy(value => value.TriggerIndex))
        {
            var queue = new Queue<GameEffectEvent>();
            ResolveSpecificTrigger(activation.Listener, activation.TriggerIndex, activation.Trigger, queue);
            DrainEventQueue(queue);
        }

        var revived = _world.TryRevive(deadUnit);
        if (revived is not null)
        {
            var queue = new Queue<GameEffectEvent>();
            RecordGameEventCore(revived.OwnerPlayerId, NativeGameEventKeys.UnitSummoned, revived.Definition, queue);
            queue.Enqueue(new GameEffectEvent(NativeTriggerKeys.OnSummon, revived));
            DrainEventQueue(queue);
            return;
        }
        _world.FinalizeDeath(deadUnit);
    }

    private static void AddOwnDeathTriggers(IEffectRuntimeUnit deadUnit, ICollection<DeathTriggerActivation> activations)
    {
        for (var index = 0; index < deadUnit.Definition.Triggers.Count; index++)
        {
            var trigger = deadUnit.Definition.Triggers[index];
            if (trigger.Event == NativeTriggerKeys.OnDeath) activations.Add(new DeathTriggerActivation(deadUnit, index, trigger));
        }
    }

    private void AddFriendlyDeathTriggers(IEffectRuntimeUnit deadUnit, ICollection<DeathTriggerActivation> activations)
    {
        foreach (var listener in _world.Units.Where(unit => unit.OwnerPlayerId == deadUnit.OwnerPlayerId).OrderBy(unit => unit.InstanceId.Value))
        {
            for (var index = 0; index < listener.Definition.Triggers.Count; index++)
            {
                var trigger = listener.Definition.Triggers[index];
                if (trigger.Event != NativeTriggerKeys.AfterFriendlyDeaths) continue;
                if (AdvanceTrigger(listener.InstanceId, index, trigger.Count!.Value)) activations.Add(new DeathTriggerActivation(listener, index, trigger));
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
        foreach (var key in _triggerProgress.Keys.Where(key => key.InstanceId == instanceId).ToArray()) _triggerProgress.Remove(key);
    }

    private EffectResolutionContext BuildContext(IEffectRuntimeUnit source, UnitInstanceId? selectedTargetInstanceId = null)
    {
        var positions = new Dictionary<PlayerId, int>();
        var snapshots = new List<EffectUnitSnapshot>();
        foreach (var unit in _world.Units)
        {
            var position = positions.GetValueOrDefault(unit.OwnerPlayerId);
            positions[unit.OwnerPlayerId] = position + 1;
            snapshots.Add(CreateSnapshot(unit, true, position));
        }
        if (snapshots.All(snapshot => snapshot.InstanceId != source.InstanceId)) snapshots.Add(CreateSnapshot(source, false, -1));
        return new EffectResolutionContext(source.InstanceId, source.OwnerPlayerId, snapshots, selectedTargetInstanceId, _world.GetHistory(source.OwnerPlayerId));
    }

    private static EffectUnitSnapshot CreateSnapshot(IEffectRuntimeUnit unit, bool isSelectable, int position) =>
        new(unit.InstanceId, unit.OwnerPlayerId, unit.IsAlive, unit.Attack, unit.Health, position, isSelectable,
            unit.Definition.Types.Select(type => type.Id), unit.Definition.Tags.Select(tag => tag.Id));

    private static int FindTriggerIndex(UnitDefinition definition, TriggerDefinition trigger)
    {
        for (var index = 0; index < definition.Triggers.Count; index++)
            if (ReferenceEquals(definition.Triggers[index], trigger) || definition.Triggers[index] == trigger) return index;
        throw new ArgumentException("Trigger does not belong to the source definition.", nameof(trigger));
    }

    private void CountProcessedEvent()
    {
        _processedEvents++;
        if (_processedEvents > MaximumProcessedEvents)
            throw new InvalidOperationException("Effect runtime exceeded its internal safety budget. The mod likely contains a recursive effect loop.");
    }

    private static void ValidateEvent(GameEffectEvent effectEvent)
    {
        ArgumentNullException.ThrowIfNull(effectEvent);
        if (!NativeTriggerKeys.IsSupported(effectEvent.Event))
            throw new ArgumentException($"Unsupported trigger '{effectEvent.Event}'.", nameof(effectEvent));
    }

    private sealed record DeathTriggerActivation(IEffectRuntimeUnit Listener, int TriggerIndex, TriggerDefinition Trigger);
}
