using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Effects;

public sealed class EffectUnitSnapshot
{
    private readonly HashSet<UnitTypeId> _types;
    private readonly HashSet<TagId> _tags;

    public UnitInstanceId InstanceId { get; }
    public PlayerId OwnerPlayerId { get; }
    public bool IsAlive { get; }

    public EffectUnitSnapshot(
        UnitInstanceId instanceId,
        PlayerId ownerPlayerId,
        bool isAlive,
        IEnumerable<UnitTypeId>? types = null,
        IEnumerable<TagId>? tags = null)
    {
        InstanceId = instanceId;
        OwnerPlayerId = ownerPlayerId;
        IsAlive = isAlive;
        _types = new HashSet<UnitTypeId>(types ?? []);
        _tags = new HashSet<TagId>(tags ?? []);
    }

    public bool HasType(UnitTypeId id) => _types.Contains(id);
    public bool HasTag(TagId id) => _tags.Contains(id);
}

public sealed class EffectResolutionContext
{
    private readonly ReadOnlyCollection<EffectUnitSnapshot> _units;

    public UnitInstanceId SourceInstanceId { get; }
    public PlayerId SourcePlayerId { get; }
    public UnitInstanceId? SelectedTargetInstanceId { get; }
    public IReadOnlyList<EffectUnitSnapshot> Units => _units;

    public EffectResolutionContext(
        UnitInstanceId sourceInstanceId,
        PlayerId sourcePlayerId,
        IEnumerable<EffectUnitSnapshot> units,
        UnitInstanceId? selectedTargetInstanceId = null)
    {
        ArgumentNullException.ThrowIfNull(units);
        var materialized = units.ToArray();
        var duplicate = materialized.GroupBy(unit => unit.InstanceId).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new ArgumentException($"Duplicate effect unit instance id '{duplicate.Key}'.", nameof(units));
        if (!materialized.Any(unit => unit.InstanceId == sourceInstanceId && unit.OwnerPlayerId == sourcePlayerId))
            throw new ArgumentException("Effect source must be present in the resolution context.", nameof(units));
        if (selectedTargetInstanceId is not null &&
            !materialized.Any(unit => unit.InstanceId == selectedTargetInstanceId.Value && unit.IsAlive))
        {
            throw new ArgumentException("Selected effect target must be a living unit in the resolution context.", nameof(selectedTargetInstanceId));
        }

        SourceInstanceId = sourceInstanceId;
        SourcePlayerId = sourcePlayerId;
        SelectedTargetInstanceId = selectedTargetInstanceId;
        _units = Array.AsReadOnly(materialized);
    }
}

public sealed record ResolvedEffect(
    int Sequence,
    UnitInstanceId SourceInstanceId,
    NativeTriggerKey Trigger,
    EffectDefinition Definition,
    IReadOnlyList<UnitInstanceId> TargetInstanceIds);

public sealed class EffectPipeline
{
    public IReadOnlyList<ResolvedEffect> ResolveEvent(
        UnitDefinition sourceDefinition,
        NativeTriggerKey eventKey,
        EffectResolutionContext context,
        IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(sourceDefinition);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(randomSource);
        if (!NativeTriggerKeys.IsSupported(eventKey))
            throw new ArgumentException($"Unsupported trigger '{eventKey}'.", nameof(eventKey));

        var resolved = new List<ResolvedEffect>();
        var sequence = 0;
        foreach (var trigger in sourceDefinition.Triggers)
        {
            if (trigger.Event != eventKey) continue;
            foreach (var effect in trigger.Effects)
            {
                sequence++;
                resolved.Add(new ResolvedEffect(
                    sequence,
                    context.SourceInstanceId,
                    eventKey,
                    effect,
                    ResolveTargets(effect, context, randomSource)));
            }
        }
        return resolved;
    }

    public IReadOnlyList<ResolvedEffect> ResolveTrigger(
        UnitDefinition sourceDefinition,
        TriggerDefinition trigger,
        EffectResolutionContext context,
        IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(sourceDefinition);
        ArgumentNullException.ThrowIfNull(trigger);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(randomSource);

        if (!sourceDefinition.Triggers.Contains(trigger))
            throw new ArgumentException("Trigger does not belong to the source definition.", nameof(trigger));

        var resolved = new List<ResolvedEffect>(trigger.Effects.Count);
        var sequence = 0;
        foreach (var effect in trigger.Effects)
        {
            sequence++;
            resolved.Add(new ResolvedEffect(
                sequence,
                context.SourceInstanceId,
                trigger.Event,
                effect,
                ResolveTargets(effect, context, randomSource)));
        }
        return resolved;
    }

    private static IReadOnlyList<UnitInstanceId> ResolveTargets(
        EffectDefinition effect,
        EffectResolutionContext context,
        IRandomSource randomSource)
    {
        var selector = effect switch
        {
            ModifyStatsEffectDefinition value => value.Target,
            DealDamageEffectDefinition value => value.Target,
            DestroyUnitEffectDefinition value => value.Target,
            TriggerEventEffectDefinition value => value.Target,
            AddBehaviorEffectDefinition value => value.Target,
            RemoveBehaviorEffectDefinition value => value.Target,
            _ => null,
        };
        if (selector is null) return [];

        var candidates = context.Units
            .Where(unit => IsInScope(unit, selector.Scope, context))
            .Where(unit => MatchesFilters(unit, selector))
            .Select(unit => unit.InstanceId)
            .ToArray();

        return selector.Scope switch
        {
            EffectTargetScope.Self or EffectTargetScope.Selected => candidates.Take(1).ToArray(),
            EffectTargetScope.RandomFriendly or EffectTargetScope.RandomEnemy =>
                candidates.Length == 0 ? [] : [candidates[randomSource.NextInt(0, candidates.Length)]],
            EffectTargetScope.AllFriendly or EffectTargetScope.AllEnemy => candidates,
            _ => throw new ArgumentOutOfRangeException(nameof(selector.Scope), selector.Scope, "Unsupported effect target scope."),
        };
    }

    private static bool IsInScope(EffectUnitSnapshot unit, EffectTargetScope scope, EffectResolutionContext context) =>
        scope switch
        {
            EffectTargetScope.Self => unit.InstanceId == context.SourceInstanceId && unit.IsAlive,
            EffectTargetScope.Selected =>
                context.SelectedTargetInstanceId is not null &&
                unit.InstanceId == context.SelectedTargetInstanceId.Value &&
                unit.IsAlive,
            EffectTargetScope.RandomFriendly or EffectTargetScope.AllFriendly => unit.IsAlive && unit.OwnerPlayerId == context.SourcePlayerId,
            EffectTargetScope.RandomEnemy or EffectTargetScope.AllEnemy => unit.IsAlive && unit.OwnerPlayerId != context.SourcePlayerId,
            _ => false,
        };

    private static bool MatchesFilters(EffectUnitSnapshot unit, EffectTargetSelector selector) =>
        (selector.RequiredTypeId is null || unit.HasType(selector.RequiredTypeId.Value)) &&
        (selector.RequiredTagId is null || unit.HasTag(selector.RequiredTagId.Value));
}
