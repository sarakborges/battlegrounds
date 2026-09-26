using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Match;

public enum LeaderSelectionFailureCode
{
    PlayerNotFound,
    PlayerAlreadySelected,
    LeaderNotOffered,
}

public readonly record struct LeaderSelectionResult
{
    public bool Succeeded { get; }
    public LeaderSelectionFailureCode? FailureCode { get; }

    private LeaderSelectionResult(bool succeeded, LeaderSelectionFailureCode? failureCode)
    {
        Succeeded = succeeded;
        FailureCode = failureCode;
    }

    public static LeaderSelectionResult Success() => new(true, null);

    public static LeaderSelectionResult Failure(LeaderSelectionFailureCode failureCode) =>
        new(false, failureCode);
}

public sealed class LeaderSelectionState
{
    private readonly ReadOnlyCollection<PlayerId> _players;
    private readonly Dictionary<PlayerId, ReadOnlyCollection<LeaderId>> _offers;
    private readonly Dictionary<PlayerId, LeaderId> _selections = [];

    public IReadOnlyList<PlayerId> Players => _players;
    public int SelectedCount => _selections.Count;
    public bool IsComplete => SelectedCount == _players.Count;

    private LeaderSelectionState(
        IEnumerable<PlayerId> players,
        Dictionary<PlayerId, ReadOnlyCollection<LeaderId>> offers)
    {
        _players = Array.AsReadOnly(players.ToArray());
        _offers = offers;
    }

    public static LeaderSelectionState Create(
        IEnumerable<PlayerId> playerIds,
        MatchRules rules,
        LeaderCatalog leaderCatalog,
        IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(leaderCatalog);
        ArgumentNullException.ThrowIfNull(randomSource);

        var players = playerIds.OrderBy(id => id.Value).ToArray();
        if (players.Length < rules.MinimumPlayers || players.Length > rules.MaximumPlayers)
        {
            throw new ArgumentOutOfRangeException(
                nameof(playerIds),
                $"Player count must be between {rules.MinimumPlayers} and {rules.MaximumPlayers}.");
        }

        var duplicate = players.GroupBy(id => id).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Duplicate player id '{duplicate.Key}'.", nameof(playerIds));
        }

        var offers = rules.LeaderOfferPolicy switch
        {
            LeaderOfferPolicy.IndependentPerPlayer => CreateIndependentOffers(players, rules.LeaderOfferSize, leaderCatalog, randomSource),
            LeaderOfferPolicy.UniqueAcrossMatch => CreateUniqueOffers(players, rules.LeaderOfferSize, leaderCatalog, randomSource),
            _ => throw new ArgumentOutOfRangeException(nameof(rules.LeaderOfferPolicy), rules.LeaderOfferPolicy, "Unsupported leader offer policy."),
        };

        return new LeaderSelectionState(players, offers);
    }

    public IReadOnlyList<LeaderId> GetOffer(PlayerId playerId)
    {
        if (!_offers.TryGetValue(playerId, out var offer))
        {
            throw new KeyNotFoundException($"Player '{playerId}' is not part of this leader selection.");
        }

        return offer;
    }

    public bool TryGetSelection(PlayerId playerId, out LeaderId leaderId) =>
        _selections.TryGetValue(playerId, out leaderId);

    public LeaderSelectionResult Select(PlayerId playerId, LeaderId leaderId)
    {
        if (!_offers.TryGetValue(playerId, out var offer))
        {
            return LeaderSelectionResult.Failure(LeaderSelectionFailureCode.PlayerNotFound);
        }

        if (_selections.ContainsKey(playerId))
        {
            return LeaderSelectionResult.Failure(LeaderSelectionFailureCode.PlayerAlreadySelected);
        }

        if (!offer.Contains(leaderId))
        {
            return LeaderSelectionResult.Failure(LeaderSelectionFailureCode.LeaderNotOffered);
        }

        _selections.Add(playerId, leaderId);
        return LeaderSelectionResult.Success();
    }

    public IReadOnlyList<PlayerSetup> GetCompletedPlayerSetups()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException("Every player must select a leader before the match can be created.");
        }

        return _players
            .Select(playerId => new PlayerSetup(playerId, _selections[playerId]))
            .ToArray();
    }

    private static Dictionary<PlayerId, ReadOnlyCollection<LeaderId>> CreateIndependentOffers(
        IReadOnlyList<PlayerId> players,
        int offerSize,
        LeaderCatalog catalog,
        IRandomSource randomSource)
    {
        if (catalog.All.Count < offerSize)
        {
            throw new InvalidOperationException(
                $"Leader catalog contains {catalog.All.Count} leaders but independent offers require at least {offerSize}.");
        }

        var result = new Dictionary<PlayerId, ReadOnlyCollection<LeaderId>>();
        foreach (var playerId in players)
        {
            var candidates = catalog.All.Select(leader => leader.Id).ToArray();
            Shuffle(candidates, randomSource);
            result.Add(playerId, Array.AsReadOnly(candidates.Take(offerSize).ToArray()));
        }

        return result;
    }

    private static Dictionary<PlayerId, ReadOnlyCollection<LeaderId>> CreateUniqueOffers(
        IReadOnlyList<PlayerId> players,
        int offerSize,
        LeaderCatalog catalog,
        IRandomSource randomSource)
    {
        var required = checked(players.Count * offerSize);
        if (catalog.All.Count < required)
        {
            throw new InvalidOperationException(
                $"Leader catalog contains {catalog.All.Count} leaders but unique offers require {required}.");
        }

        var candidates = catalog.All.Select(leader => leader.Id).ToArray();
        Shuffle(candidates, randomSource);

        var result = new Dictionary<PlayerId, ReadOnlyCollection<LeaderId>>();
        var cursor = 0;
        foreach (var playerId in players)
        {
            result.Add(playerId, Array.AsReadOnly(candidates.Skip(cursor).Take(offerSize).ToArray()));
            cursor += offerSize;
        }

        return result;
    }

    private static void Shuffle<T>(T[] values, IRandomSource randomSource)
    {
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swapIndex = randomSource.NextInt(0, index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}
