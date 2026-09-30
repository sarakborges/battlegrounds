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
            players = BuildWebCombatPlayersState(),
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

    private object[] BuildWebCombatPlayersState()
    {
        if (_session?.Match is null)
            return [];

        return _session.Match.Players
            .OrderBy(player => player.Id.Value)
            .Select(player => (object)new
            {
                id = player.Id.Value,
                human = player.Id == _session.HumanPlayerId,
                health = player.Health,
                armor = player.Leader?.Armor ?? 0,
                tier = player.Tier,
                eliminated = player.IsEliminated,
                ready = player.IsReadyForCombat,
                leaderId = player.Leader?.Definition.Id.Value,
                leader = player.Leader is null ? null : LeaderName(player.Leader.Definition.Id),
                leaderDescription = player.Leader is null ? null : LeaderDescription(player.Leader.Definition.Id),
            })
            .ToArray();
    }

    private object BuildWebCombatSide(
        PlayerId playerId,
        IReadOnlyList<CombatPlaybackUnitState> units,
        bool archived)
    {
        if (_session is null)
            throw new InvalidOperationException("Web combat side requires an active session.");
        if (_session.Match is null || !_session.Match.TryGetPlayer(playerId, out var player))
            throw new InvalidOperationException($"Combat player '{playerId.Value}' is missing from the active match.");

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
            health = player.Health,
            armor = player.Leader?.Armor ?? 0,
            tier = player.Tier,
            leaderId = player.Leader?.Definition.Id.Value,
            leader = player.Leader is null ? null : LeaderName(player.Leader.Definition.Id),
            leaderDescription = player.Leader is null ? null : LeaderDescription(player.Leader.Definition.Id),
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
