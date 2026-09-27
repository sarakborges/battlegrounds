using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Combat;

public enum CombatTimelineEventKind
{
    Trigger,
    AttackStarted,
    UnitSummoned,
    UnitStatsChanged,
    UnitDamaged,
    UnitDestroyed,
    UnitDied,
    UnitRevived,
    BehaviorChanged,
    ResourceChanged,
    PowerChanged,
}

public abstract record CombatTimelineEvent(int Sequence, CombatTimelineEventKind Kind);

public sealed record CombatTriggerTimelineEvent(
    int Sequence,
    PlayerId SourcePlayerId,
    NativeTriggerKey Trigger,
    UnitInstanceId? SourceUnitInstanceId,
    PowerId? SourcePowerId)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.Trigger);

public sealed record CombatAttackStartedTimelineEvent(
    int Sequence,
    PlayerId AttackerPlayerId,
    UnitInstanceId AttackerInstanceId,
    PlayerId TargetPlayerId,
    UnitInstanceId TargetInstanceId)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.AttackStarted);

public sealed record CombatUnitSummonedTimelineEvent(
    int Sequence,
    CombatUnitSnapshot Unit,
    int Position,
    UnitInstanceId? SourceUnitInstanceId,
    PowerId? SourcePowerId)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.UnitSummoned);

public sealed record CombatUnitStatsChangedTimelineEvent(
    int Sequence,
    PlayerId PlayerId,
    UnitInstanceId UnitInstanceId,
    int AttackBefore,
    int AttackAfter,
    int HealthBefore,
    int HealthAfter)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.UnitStatsChanged);

public sealed record CombatUnitDamagedTimelineEvent(
    int Sequence,
    PlayerId PlayerId,
    UnitInstanceId UnitInstanceId,
    int Amount,
    int HealthBefore,
    int HealthAfter)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.UnitDamaged);

public sealed record CombatUnitDestroyedTimelineEvent(
    int Sequence,
    PlayerId PlayerId,
    UnitInstanceId UnitInstanceId,
    int HealthBefore,
    int HealthAfter)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.UnitDestroyed);

public sealed record CombatUnitDiedTimelineEvent(
    int Sequence,
    PlayerId PlayerId,
    UnitInstanceId UnitInstanceId,
    UnitId UnitId,
    int Position)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.UnitDied);

public sealed record CombatUnitRevivedTimelineEvent(
    int Sequence,
    CombatUnitSnapshot Unit,
    int Position)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.UnitRevived);

public enum CombatBehaviorChangeKind
{
    Added,
    Removed,
    Consumed,
}

public sealed record CombatBehaviorChangedTimelineEvent(
    int Sequence,
    PlayerId PlayerId,
    UnitInstanceId UnitInstanceId,
    BehaviorId BehaviorId,
    NativeBehaviorKey Handler,
    CombatBehaviorChangeKind Change)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.BehaviorChanged);

public sealed record CombatResourceChangedTimelineEvent(
    int Sequence,
    PlayerId PlayerId,
    int Delta)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.ResourceChanged);

public sealed record CombatPowerChangedTimelineEvent(
    int Sequence,
    PlayerId PlayerId,
    PowerId? PreviousPowerId,
    PowerId PowerId)
    : CombatTimelineEvent(Sequence, CombatTimelineEventKind.PowerChanged);
