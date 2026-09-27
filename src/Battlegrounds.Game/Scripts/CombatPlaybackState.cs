using Battlegrounds.Application;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;

namespace Battlegrounds.Game;

internal sealed class CombatPlaybackState
{
    private readonly List<CombatPlaybackUnitState> _leftUnits;
    private readonly List<CombatPlaybackUnitState> _rightUnits;
    private readonly Dictionary<UnitInstanceId, CombatPlaybackUnitState> _unitsById;
    private readonly ModPresentationText _text;
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
    public string EventText { get; private set; }

    private CombatPlaybackState(
        SessionCombatRecord record,
        CombatSettlement settlement,
        ModPresentationText text)
    {
        Record = record;
        Settlement = settlement;
        _text = text ?? throw new ArgumentNullException(nameof(text));
        EventText = Text("ui.combatReady", ("combat", Term("combat")));
        _leftUnits = record.StartingUnits
            .Where(unit => unit.PlayerId == LeftPlayerId)
            .Select(unit => CombatPlaybackUnitState.FromSnapshot(unit, UnitName(unit.UnitId)))
            .ToList();
        _rightUnits = record.StartingUnits
            .Where(unit => unit.PlayerId == RightPlayerId)
            .Select(unit => CombatPlaybackUnitState.FromSnapshot(unit, UnitName(unit.UnitId)))
            .ToList();
        _unitsById = _leftUnits.Concat(_rightUnits).ToDictionary(unit => unit.InstanceId);
    }

    public static CombatPlaybackState? TryCreate(
        SessionCombatRecord record,
        PlayerId focusPlayerId,
        ModPresentationText text)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(text);
        var settlement = record.RoundResult.Settlements.FirstOrDefault(value =>
            value.LeftPlayerId == focusPlayerId ||
            value.RightPlayerId == focusPlayerId ||
            value.EliminatedOpponentSourcePlayerId == focusPlayerId);
        return settlement is null ? null : new CombatPlaybackState(record, settlement, text);
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
            {
                var delta = (resource.Delta >= 0 ? "+" : string.Empty) + resource.Delta;
                EventText = Text(
                    "ui.combatResourceChanged",
                    ("player", resource.PlayerId.Value),
                    ("combat", Term("combat")),
                    ("resource", Term("resource")),
                    ("delta", delta));
                break;
            }
            case CombatPowerChangedTimelineEvent power:
                EventText = Text(
                    "ui.combatPowerChanged",
                    ("player", power.PlayerId.Value),
                    ("power", Term("power")),
                    ("previous", power.PreviousPowerId is PowerId previousPowerId ? PowerName(previousPowerId) : "—"),
                    ("next", PowerName(power.PowerId)));
                break;
            default:
                EventText = Text(
                    "ui.combatGenericEvent",
                    ("combat", Term("combat")),
                    ("sequence", @event.Sequence),
                    ("kind", @event.Kind));
                break;
        }
    }

    private void ApplyTrigger(CombatTriggerTimelineEvent trigger)
    {
        if (trigger.SourceUnitInstanceId is UnitInstanceId unitId && _unitsById.TryGetValue(unitId, out var unit))
        {
            unit.Highlight = "◆";
            unit.Status = trigger.Trigger.Value;
            EventText = Text(
                "ui.combatUnitTriggered",
                ("player", trigger.SourcePlayerId.Value),
                ("name", unit.Name),
                ("trigger", trigger.Trigger.Value));
            return;
        }

        var source = trigger.SourcePowerId is PowerId powerId
            ? Text("ui.combatPowerSource", ("power", Term("power")), ("id", PowerName(powerId)))
            : Text("ui.combatSource", ("combat", Term("combat")));
        EventText = Text(
            "ui.combatSourceTriggered",
            ("player", trigger.SourcePlayerId.Value),
            ("source", source),
            ("trigger", trigger.Trigger.Value));
    }

    private void ApplyAttackStarted(CombatAttackStartedTimelineEvent attack)
    {
        var attacker = GetOrCreateUnit(attack.AttackerPlayerId, attack.AttackerInstanceId);
        var target = GetOrCreateUnit(attack.TargetPlayerId, attack.TargetInstanceId);
        attacker.Highlight = "→";
        target.Highlight = "◎";
        EventText = Text(
            "ui.combatAttackStarted",
            ("attackerPlayer", attack.AttackerPlayerId.Value),
            ("attacker", attacker.Name),
            ("targetPlayer", attack.TargetPlayerId.Value),
            ("target", target.Name));
    }

    private void ApplySummon(CombatUnitSummonedTimelineEvent summon)
    {
        var unit = CombatPlaybackUnitState.FromSnapshot(summon.PlayerId, summon.Unit, UnitName(summon.Unit.UnitId));
        _unitsById[unit.InstanceId] = unit;
        InsertOnBoard(unit, summon.Position);
        unit.Highlight = "+";
        EventText = Text(
            "ui.combatSummoned",
            ("player", summon.PlayerId.Value),
            ("name", unit.Name),
            ("position", summon.Position + 1));
    }

    private void ApplyStatsChanged(CombatUnitStatsChangedTimelineEvent stats)
    {
        var unit = GetOrCreateUnit(stats.PlayerId, stats.UnitInstanceId);
        unit.Attack = stats.AttackAfter;
        unit.Health = stats.HealthAfter;
        unit.Highlight = "Δ";
        unit.Status = $"{stats.AttackBefore}/{stats.HealthBefore} → {stats.AttackAfter}/{stats.HealthAfter}";
        EventText = Text(
            "ui.combatStatsChanged",
            ("name", unit.Name),
            ("attack", stats.AttackAfter),
            ("health", stats.HealthAfter));
    }

    private void ApplyDamage(CombatUnitDamagedTimelineEvent damage)
    {
        var unit = GetOrCreateUnit(damage.PlayerId, damage.UnitInstanceId);
        unit.Health = damage.HealthAfter;
        unit.Highlight = "−";
        unit.Status = $"-{damage.Amount} {Term("health")}";
        EventText = Text(
            "ui.combatDamageTaken",
            ("player", damage.PlayerId.Value),
            ("name", unit.Name),
            ("amount", damage.Amount),
            ("before", damage.HealthBefore),
            ("after", damage.HealthAfter));
    }

    private void ApplyDestroyed(CombatUnitDestroyedTimelineEvent destroyed)
    {
        var unit = GetOrCreateUnit(destroyed.PlayerId, destroyed.UnitInstanceId);
        unit.Health = destroyed.HealthAfter;
        unit.Highlight = "×";
        EventText = Text(
            "ui.combatDestroyed",
            ("player", destroyed.PlayerId.Value),
            ("name", unit.Name));
    }

    private void ApplyDied(CombatUnitDiedTimelineEvent died)
    {
        var unit = GetOrCreateUnit(died.PlayerId, died.UnitInstanceId);
        unit.IsAlive = false;
        RemoveFromBoard(unit);
        EventText = Text(
            "ui.combatDied",
            ("player", died.PlayerId.Value),
            ("name", unit.Name),
            ("position", died.Position + 1));
    }

    private void ApplyRevived(CombatUnitRevivedTimelineEvent revived)
    {
        if (!_unitsById.TryGetValue(revived.Unit.InstanceId, out var unit))
        {
            unit = CombatPlaybackUnitState.FromSnapshot(revived.PlayerId, revived.Unit, UnitName(revived.Unit.UnitId));
            _unitsById[unit.InstanceId] = unit;
        }
        else
        {
            unit.Name = UnitName(revived.Unit.UnitId);
            unit.Attack = revived.Unit.Attack;
            unit.Health = revived.Unit.Health;
        }

        unit.IsAlive = true;
        InsertOnBoard(unit, revived.Position);
        unit.Highlight = "↻";
        EventText = Text(
            "ui.combatRevived",
            ("player", revived.PlayerId.Value),
            ("name", unit.Name),
            ("position", revived.Position + 1));
    }

    private void ApplyBehaviorChanged(CombatBehaviorChangedTimelineEvent behavior)
    {
        var unit = GetOrCreateUnit(behavior.PlayerId, behavior.UnitInstanceId);
        unit.Highlight = "◇";
        unit.Status = behavior.Handler.Value;
        EventText = Text(
            "ui.combatBehaviorChanged",
            ("player", behavior.PlayerId.Value),
            ("name", unit.Name),
            ("behavior", behavior.Handler.Value),
            ("change", behavior.Change.ToString().ToLowerInvariant()));
    }

    private CombatPlaybackUnitState GetOrCreateUnit(PlayerId playerId, UnitInstanceId instanceId)
    {
        if (_unitsById.TryGetValue(instanceId, out var existing)) return existing;

        var created = new CombatPlaybackUnitState(
            playerId,
            instanceId,
            Text("ui.combatUnitFallback", ("unit", Term("unit")), ("instance", instanceId.Value)),
            null,
            0);
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
            : Text("ui.archivedPlayer", ("player", Settlement.EliminatedOpponentSourcePlayerId?.Value));
        var winner = Settlement.EliminatedOpponentWon
            ? opponent
            : Settlement.WinnerPlayerId is PlayerId winnerId
                ? $"P{winnerId.Value}"
                : Text("ui.draw");
        var damage = Settlement.DamagedPlayerId is PlayerId damaged
            ? Text(
                "ui.playerDamage",
                ("player", damaged.Value),
                ("damage", Settlement.PlayerDamage),
                ("absorbed", Settlement.ArmorAbsorbed),
                ("armor", Term("armor")))
            : Text("ui.noPlayerDamage");
        return Text(
            "ui.combatComplete",
            ("combat", Term("combat")),
            ("winner", winner),
            ("damage", damage));
    }

    private string UnitName(UnitId id) => _text.EntityName(ModPresentationEntityKind.Unit, id.Value);

    private string PowerName(PowerId id) => _text.EntityName(ModPresentationEntityKind.Power, id.Value);

    private string Term(string key) => _text.Term(key);

    private string Text(string key, params (string Name, object? Value)[] values) =>
        _text.Format(key, values);
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

    public static CombatPlaybackUnitState FromSnapshot(SessionCombatUnitSnapshot snapshot, string name) =>
        new(snapshot.PlayerId, snapshot.InstanceId, name, snapshot.Attack, snapshot.Health);

    public static CombatPlaybackUnitState FromSnapshot(PlayerId playerId, CombatUnitSnapshot snapshot, string name) =>
        new(playerId, snapshot.InstanceId, name, snapshot.Attack, snapshot.Health);
}
