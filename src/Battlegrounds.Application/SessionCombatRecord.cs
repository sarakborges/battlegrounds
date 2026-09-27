using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;

namespace Battlegrounds.Application;

public sealed record SessionCombatUnitSnapshot(
    PlayerId PlayerId,
    UnitInstanceId InstanceId,
    UnitId UnitId,
    string Name,
    int Tier,
    int Attack,
    int Health);

/// <summary>
/// Immutable client-facing observation of one resolved combat round.
/// StartingUnits is captured before Core resolves combat; RoundResult is the authoritative immutable result.
/// This record is not gameplay state and is never read by Core simulation.
/// </summary>
public sealed class SessionCombatRecord
{
    private readonly ReadOnlyCollection<CombatPairing> _pairings;
    private readonly ReadOnlyCollection<SessionCombatUnitSnapshot> _startingUnits;

    public long Sequence { get; }
    public int Round { get; }
    public IReadOnlyList<CombatPairing> Pairings => _pairings;
    public IReadOnlyList<SessionCombatUnitSnapshot> StartingUnits => _startingUnits;
    public CombatRoundResult RoundResult { get; }

    internal SessionCombatRecord(
        long sequence,
        int round,
        IEnumerable<CombatPairing> pairings,
        IEnumerable<SessionCombatUnitSnapshot> startingUnits,
        CombatRoundResult roundResult)
    {
        if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
        if (round <= 0) throw new ArgumentOutOfRangeException(nameof(round));
        ArgumentNullException.ThrowIfNull(pairings);
        ArgumentNullException.ThrowIfNull(startingUnits);
        RoundResult = roundResult ?? throw new ArgumentNullException(nameof(roundResult));

        Sequence = sequence;
        Round = round;
        _pairings = Array.AsReadOnly(pairings.ToArray());
        _startingUnits = Array.AsReadOnly(startingUnits.ToArray());
    }
}
