using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Match;

public sealed class MatchState
{
    private readonly List<PlayerState> _players;
    private readonly ReadOnlyCollection<PlayerState> _playersView;
    private readonly Dictionary<PlayerId, PlayerState> _playersById;
    private readonly List<MatchElimination> _eliminations = [];
    private readonly ReadOnlyCollection<MatchElimination> _eliminationsView;
    private readonly Dictionary<PlayerId, int> _placements = [];
    private long _nextUnitInstanceId = 1;
    private long _nextActionInstanceId = 1;
    private long _nextEliminationSequence = 1;

    public MatchPhase Phase { get; private set; } = MatchPhase.Setup;
    public int Round { get; private set; }
    public long Revision { get; private set; }
    public IReadOnlyList<PlayerState> Players => _playersView;
    public IReadOnlyList<MatchElimination> Eliminations => _eliminationsView;
    public EliminatedOpponentSnapshot? LatestEliminatedOpponent { get; private set; }
    public int ActivePlayerCount => _players.Count(player => !player.IsEliminated);
    public PlayerId? WinnerPlayerId =>
        Phase == MatchPhase.Finished
            ? _players.Where(player => !player.IsEliminated).Select(player => (PlayerId?)player.Id).SingleOrDefault()
            : null;

    private MatchState(List<PlayerState> players)
    {
        _players = players;
        _playersView = _players.AsReadOnly();
        _playersById = players.ToDictionary(player => player.Id);
        _eliminationsView = _eliminations.AsReadOnly();
    }

    public static MatchState Create(IEnumerable<PlayerId> playerIds, MatchRules rules)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        ArgumentNullException.ThrowIfNull(rules);
        var ids = playerIds.ToArray();
        ValidatePlayerIds(ids, rules);
        return new MatchState(ids.Select(id => new PlayerState(id, rules.StartingHealth)).ToList());
    }

    public static MatchState Create(IEnumerable<PlayerSetup> playerSetups, MatchRules rules, LeaderCatalog leaderCatalog)
    {
        ArgumentNullException.ThrowIfNull(playerSetups);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(leaderCatalog);
        var setups = playerSetups.ToArray();
        ValidatePlayerIds(setups.Select(setup => setup.PlayerId).ToArray(), rules);
        var players = setups
            .Select(setup => new PlayerState(setup.PlayerId, rules.StartingHealth, leaderCatalog.GetRequired(setup.LeaderId)))
            .ToList();
        return new MatchState(players);
    }

    public bool TryGetPlayer(PlayerId playerId, out PlayerState player) => _playersById.TryGetValue(playerId, out player!);
    public bool TryGetPlacement(PlayerId playerId, out int placement) => _placements.TryGetValue(playerId, out placement);

    internal void BeginPreparation()
    {
        if (Phase is not (MatchPhase.Setup or MatchPhase.Combat)) throw new InvalidOperationException($"Cannot begin preparation from {Phase}.");
        if (ActivePlayerCount <= 1) throw new InvalidOperationException("A finished match cannot begin another preparation round.");
        Round++;
        Phase = MatchPhase.Preparation;
    }

    internal void BeginCombat()
    {
        if (Phase != MatchPhase.Preparation) throw new InvalidOperationException($"Cannot begin combat from {Phase}.");
        if (ActivePlayerCount <= 1) throw new InvalidOperationException("A finished match cannot begin combat.");
        Phase = MatchPhase.Combat;
    }

    internal void RecordEliminations(IReadOnlyList<PlayerId> eliminationSequence, IReadOnlyDictionary<PlayerId, int> healthBeforeCombat)
    {
        ArgumentNullException.ThrowIfNull(eliminationSequence);
        ArgumentNullException.ThrowIfNull(healthBeforeCombat);
        if (eliminationSequence.Count == 0) return;
        if (eliminationSequence.Distinct().Count() != eliminationSequence.Count)
            throw new ArgumentException("Elimination sequence cannot contain duplicate players.", nameof(eliminationSequence));

        var candidates = eliminationSequence.Select(playerId =>
        {
            if (!_playersById.TryGetValue(playerId, out var player))
                throw new ArgumentException($"Unknown eliminated player '{playerId}'.", nameof(eliminationSequence));
            if (!player.IsEliminated) throw new InvalidOperationException($"Player '{playerId}' is not eliminated.");
            if (_placements.ContainsKey(playerId)) throw new InvalidOperationException($"Player '{playerId}' already has a placement.");
            if (!healthBeforeCombat.TryGetValue(playerId, out var healthBefore))
                throw new ArgumentException($"Missing pre-combat health for eliminated player '{playerId}'.", nameof(healthBeforeCombat));
            return (Player: player, HealthBefore: healthBefore);
        }).ToArray();

        var placementByPlayer = candidates
            .OrderByDescending(candidate => candidate.HealthBefore)
            .ThenBy(candidate => candidate.Player.Id.Value)
            .Select((candidate, index) => new { candidate.Player.Id, Placement = ActivePlayerCount + 1 + index })
            .ToDictionary(item => item.Id, item => item.Placement);

        foreach (var playerId in eliminationSequence)
        {
            var candidate = candidates.Single(item => item.Player.Id == playerId);
            var record = new MatchElimination(
                _nextEliminationSequence++, Round, playerId, placementByPlayer[playerId], candidate.HealthBefore, candidate.Player.Health);
            _placements.Add(playerId, record.Placement);
            _eliminations.Add(record);
        }

        var latestPlayer = candidates.Single(item => item.Player.Id == eliminationSequence[^1]).Player;
        LatestEliminatedOpponent = new EliminatedOpponentSnapshot(
            latestPlayer.Id,
            latestPlayer.Tier,
            Round,
            CombatParticipant.FromField(
                latestPlayer.Id,
                latestPlayer.Field,
                latestPlayer.Leader?.CurrentPowerId,
                latestPlayer.EffectHistory.Snapshot()));
    }

    internal void Finish()
    {
        if (Phase != MatchPhase.Combat) throw new InvalidOperationException($"Cannot finish match from {Phase}.");
        if (ActivePlayerCount > 1) throw new InvalidOperationException("Cannot finish while more than one player remains active.");
        var winner = _players.SingleOrDefault(player => !player.IsEliminated);
        if (winner is not null) _placements[winner.Id] = 1;
        Phase = MatchPhase.Finished;
    }

    internal UnitInstance CreateUnit(UnitDefinition definition, UnitInstanceOrigin origin = UnitInstanceOrigin.Pooled)
    {
        var id = new UnitInstanceId(_nextUnitInstanceId++);
        return new UnitInstance(id, definition, origin);
    }

    internal ActionInstance CreateAction(ActionDefinition definition)
    {
        var id = new ActionInstanceId(_nextActionInstanceId++);
        return new ActionInstance(id, definition);
    }

    internal void MarkChanged() => Revision++;

    private static void ValidatePlayerIds(IReadOnlyList<PlayerId> ids, MatchRules rules)
    {
        if (ids.Count < rules.MinimumPlayers || ids.Count > rules.MaximumPlayers)
            throw new ArgumentOutOfRangeException(nameof(ids), $"A match requires between {rules.MinimumPlayers} and {rules.MaximumPlayers} players.");
        if (ids.Distinct().Count() != ids.Count)
            throw new ArgumentException("Player ids must be unique.", nameof(ids));
    }
}
