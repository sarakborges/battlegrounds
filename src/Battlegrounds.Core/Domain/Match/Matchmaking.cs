using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Match;

public interface ICombatPairingPolicy
{
    IReadOnlyList<CombatPairing> CreatePairings(MatchState match, IRandomSource randomSource);
}

/// <summary>
/// Neutral history-aware pairing policy. Live opponents with fewer prior meetings are preferred;
/// ties prefer the least-recent meeting and then use the injected RNG. When an eliminated-opponent
/// pairing is required, it is assigned to the active player who has received it least often.
/// </summary>
public sealed class HistoryAwareCombatPairingPolicy : ICombatPairingPolicy
{
    public IReadOnlyList<CombatPairing> CreatePairings(MatchState match, IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(randomSource);
        if (match.Phase != MatchPhase.Combat)
        {
            throw new InvalidOperationException(
                $"Combat pairings can only be created while the match is in Combat, not {match.Phase}.");
        }

        var remaining = match.Players
            .Where(player => !player.IsEliminated)
            .Select(player => player.Id)
            .OrderBy(id => id.Value)
            .ToList();
        if (remaining.Count <= 1)
        {
            throw new InvalidOperationException("Combat pairing requires at least two active players.");
        }

        var result = new List<CombatPairing>((remaining.Count + 1) / 2);
        if (remaining.Count % 2 != 0)
        {
            if (match.LatestEliminatedOpponent is null)
            {
                throw new InvalidOperationException(
                    "An odd active-player count requires a snapshot from a previously eliminated player.");
            }

            var eliminatedOpponentPlayer = ChooseEliminatedOpponentPlayer(match, remaining, randomSource);
            result.Add(CombatPairing.VersusEliminatedOpponent(eliminatedOpponentPlayer));
            remaining.Remove(eliminatedOpponentPlayer);
        }

        while (remaining.Count > 0)
        {
            var leftPlayerId = remaining[0];
            remaining.RemoveAt(0);
            var rightPlayerId = ChooseLiveOpponent(match, leftPlayerId, remaining, randomSource);
            remaining.Remove(rightPlayerId);
            result.Add(new CombatPairing(leftPlayerId, rightPlayerId));
        }

        return Array.AsReadOnly(result.ToArray());
    }

    private static PlayerId ChooseEliminatedOpponentPlayer(
        MatchState match,
        IReadOnlyList<PlayerId> candidates,
        IRandomSource randomSource)
    {
        var scored = candidates
            .Select(playerId =>
            {
                var history = match.CombatPairingHistory
                    .Where(entry => entry.UsesEliminatedOpponent && entry.LeftPlayerId == playerId)
                    .ToArray();
                return new EliminatedOpponentCandidate(
                    playerId,
                    history.Length,
                    history.Length == 0 ? int.MinValue : history.Max(entry => entry.Round));
            })
            .ToArray();
        var minimumCount = scored.Min(candidate => candidate.Count);
        var oldestRound = scored
            .Where(candidate => candidate.Count == minimumCount)
            .Min(candidate => candidate.LastRound);
        var tied = scored
            .Where(candidate => candidate.Count == minimumCount && candidate.LastRound == oldestRound)
            .OrderBy(candidate => candidate.PlayerId.Value)
            .ToArray();
        return tied[randomSource.NextInt(0, tied.Length)].PlayerId;
    }

    private static PlayerId ChooseLiveOpponent(
        MatchState match,
        PlayerId leftPlayerId,
        IReadOnlyList<PlayerId> candidates,
        IRandomSource randomSource)
    {
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("A live combat pairing requires an opponent candidate.");
        }

        var scored = candidates
            .Select(playerId =>
            {
                var history = match.CombatPairingHistory
                    .Where(entry => IsLiveMeeting(entry, leftPlayerId, playerId))
                    .ToArray();
                return new LiveOpponentCandidate(
                    playerId,
                    history.Length,
                    history.Length == 0 ? int.MinValue : history.Max(entry => entry.Round));
            })
            .ToArray();
        var minimumMeetings = scored.Min(candidate => candidate.Meetings);
        var oldestRound = scored
            .Where(candidate => candidate.Meetings == minimumMeetings)
            .Min(candidate => candidate.LastRound);
        var tied = scored
            .Where(candidate => candidate.Meetings == minimumMeetings && candidate.LastRound == oldestRound)
            .OrderBy(candidate => candidate.PlayerId.Value)
            .ToArray();
        return tied[randomSource.NextInt(0, tied.Length)].PlayerId;
    }

    private static bool IsLiveMeeting(MatchCombatPairing entry, PlayerId leftPlayerId, PlayerId rightPlayerId) =>
        !entry.UsesEliminatedOpponent &&
        ((entry.LeftPlayerId == leftPlayerId && entry.RightPlayerId == rightPlayerId) ||
         (entry.LeftPlayerId == rightPlayerId && entry.RightPlayerId == leftPlayerId));

    private readonly record struct LiveOpponentCandidate(PlayerId PlayerId, int Meetings, int LastRound);
    private readonly record struct EliminatedOpponentCandidate(PlayerId PlayerId, int Count, int LastRound);
}
