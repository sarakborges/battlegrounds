using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Match;

public sealed class MatchState
{
    private readonly List<PlayerState> _players;
    private readonly ReadOnlyCollection<PlayerState> _playersView;
    private readonly Dictionary<PlayerId, PlayerState> _playersById;
    private long _nextUnitInstanceId = 1;

    public MatchPhase Phase { get; private set; } = MatchPhase.Setup;
    public int Round { get; private set; }
    public long Revision { get; private set; }
    public IReadOnlyList<PlayerState> Players => _playersView;

    private MatchState(List<PlayerState> players)
    {
        _players = players;
        _playersView = _players.AsReadOnly();
        _playersById = players.ToDictionary(player => player.Id);
    }

    public static MatchState Create(IEnumerable<PlayerId> playerIds, MatchRules rules)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        ArgumentNullException.ThrowIfNull(rules);

        var ids = playerIds.ToArray();
        if (ids.Length < rules.MinimumPlayers || ids.Length > rules.MaximumPlayers)
        {
            throw new ArgumentOutOfRangeException(
                nameof(playerIds),
                $"A match requires between {rules.MinimumPlayers} and {rules.MaximumPlayers} players.");
        }

        if (ids.Distinct().Count() != ids.Length)
        {
            throw new ArgumentException("Player ids must be unique.", nameof(playerIds));
        }

        return new MatchState(ids.Select(id => new PlayerState(id)).ToList());
    }

    public bool TryGetPlayer(PlayerId playerId, out PlayerState player) =>
        _playersById.TryGetValue(playerId, out player!);

    internal void BeginPreparation()
    {
        if (Phase is not (MatchPhase.Setup or MatchPhase.Combat))
        {
            throw new InvalidOperationException($"Cannot begin preparation from {Phase}.");
        }

        Round++;
        Phase = MatchPhase.Preparation;
    }

    internal void BeginCombat()
    {
        if (Phase != MatchPhase.Preparation)
        {
            throw new InvalidOperationException($"Cannot begin combat from {Phase}.");
        }

        Phase = MatchPhase.Combat;
    }

    internal UnitInstance CreateUnit(UnitDefinition definition)
    {
        var id = new UnitInstanceId(_nextUnitInstanceId++);
        return new UnitInstance(id, definition);
    }

    internal void MarkChanged() => Revision++;
}
