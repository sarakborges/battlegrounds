using System.Collections.ObjectModel;
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

    public PlayerId? WinnerPlayerId { get; }
    public bool IsDraw => WinnerPlayerId is null;
    public CombatEndReason EndReason { get; }
    public int AttackCount => _attacks.Count;
    public IReadOnlyList<CombatAttack> Attacks => _attacks;
    public IReadOnlyList<CombatSurvivor> LeftSurvivors => _leftSurvivors;
    public IReadOnlyList<CombatSurvivor> RightSurvivors => _rightSurvivors;

    internal CombatResult(
        PlayerId? winnerPlayerId,
        CombatEndReason endReason,
        IEnumerable<CombatAttack> attacks,
        IEnumerable<CombatSurvivor> leftSurvivors,
        IEnumerable<CombatSurvivor> rightSurvivors)
    {
        WinnerPlayerId = winnerPlayerId;
        EndReason = endReason;
        _attacks = Array.AsReadOnly(attacks.ToArray());
        _leftSurvivors = Array.AsReadOnly(leftSurvivors.ToArray());
        _rightSurvivors = Array.AsReadOnly(rightSurvivors.ToArray());
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

public readonly record struct CombatSurvivor(UnitInstanceId InstanceId, int Health);
