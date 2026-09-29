using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Game;

public partial class Main
{
    private object BuildWebCombatState()
    {
        if (_session is null || _combatPlayback is null)
            throw new InvalidOperationException("Web combat state requires an active combat playback.");

        var playback = _combatPlayback;
        var timeline = playback.Settlement.CombatResult.Timeline;
        var currentEvent = playback.CurrentEvent;

        return new
        {
            status = "combat",
            phase = "combat",
            mod = new { id = _session.Mod.Id, name = _session.Mod.Name },
            seed = Seed,
            labels = BuildWebLabels(),
            cosmetics = BuildWebCosmeticsState(),
            theme = BuildWebThemeState(),
            combat = new
            {
                round = playback.Record.Round,
                eventText = playback.EventText,
                eventSequence = currentEvent?.Sequence ?? 0,
                eventCount = timeline.Count,
                eventKind = currentEvent?.Kind.ToString().ToLowerInvariant(),
                settlementVisible = playback.SettlementVisible,
                left = BuildWebCombatSide(
                    playback.LeftPlayerId,
                    playback.LeftUnits,
                    archived: false),
                right = BuildWebCombatSide(
                    playback.RightPlayerId,
                    playback.RightUnits,
                    archived: playback.Settlement.RightPlayerId is null),
            },
        };
    }

    private object BuildWebCombatSide(
        PlayerId playerId,
        IReadOnlyList<CombatPlaybackUnitState> units,
        bool archived)
    {
        if (_session is null)
            throw new InvalidOperationException("Web combat side requires an active session.");

        var label = playerId == _session.HumanPlayerId
            ? Text("ui.sideYou", ("player", playerId.Value))
            : archived
                ? Text("ui.sideArchived", ("player", playerId.Value))
                : Text("ui.sideAi", ("player", playerId.Value));

        return new
        {
            playerId = playerId.Value,
            human = playerId == _session.HumanPlayerId,
            archived,
            label,
            units = units.Select(unit =>
            {
                var unitId = ResolveCombatUnitId(unit.InstanceId);
                return new
                {
                    instanceId = unit.InstanceId.Value,
                    unitId = unitId?.Value,
                    name = unit.Name,
                    description = unitId is UnitId resolvedUnitId ? UnitDescription(resolvedUnitId) : string.Empty,
                    tier = ResolveCombatUnitTier(unitId),
                    attack = unit.Attack,
                    health = Math.Max(0, unit.Health),
                    alive = unit.IsAlive,
                    highlight = unit.Highlight,
                    status = unit.Status,
                };
            }).ToArray(),
        };
    }
}
