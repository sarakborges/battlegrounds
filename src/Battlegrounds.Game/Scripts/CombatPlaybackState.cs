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
    private int _nextTimelineIndex;

    public SessionCombatRecord Record { get; }
    public CombatSettlement Settlement { get; }
    public PlayerId LeftPlayerId => Settlement.LeftPlayerId;
    public PlayerId RightPlayerId => Settlement.RightPlayerId ?? Settlement.EliminatedOpponentSourcePlayerId
        ?? throw new InvalidOperationException("Combat settlement has no right-side participant.");
    public IReadOnlyList<CombatPlaybackUnitState> LeftUnits => _leftUnits;
    public IReadOnlyList<CombatPlaybackUnitState> RightUnits => _rightUnits;
    public CombatTimelineEvent? CurrentEvent { get; private set; }
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

        var timeline = Settlement.CombatResult.Timeline;
        if (_nextTimelineIndex < timeline.Count)
        {
            ApplyEvent(timeline[_nextTimelineIndex++]);
            return false;
        }

        if (!SettlementVisible)
        {
            CurrentEvent = null;
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
        var timeline = Settlement.CombatResult.Timeline;
        while (_nextTimelineIndex < timeline.Count)
            ApplyEvent(timeline[_nextTimelineIndex++]);
        CurrentEvent = null;
        ClearHighlights();
        SettlementVisible = true;
        EventText = BuildSettlementText();
    }

    private void ApplyEvent(CombatTimelineEvent @event)
    {
        ClearHighlights();
        CurrentEvent = @event;

        switch (@event)
        {
            case CombatTriggerTimelineEvent trigger:
                ApplyTrigger(trigger);
                break;
            case CombatAttackStartedTimelineEvent attack:
                ApplyAttackStarted(attack);
                break;
            case CombatUnitSummonedTimelineEvent summon:
                ApplySummon(summon);
                break;
            case CombatUnitStatsChangedTimelineEvent stats:
                ApplyStatsChanged(stats);
                break;
            case CombatUnitDamagedTimelineEvent damage:
                ApplyDamage(damage);
                break;
            case CombatUnitDestroyedTimelineEvent destroyed:
                ApplyDestroyed(destroyed);
                break;
            case CombatUnitDiedTimelineEvent died:
                ApplyDied(died);
                break;
            case CombatUnitRevivedTimelineEvent revived:
                ApplyRevived(revived);
                break;
            case CombatBehaviorChangedTimelineEvent behavior:
                ApplyBehaviorChanged(behavior);
                break;
            case CombatResourceChangedTimelineEvent resource:
                EventText = $"P{resource.PlayerId.Value} combat Resource {(resource.Delta >= 0 ? "+" : string.Empty)}{resource.Delta}.";
                break;
            case CombatPowerChangedTimelineEvent power:
                EventText = $"P{power.PlayerId.Value} Power changed from {power.PreviousPowerId?.Value ?? "none"} to {power.PowerId.Value}.";
                break;
            default:
                EventText = $"Combat event {@event.Sequence}: {@event.Kind}.";
                break;
        }
    }

    private void ApplyTrigger(CombatTriggerTimelineEvent trigger)
    {
        if (trigger.SourceUnitInstanceId is UnitInstanceId unitId && _unitsById.TryGetValue(unitId, out var unit))
        {
            unit.Highlight = "TRIGGER";
            unit.Status = trigger.Trigger.Value;
            EventText = $"P{trigger.SourcePlayerId.Value} {unit.Name} triggered {trigger.Trigger.Value}.";
            return;
        }

        var source = trigger.SourcePowerId is PowerId powerId ? $"Power {powerId.Value}" : "combat source";
        EventText = $"P{trigger.SourcePlayerId.Value} {source} triggered {trigger.Trigger.Value}.";
    }

    private void ApplyAttackStarted(CombatAttackStartedTimelineEvent attack)
    {
        var attacker = GetOrCreateUnit(attack.AttackerPlayerId, attack.AttackerInstanceId);
        var target = GetOrCreateUnit(attack.TargetPlayerId, attack.TargetInstanceId);
        attacker.Highlight = "ATTACK";
        target.Highlight = "TARGET";
        EventText = $"P{attack.AttackerPlayerId.Value} {attacker.Name} attacks P{attack.TargetPlayerId.Value} {target.Name}.";
    }

    private void ApplySummon(CombatUnitSummonedTimelineEvent summon)
    {
        var unit = CombatPlaybackUnitState.FromSnapshot(summon.PlayerId, summon.Unit);
        _unitsById[unit.InstanceId] = unit;
        InsertOnBoard(unit, summon.Position);
        unit.Highlight = "SUMMON";
        unit.Status = "summoned";
        EventText = $"P{summon.PlayerId.Value} summoned {unit.Name} at position {summon.Position + 1}.";
    }

    private void ApplyStatsChanged(CombatUnitStatsChangedTimelineEvent stats)
    {
        var unit = GetOrCreateUnit(stats.PlayerId, stats.UnitInstanceId);
        unit.Attack = stats.AttackAfter;
        unit.Health = stats.HealthAfter;
        unit.Highlight = "EFFECT";
        unit.Status = $"{stats.AttackBefore}/{stats.HealthBefore} → {stats.AttackAfter}/{stats.HealthAfter}";
        EventText = $"{unit.Name} stats changed to {stats.AttackAfter}/{stats.HealthAfter}.";
    }

    private void ApplyDamage(CombatUnitDamagedTimelineEvent damage)
    {
        var unit = GetOrCreateUnit(damage.PlayerId, damage.UnitInstanceId);
        unit.Health = damage.HealthAfter;
        unit.Highlight = "DAMAGE";
        unit.Status = $"-{damage.Amount} Health";
        EventText = $"P{damage.PlayerId.Value} {unit.Name} takes {damage.Amount} damage ({damage.HealthBefore} → {damage.HealthAfter}).";
    }

    private void ApplyDestroyed(CombatUnitDestroyedTimelineEvent destroyed)
    {
        var unit = GetOrCreateUnit(destroyed.PlayerId, destroyed.UnitInstanceId);
        unit.Health = destroyed.HealthAfter;
        unit.Highlight = "DESTROY";
        unit.Status = "destroyed";
        EventText = $"P{destroyed.PlayerId.Value} {unit.Name} was destroyed.";
    }

    private void ApplyDied(CombatUnitDiedTimelineEvent died)
    {
        var unit = GetOrCreateUnit(died.PlayerId, died.UnitInstanceId);
        unit.IsAlive = false;
        unit.Status = "died";
        RemoveFromBoard(unit);
        EventText = $"P{died.PlayerId.Value} {unit.Name} died from position {died.Position + 1}.";
    }

    private void ApplyRevived(CombatUnitRevivedTimelineEvent revived)
    {
        if (!_unitsById.TryGetValue(revived.Unit.InstanceId, out var unit))
        {
            unit = CombatPlaybackUnitState.FromSnapshot(revived.PlayerId, revived.Unit);
            _unitsById[unit.InstanceId] = unit;
        }
        else
        {
            unit.Name = revived.Unit.Definition?.Name ?? revived.Unit.UnitId.Value;
            unit.Attack = revived.Unit.Attack;
            unit.Health = revived.Unit.Health;
        }

        unit.IsAlive = true;
        InsertOnBoard(unit, revived.Position);
        unit.Highlight = "REVIVE";
        unit.Status = "revived";
        EventText = $"P{revived.PlayerId.Value} {unit.Name} revived at position {revived.Position + 1}.";
    }

    private void ApplyBehaviorChanged(CombatBehaviorChangedTimelineEvent behavior)
    {
        var unit = GetOrCreateUnit(behavior.PlayerId, behavior.UnitInstanceId);
        unit.Highlight = "BEHAVIOR";
        unit.Status = $"{behavior.Handler.Value} {behavior.Change.ToString().ToLowerInvariant()}";
        EventText = $"P{behavior.PlayerId.Value} {unit.Name}: behavior {behavior.Handler.Value} {behavior.Change.ToString().ToLowerInvariant()}.";
    }

    private CombatPlaybackUnitState GetOrCreateUnit(PlayerId playerId, UnitInstanceId instanceId)
    {
        if (_unitsById.TryGetValue(instanceId, out var existing)) return existing;

        var created = new CombatPlaybackUnitState(playerId, instanceId, $"Unit #{instanceId.Value}", null, 0);
        _unitsById[instanceId] = created;
        GetSide(playerId).Add(created);
        return created;
    }

    private void InsertOnBoard(CombatPlaybackUnitState unit, int position)
    {
        var side = GetSide(unit.PlayerId);
        side.Remove(unit);
        side.Insert(Math.Clamp(position, 0, side.Count), unit);
    }

    private void RemoveFromBoard(CombatPlaybackUnitState unit) => GetSide(unit.PlayerId).Remove(unit);

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
}

internal sealed class CombatPlaybackUnitState
{
    public PlayerId PlayerId { get; }
    public UnitInstanceId InstanceId { get; }
    public string Name { get; set; }
    public int? Attack { get; set; }
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

    public static CombatPlaybackUnitState FromSnapshot(PlayerId playerId, CombatUnitSnapshot snapshot) =>
        new(
            playerId,
            snapshot.InstanceId,
            snapshot.Definition?.Name ?? snapshot.UnitId.Value,
            snapshot.Attack,
            snapshot.Health);
}
