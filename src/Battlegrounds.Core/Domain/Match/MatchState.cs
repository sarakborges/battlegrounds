using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Players;

namespace Battlegrounds.Core.Domain.Match;

public sealed class MatchState
{
    private readonly List<PlayerState> _players;
    private readonly ReadOnlyCollection<PlayerState> _playersView;
    private readonly Dictionary<PlayerId, PlayerState> _playersById;
    private long _nextMinionInstanceId = 1;

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

    public static MatchState Create(IEnumerable<PlayerId> playerIds)
    {
        if (playerIds is null)
        {
            throw new ArgumentNullException(nameof(playerIds));
        }

        var ids = playerIds.ToArray();
        if (ids.Length is < 2 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(playerIds), "A match requires between 2 and 8 players.");
        }

        if (ids.Distinct().Count() != ids.Length)
        {
            throw new ArgumentException("Player ids must be unique.", nameof(playerIds));
        }

        return new MatchState(ids.Select(id => new PlayerState(id)).ToList());
    }

    public bool TryGetPlayer(PlayerId playerId, out PlayerState player) =>
        _playersById.TryGetValue(playerId, out player!);

    internal void BeginRecruitment()
    {
        if (Phase is not (MatchPhase.Setup or MatchPhase.Combat))
        {
            throw new InvalidOperationException($"Cannot begin recruitment from {Phase}.");
        }

        Round++;
        Phase = MatchPhase.Recruitment;
    }

    internal void BeginCombat()
    {
        if (Phase != MatchPhase.Recruitment)
        {
            throw new InvalidOperationException($"Cannot begin combat from {Phase}.");
        }

        Phase = MatchPhase.Combat;
    }

    internal MinionInstance CreateMinion(CardDefinition definition)
    {
        var id = new MinionInstanceId(_nextMinionInstanceId++);
        return new MinionInstance(id, definition);
    }

    internal void MarkChanged() => Revision++;
}
