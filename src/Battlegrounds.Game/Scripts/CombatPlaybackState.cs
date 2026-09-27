using Battlegrounds.Application;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;

namespace Battlegrounds.Game;

internal sealed class CombatPlaybackState
{
    private readonly List<CombatPlaybackUnitState> _leftUnits;
    private readonly List<CombatPlaybackUnitState> _rightUnits;
    private readonly Dictionary<UnitInstanceId, CombatPlaybackUnitState> _unitsById;
    private int _nextAttackIndex;

    public SessionCombatRecord Record { get; }
    public CombatSettlement Settlement { get; }
    public PlayerId LeftPlayerId => Settlement.LeftPlayerId;
    public PlayerId RightPlayerId => Settlement.RightPlayerId ?? Settlement.EliminatedOpponentSourcePlayerId
        ?? throw new InvalidOperationException("Combat settlement has no right-side participant.");
    public IReadOnlyList<CombatPlaybackUnitState> LeftUnits => _leftUnits;
    public IReadOnlyList<CombatPlaybackUnitState> RightUnits => _rightUnits;
    public CombatAttack? CurrentAttack { get; private set; }
    public bool SettlementVisible { get; private set; }
    public bool IsComplete { get; private set; }
    public string EventText { get; private set; } = "Combat ready.";

    private CombatPlaybackState(SessionCombatRecord record, CombatSettlement settlement)
    {
        Record = record;
        Settlement = settlement;
        _leftUnits = record.StartingUnits
            .Where(unit => unit.PlayerId == LeftPlayerId)
            .Select(CombatPlaybackUnitState.FromSnapshot)
            .ToList();
        _rightUnits = record.StartingUnits
            .Where(unit => unit.PlayerId == RightPlayerId)
            .Select(CombatPlaybackUnitState.FromSnapshot)
            .ToList();
        _unitsById = _leftUnits.Concat(_rightUnits).ToDictionary(unit => unit.InstanceId);
    }

    public static CombatPlaybackState? TryCreate(SessionCombatRecord record, PlayerId focusPlayerId)
    {
        ArgumentNullException.ThrowIfNull(record);
        var settlement = record.RoundResult.Settlements.FirstOrDefault(value =>
            value.LeftPlayerId == focusPlayerId ||
            value.RightPlayerId == focusPlayerId ||
            value.EliminatedOpponentSourcePlayerId == focusPlayerId);
        return settlement is null ? null : new CombatPlaybackState(record, settlement);
    }

    public bool Advance()
    {
        if (IsComplete) return true;

        var attacks = Settlement.CombatResult.Attacks;
        if (_nextAttackIndex < attacks.Count)
        {
            ApplyAttack(attacks[_nextAttackIndex++]);
            return false;
        }

        if (!SettlementVisible)
        {
            CurrentAttack = null;
            ClearHighlights();
            SettlementVisible = true;
            EventText = BuildSettlementText();
            return false;
        }

        IsComplete = true;
        return true;
    }

    public void SkipToSettlement()
    {
        if (IsComplete) return;
        while (_nextAttackIndex < Settlement.CombatResult.Attacks.Count)
            ApplyAttack(Settlement.CombatResult.Attacks[_nextAttackIndex++]);
        CurrentAttack = null;
        ClearHighlights();
        SettlementVisible = true;
        EventText = BuildSettlementText();
    }

    private void ApplyAttack(CombatAttack attack)
    {
        ClearHighlights();
        CurrentAttack = attack;

        var attacker = GetOrCreateUnit(
            attack.AttackerPlayerId,
            attack.AttackerInstanceId,
            attack.AttackerHealthAfter + attack.DamageToAttacker);
        var target = GetOrCreateUnit(
            attack.TargetPlayerId,
            attack.TargetInstanceId,
            attack.TargetHealthAfter + attack.DamageToTarget);

        attacker.Highlight = "ATTACK";
        target.Highlight = "TARGET";
        attacker.Health = attack.AttackerHealthAfter;
        target.Health = attack.TargetHealthAfter;
        attacker.IsAlive = !attack.AttackerDied || attack.AttackerRevived;
        target.IsAlive = !attack.TargetDied || attack.TargetRevived;
        attacker.Status = BuildUnitOutcome(
            attack.AttackerBarrierLost,
            attack.AttackerLethalTriggered,
            attack.AttackerDied,
            attack.AttackerRevived);
        target.Status = BuildUnitOutcome(
            attack.TargetBarrierLost,
            attack.TargetLethalTriggered,
            attack.TargetDied,
            attack.TargetRevived);

        EventText = $"Attack {attack.Sequence}: P{attack.AttackerPlayerId.Value} {attacker.Name} → " +
                    $"P{attack.TargetPlayerId.Value} {target.Name}. " +
                    $"Target takes {attack.DamageToTarget}; attacker takes {attack.DamageToAttacker}.";
    }

    private CombatPlaybackUnitState GetOrCreateUnit(PlayerId playerId, UnitInstanceId instanceId, int inferredHealth)
    {
        if (_unitsById.TryGetValue(instanceId, out var existing)) return existing;

        var created = new CombatPlaybackUnitState(
            playerId,
            instanceId,
            $"Unit #{instanceId.Value}",
            attack: null,
            Math.Max(0, inferredHealth));
        _unitsById[instanceId] = created;
        GetSide(playerId).Add(created);
        return created;
    }

    private List<CombatPlaybackUnitState> GetSide(PlayerId playerId)
    {
        if (playerId == LeftPlayerId) return _leftUnits;
        if (playerId == RightPlayerId) return _rightUnits;
        throw new InvalidOperationException($"Player '{playerId}' is not part of this combat playback.");
    }

    private void ClearHighlights()
    {
        foreach (var unit in _unitsById.Values)
        {
            unit.Highlight = string.Empty;
            unit.Status = string.Empty;
        }
    }

    private string BuildSettlementText()
    {
        var opponent = Settlement.RightPlayerId is PlayerId right
            ? $"P{right.Value}"
            : $"archived P{Settlement.EliminatedOpponentSourcePlayerId?.Value}";
        var winner = Settlement.EliminatedOpponentWon
            ? opponent
            : Settlement.WinnerPlayerId is PlayerId winnerId
                ? $"P{winnerId.Value}"
                : "draw";
        var damage = Settlement.DamagedPlayerId is PlayerId damaged
            ? $" P{damaged.Value} takes {Settlement.PlayerDamage} player damage " +
              $"({Settlement.ArmorAbsorbed} absorbed by Armor)."
            : " No player damage.";
        return $"Combat complete: {winner}." + damage;
    }

    private static string BuildUnitOutcome(bool barrierLost, bool lethalTriggered, bool died, bool revived)
    {
        var parts = new List<string>();
        if (barrierLost) parts.Add("barrier lost");
        if (lethalTriggered) parts.Add("lethal triggered");
        if (died && revived) parts.Add("died → revived");
        else if (died) parts.Add("died");
        return string.Join(" • ", parts);
    }
}

internal sealed class CombatPlaybackUnitState
{
    public PlayerId PlayerId { get; }
    public UnitInstanceId InstanceId { get; }
    public string Name { get; }
    public int? Attack { get; }
    public int Health { get; set; }
    public bool IsAlive { get; set; } = true;
    public string Highlight { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

    public CombatPlaybackUnitState(
        PlayerId playerId,
        UnitInstanceId instanceId,
        string name,
        int? attack,
        int health)
    {
        PlayerId = playerId;
        InstanceId = instanceId;
        Name = name;
        Attack = attack;
        Health = health;
    }

    public static CombatPlaybackUnitState FromSnapshot(SessionCombatUnitSnapshot snapshot) =>
        new(snapshot.PlayerId, snapshot.InstanceId, snapshot.Name, snapshot.Attack, snapshot.Health);
}
