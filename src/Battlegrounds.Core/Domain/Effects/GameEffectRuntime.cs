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
    IReadOnlyList<IEffectRuntimeUnit> ExtractDeadUnits();
    IEffectRuntimeUnit? TryRevive(IEffectRuntimeUnit deadUnit);
    void FinalizeDeath(IEffectRuntimeUnit deadUnit);
}

internal sealed record GameEffectEvent(
    NativeTriggerKey Event,
    IEffectRuntimeUnit Subject,
    bool ResolveDeathLifecycle = false);

internal sealed class GameEffectRuntime
{
    private const int MaximumProcessedEvents = 10_000;

    private readonly IEffectRuntimeWorld _world;
    private readonly IRandomSource _randomSource;
    private readonly UnitCatalog? _unitCatalog;
    private readonly BehaviorCatalog? _behaviorCatalog;
    private readonly EffectPipeline _pipeline = new();

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

        var queue = new Queue<GameEffectEvent>();
        foreach (var effectEvent in initialEvents)
        {
            ValidateEvent(effectEvent);
            queue.Enqueue(effectEvent);
        }

        var processed = 0;
        while (queue.Count > 0)
        {
            processed++;
            if (processed > MaximumProcessedEvents)
            {
                throw new InvalidOperationException(
                    "Effect runtime exceeded its internal safety budget. The mod likely contains a recursive effect loop.");
            }

            var effectEvent = queue.Dequeue();
            foreach (var listener in ResolveListeners(effectEvent))
            {
                ResolveListener(effectEvent, listener, queue);
            }

            if (effectEvent.ResolveDeathLifecycle)
            {
                var revived = _world.TryRevive(effectEvent.Subject);
                if (revived is not null)
                {
                    queue.Enqueue(new GameEffectEvent(NativeTriggerKeys.OnSummon, revived));
                }
                else
                {
                    _world.FinalizeDeath(effectEvent.Subject);
                }
            }

            QueueDeaths(queue);
        }
    }

    private IReadOnlyList<IEffectRuntimeUnit> ResolveListeners(GameEffectEvent effectEvent)
    {
        if (effectEvent.Event == NativeTriggerKeys.OnSummon)
        {
            return _world.Units
                .Where(unit => unit.IsAlive && unit.OwnerPlayerId == effectEvent.Subject.OwnerPlayerId)
                .ToArray();
        }

        return [effectEvent.Subject];
    }

    private void ResolveListener(
        GameEffectEvent effectEvent,
        IEffectRuntimeUnit listener,
        Queue<GameEffectEvent> queue)
    {
        if (!listener.Definition.Triggers.Any(trigger => trigger.Event == effectEvent.Event))
        {
            return;
        }

        var context = BuildContext(listener);
        var resolvedEffects = _pipeline.ResolveEvent(
            listener.Definition,
            effectEvent.Event,
            context,
            _randomSource);

        foreach (var resolved in resolvedEffects)
        {
            ApplyEffect(listener, resolved, queue);
            QueueDeaths(queue);
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
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    _world.ModifyStats(target, modifyStats.AttackDelta, modifyStats.HealthDelta);
                }
                break;

            case DealDamageEffectDefinition dealDamage:
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    if (_world.TryConsumeBehavior(target, NativeBehaviorKeys.DamageBarrier))
                    {
                        continue;
                    }

                    _world.TakeDamage(target, dealDamage.Amount);
                    if (_world.TryConsumeBehavior(source, NativeBehaviorKeys.LethalFirstDamagePerCombat))
                    {
                        _world.Destroy(target);
                    }

                    queue.Enqueue(new GameEffectEvent(NativeTriggerKeys.OnDamage, target));
                }
                break;

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
                var unitCatalog = _unitCatalog
                    ?? throw new InvalidOperationException("summonUnit requires a UnitCatalog in the effect runtime.");
                var definition = unitCatalog.GetRequired(summon.UnitId);
                foreach (var summoned in _world.Summon(source, definition, summon.Count))
                {
                    queue.Enqueue(new GameEffectEvent(NativeTriggerKeys.OnSummon, summoned));
                }
                break;

            case AddBehaviorEffectDefinition addBehavior:
                var behaviorCatalog = _behaviorCatalog
                    ?? throw new InvalidOperationException("addBehavior requires a BehaviorCatalog in the effect runtime.");
                var behavior = behaviorCatalog.GetRequired(addBehavior.BehaviorId);
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    _world.AddBehavior(target, behavior);
                }
                break;

            case RemoveBehaviorEffectDefinition removeBehavior:
                foreach (var target in GetCurrentTargets(resolved.TargetInstanceIds))
                {
                    _world.RemoveBehavior(target, removeBehavior.BehaviorId);
                }
                break;

            case AddResourceEffectDefinition addResource:
                _world.AdjustResource(source.OwnerPlayerId, addResource.Amount);
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
            if (_world.TryGetUnit(instanceId, out var unit) && unit.IsAlive)
            {
                yield return unit;
            }
        }
    }

    private void QueueDeaths(Queue<GameEffectEvent> queue)
    {
        foreach (var deadUnit in _world.ExtractDeadUnits())
        {
            queue.Enqueue(new GameEffectEvent(
                NativeTriggerKeys.OnDeath,
                deadUnit,
                ResolveDeathLifecycle: true));
        }
    }

    private EffectResolutionContext BuildContext(IEffectRuntimeUnit source)
    {
        var snapshots = _world.Units
            .Select(CreateSnapshot)
            .ToList();

        if (snapshots.All(snapshot => snapshot.InstanceId != source.InstanceId))
        {
            snapshots.Add(CreateSnapshot(source));
        }

        return new EffectResolutionContext(
            source.InstanceId,
            source.OwnerPlayerId,
            snapshots);
    }

    private static EffectUnitSnapshot CreateSnapshot(IEffectRuntimeUnit unit) =>
        new(
            unit.InstanceId,
            unit.OwnerPlayerId,
            unit.IsAlive,
            unit.Definition.Types.Select(type => type.Id),
            unit.Definition.Tags.Select(tag => tag.Id));

    private static void ValidateEvent(GameEffectEvent effectEvent)
    {
        ArgumentNullException.ThrowIfNull(effectEvent);
        if (!NativeTriggerKeys.IsSupported(effectEvent.Event))
        {
            throw new ArgumentException($"Unsupported trigger '{effectEvent.Event}'.", nameof(effectEvent));
        }
    }
}
