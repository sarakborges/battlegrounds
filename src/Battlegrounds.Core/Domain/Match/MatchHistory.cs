using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Core.Domain.Match;

public sealed record MatchElimination(
    long Sequence,
    int Round,
    PlayerId PlayerId,
    int Placement,
    int HealthBeforeCombat,
    int HealthAfterCombat);

public sealed class EliminatedOpponentSnapshot
{
    public PlayerId SourcePlayerId { get; }
    public int Tier { get; }
    public int EliminatedRound { get; }
    public CombatParticipant Participant { get; }

    internal EliminatedOpponentSnapshot(
        PlayerId sourcePlayerId,
        int tier,
        int eliminatedRound,
        CombatParticipant participant)
    {
        if (tier <= 0) throw new ArgumentOutOfRangeException(nameof(tier));
        ArgumentNullException.ThrowIfNull(participant);
        if (participant.PlayerId != sourcePlayerId)
        {
            throw new ArgumentException(
                "Eliminated-opponent participant must keep the source player's id.",
                nameof(participant));
        }

        SourcePlayerId = sourcePlayerId;
        Tier = tier;
        EliminatedRound = eliminatedRound;
        Participant = participant;
    }
}
