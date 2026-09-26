using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Preparation;

internal sealed class PreparationEffectEngine
{
    private readonly PreparationRules _rules;
    private readonly IUnitPool _unitPool;
    private readonly IRandomSource _randomSource;
    private readonly UnitCatalog? _unitCatalog;
    private readonly BehaviorCatalog? _behaviorCatalog;
    private readonly EffectPipeline _pipeline = new();

    public PreparationEffectEngine(
        PreparationRules rules,
        IUnitPool unitPool,
        IRandomSource randomSource,
        UnitCatalog? unitCatalog,
        BehaviorCatalog? behaviorCatalog)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _unitPool = unitPool ?? throw new ArgumentNullException(nameof(unitPool));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        _unitCatalog = unitCatalog;
        _behaviorCatalog = behaviorCatalog;
    }

    public void ProcessPlayedUnit(MatchState match, PlayerState owner, UnitInstance unit)
    {
        ProcessEvents(match, [new EffectEvent(NativeTriggerKeys.OnPlay, unit, owner.Id)]);

        if (unit.IsAlive && owner.Field.Any(candidate => candidate.Id == unit.Id))
        {
            ProcessEvents(match, [new EffectEvent(NativeTriggerKeys.OnSummon, unit, owner.Id)]);
        }
    }

    public void ProcessTurnEvent(MatchState match, PlayerState owner, NativeTriggerKey eventKey)
    {
        if (eventKey != NativeTriggerKeys.OnTurnStart && eventKey != NativeTriggerKeys.OnTurnEnd)
        {
            throw new ArgumentException("Preparation turn events must be onTurnStart or onTurnEnd.", nameof(eventKey));
        }

        var initialUnits = owner.Field.Where(unit => unit.IsAlive).ToArray();
        ProcessEvents(match, initialUnits.Select(unit => new EffectEvent(eventKey, unit, owner.Id)));
    }

    private void ProcessEvents(MatchState match, IEnumerable<EffectEvent> initialEvents)
    {
        var queue = new EffectEventQueue();
        foreach (var effectEvent in initialEvents)
        {
            queue.Enqueue(effectEvent);
        }

        var deathQueued = new HashSet<UnitInstanceId>();
        while (queue.Count > 0)
        {
            var effectEvent = queue.Dequeue();
            foreach (var listener in ResolveListeners(match, effectEvent))
            {
                ResolveListener(match, effectEvent, listener.Unit, listener.Owner, queue, deathQueued);
            }
        }
    }

    private void ResolveListener(
        MatchState match,
        EffectEvent effectEvent,
        UnitInstance listener,
        PlayerState listenerOwner,
        EffectEventQueue queue,
        HashSet<UnitInstanceId> deathQueued)
    {
        if (!listener.Definition.Triggers.Any(trigger => trigger.Event == effectEvent.Event))
        {
            return;
        }

        var context = BuildContext(match, listener, listenerOwner.Id);
        var resolvedEffects = _pipeline.ResolveEvent(
            listener.Definition,
            effectEvent.Event,
            context,
            _randomSource);

        foreach (var resolved in resolvedEffects)
        {
            ApplyEffect(match, listenerOwner, resolved, queue, deathQueued);
        }
    }

    private void ApplyEffect(
        MatchState match,
        PlayerState sourceOwner,
        ResolvedEffect resolved,
        EffectEventQueue queue,
        HashSet<UnitInstanceId> deathQueued)
    {
        switch (resolved.Definition)
        {
            case ModifyStatsEffectDefinition modifyStats:
                foreach (var targetId in resolved.TargetInstanceIds)
                {
                    if (TryFindFieldUnit(match, targetId, out _, out var target))
                    {
                        target.ModifyStats(modifyStats.AttackDelta, modifyStats.HealthDelta);
                        QueueDeathIfNeeded(match, target, queue, deathQueued);
                    }
                }
                break;

            case DealDamageEffectDefinition dealDamage:
                foreach (var targetId in resolved.TargetInstanceIds)
                {
                    if (!TryFindFieldUnit(match, targetId, out var targetOwner, out var target))
                    {
                        continue;
                    }

                    if (target.RemoveBehavior(NativeBehaviorKeys.DamageBarrier))
                    {
                        continue;
                    }

                    target.TakeDamage(dealDamage.Amount);
                    queue.Enqueue(new EffectEvent(NativeTriggerKeys.OnDamage, target, targetOwner.Id));
                    QueueDeathIfNeeded(match, target, queue, deathQueued);
                }
                break;

            case SummonUnitEffectDefinition summon:
                var catalog = _unitCatalog
                    ?? throw new InvalidOperationException("summonUnit requires a UnitCatalog in the preparation engine.");
                var definition = catalog.GetRequired(summon.UnitId);
                for (var index = 0; index < summon.Count; index++)
                {
                    if (sourceOwner.Field.Count >= _rules.FieldCapacity)
                    {
                        break;
                    }

                    var summoned = match.CreateUnit(definition, UnitInstanceOrigin.Generated);
                    sourceOwner.AddToField(summoned);
                    queue.Enqueue(new EffectEvent(NativeTriggerKeys.OnSummon, summoned, sourceOwner.Id));
                }
                break;

            case AddBehaviorEffectDefinition addBehavior:
                var behaviors = _behaviorCatalog
                    ?? throw new InvalidOperationException("addBehavior requires a BehaviorCatalog in the preparation engine.");
                var behavior = behaviors.GetRequired(addBehavior.BehaviorId);
                foreach (var targetId in resolved.TargetInstanceIds)
                {
                    if (TryFindFieldUnit(match, targetId, out _, out var target))
                    {
                        target.AddBehavior(behavior);
                    }
                }
                break;

            case RemoveBehaviorEffectDefinition removeBehavior:
                foreach (var targetId in resolved.TargetInstanceIds)
                {
                    if (TryFindFieldUnit(match, targetId, out _, out var target))
                    {
                        target.RemoveBehavior(removeBehavior.BehaviorId);
                    }
                }
                break;

            case AddResourceEffectDefinition addResource:
                sourceOwner.AdjustResource(addResource.Amount, _rules.MaximumResource);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(resolved),
                    resolved.Definition.Kind,
                    "Unsupported preparation effect kind.");
        }
    }

    private void QueueDeathIfNeeded(
        MatchState match,
        UnitInstance unit,
        EffectEventQueue queue,
        HashSet<UnitInstanceId> deathQueued)
    {
        if (unit.IsAlive || !deathQueued.Add(unit.Id))
        {
            return;
        }

        if (!TryFindFieldUnit(match, unit.Id, out var owner, out _))
        {
            return;
        }

        var removed = owner.RemoveFromField(unit.Id)
            ?? throw new InvalidOperationException("Dead unit disappeared before death processing.");

        if (removed.Origin == UnitInstanceOrigin.Pooled)
        {
            _unitPool.ReturnUnit(removed.Definition);
        }

        queue.Enqueue(new EffectEvent(NativeTriggerKeys.OnDeath, removed, owner.Id));
    }

    private static IReadOnlyList<(UnitInstance Unit, PlayerState Owner)> ResolveListeners(
        MatchState match,
        EffectEvent effectEvent)
    {
        if (effectEvent.Event == NativeTriggerKeys.OnSummon)
        {
            if (!match.TryGetPlayer(effectEvent.SubjectPlayerId, out var owner))
            {
                throw new InvalidOperationException("Effect event owner is not part of the match.");
            }

            return owner.Field
                .Where(unit => unit.IsAlive)
                .Select(unit => (unit, owner))
                .ToArray();
        }

        if (!match.TryGetPlayer(effectEvent.SubjectPlayerId, out var subjectOwner))
        {
            throw new InvalidOperationException("Effect event owner is not part of the match.");
        }

        return [(effectEvent.Subject, subjectOwner)];
    }

    private static EffectResolutionContext BuildContext(
        MatchState match,
        UnitInstance source,
        PlayerId sourcePlayerId)
    {
        var snapshots = match.Players
            .SelectMany(player => player.Field.Select(unit => CreateSnapshot(unit, player.Id)))
            .ToList();

        if (snapshots.All(snapshot => snapshot.InstanceId != source.Id))
        {
            snapshots.Add(CreateSnapshot(source, sourcePlayerId));
        }

        return new EffectResolutionContext(source.Id, sourcePlayerId, snapshots);
    }

    private static EffectUnitSnapshot CreateSnapshot(UnitInstance unit, PlayerId ownerPlayerId) =>
        new(
            unit.Id,
            ownerPlayerId,
            unit.IsAlive,
            unit.Definition.Types.Select(type => type.Id),
            unit.Definition.Tags.Select(tag => tag.Id));

    private static bool TryFindFieldUnit(
        MatchState match,
        UnitInstanceId instanceId,
        out PlayerState owner,
        out UnitInstance unit)
    {
        foreach (var candidateOwner in match.Players)
        {
            if (candidateOwner.TryGetFieldUnit(instanceId, out var candidate))
            {
                owner = candidateOwner;
                unit = candidate;
                return true;
            }
        }

        owner = null!;
        unit = null!;
        return false;
    }
}
