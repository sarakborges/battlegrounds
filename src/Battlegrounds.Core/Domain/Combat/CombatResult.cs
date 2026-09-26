using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Combat;

public enum CombatEndReason
{
    Elimination,
    NoAttackPower,
}

public sealed class CombatResult
{
    private readonly ReadOnlyCollection<CombatAttack> _attacks;
    private readonly ReadOnlyCollection<CombatSurvivor> _leftSurvivors;
    private readonly ReadOnlyCollection<CombatSurvivor> _rightSurvivors;
    private readonly ReadOnlyDictionary<PlayerId, int> _resourceDeltas;
    private readonly ReadOnlyDictionary<PlayerId, PowerId> _powerChanges;
    private readonly ReadOnlyDictionary<PlayerId, EffectHistoryDelta> _historyDeltas;

    public PlayerId? WinnerPlayerId { get; }
    public bool IsDraw => WinnerPlayerId is null;
    public CombatEndReason EndReason { get; }
    public int AttackCount => _attacks.Count;
    public IReadOnlyList<CombatAttack> Attacks => _attacks;
    public IReadOnlyList<CombatSurvivor> LeftSurvivors => _leftSurvivors;
    public IReadOnlyList<CombatSurvivor> RightSurvivors => _rightSurvivors;
    public IReadOnlyDictionary<PlayerId, int> ResourceDeltas => _resourceDeltas;
    public IReadOnlyDictionary<PlayerId, PowerId> PowerChanges => _powerChanges;
    internal IReadOnlyDictionary<PlayerId, EffectHistoryDelta> HistoryDeltas => _historyDeltas;

    internal CombatResult(
        PlayerId? winnerPlayerId,
        CombatEndReason endReason,
        IEnumerable<CombatAttack> attacks,
        IEnumerable<CombatSurvivor> leftSurvivors,
        IEnumerable<CombatSurvivor> rightSurvivors,
        IReadOnlyDictionary<PlayerId, int>? resourceDeltas = null,
        IReadOnlyDictionary<PlayerId, PowerId>? powerChanges = null,
        IReadOnlyDictionary<PlayerId, EffectHistoryDelta>? historyDeltas = null)
    {
        WinnerPlayerId = winnerPlayerId;
        EndReason = endReason;
        _attacks = Array.AsReadOnly(attacks.ToArray());
        _leftSurvivors = Array.AsReadOnly(leftSurvivors.ToArray());
        _rightSurvivors = Array.AsReadOnly(rightSurvivors.ToArray());
        _resourceDeltas = new ReadOnlyDictionary<PlayerId, int>(
            new Dictionary<PlayerId, int>(resourceDeltas ?? new Dictionary<PlayerId, int>()));
        _powerChanges = new ReadOnlyDictionary<PlayerId, PowerId>(
            new Dictionary<PlayerId, PowerId>(powerChanges ?? new Dictionary<PlayerId, PowerId>()));
        _historyDeltas = new ReadOnlyDictionary<PlayerId, EffectHistoryDelta>(
            new Dictionary<PlayerId, EffectHistoryDelta>(historyDeltas ?? new Dictionary<PlayerId, EffectHistoryDelta>()));
    }
}

public readonly record struct CombatAttack(
    int Sequence,
    PlayerId AttackerPlayerId,
    UnitInstanceId AttackerInstanceId,
    PlayerId TargetPlayerId,
    UnitInstanceId TargetInstanceId,
    int DamageToAttacker,
    int DamageToTarget,
    bool AttackerBarrierLost,
    bool TargetBarrierLost,
    bool AttackerLethalTriggered,
    bool TargetLethalTriggered,
    int AttackerHealthAfter,
    int TargetHealthAfter,
    bool AttackerDied,
    bool TargetDied,
    bool AttackerRevived,
    bool TargetRevived);

public readonly record struct CombatSurvivor(
    UnitInstanceId InstanceId,
    int Health,
    int Tier = 1);
