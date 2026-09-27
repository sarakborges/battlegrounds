using Battlegrounds.AI;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Playables;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.Simulator;

public sealed record SimulationEntityCount(string Id, long Count);

public sealed record SimulationPlayerEntityTelemetry(
    IReadOnlyList<SimulationEntityCount> UnitOfferAppearances,
    IReadOnlyList<SimulationEntityCount> UnitAcquires,
    IReadOnlyList<SimulationEntityCount> UnitReleases,
    IReadOnlyList<SimulationEntityCount> UnitDeploys,
    IReadOnlyList<SimulationEntityCount> ActionOfferAppearances,
    IReadOnlyList<SimulationEntityCount> ActionAcquires,
    IReadOnlyList<SimulationEntityCount> ActionPlays,
    IReadOnlyList<SimulationEntityCount> CombineExecutions,
    IReadOnlyList<SimulationEntityCount> FinalBoardUnitCopies);

public sealed record SimulationUnitSummary(
    string Id,
    string Name,
    int Tier,
    long OfferAppearances,
    long Acquires,
    double AcquireRate,
    long Releases,
    long Deploys,
    int FinalBoardPlayers,
    long FinalBoardCopies,
    int WinnerFinalBoards,
    double FinalBoardWinRate,
    double? AveragePlacementWhenOnFinalBoard);

public sealed record SimulationActionSummary(
    string Id,
    string Name,
    int Tier,
    long OfferAppearances,
    long Acquires,
    double AcquireRate,
    long Plays,
    double PlaysPerAcquire);

public sealed record SimulationCombineSummary(
    string Id,
    string Name,
    string SourceUnitId,
    int RequiredCopies,
    string ResultUnitId,
    long Executions);

public sealed record SimulationTierSummary(
    int Tier,
    long UnitOfferAppearances,
    long UnitAcquires,
    long ActionOfferAppearances,
    long ActionAcquires,
    long FinalBoardUnitCopies);

public sealed record SimulationEntityReport(
    IReadOnlyList<SimulationUnitSummary> Units,
    IReadOnlyList<SimulationActionSummary> Actions,
    IReadOnlyList<SimulationCombineSummary> Combines,
    IReadOnlyList<SimulationTierSummary> Tiers);

internal sealed class SimulationEntityTracker : IPreparationAiCommandObserver
{
    private readonly Dictionary<PlayerId, PlayerEntityCounts> _players = [];
    private PendingEntityEvent? _pending;

    public void BeginPreparationTurn(PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        RecordOfferSnapshot(player);
    }

    public void BeforeCommand(PlayerState player, IPreparationCommand command)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(command);
        if (_pending is not null)
            throw new InvalidOperationException("Simulation entity observer received overlapping AI commands.");

        _pending = CaptureCommandEntity(player, command);
    }

    public void AfterAcceptedCommand(PlayerState player, IPreparationCommand command)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(command);

        if (_pending is PendingEntityEvent pending)
            Commit(player.Id, pending);
        _pending = null;

        if (command is RefreshOfferCommand)
            RecordOfferSnapshot(player);
    }

    public SimulationPlayerEntityTelemetry BuildPlayerTelemetry(PlayerState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        var counts = GetCounts(player.Id);
        var finalBoard = player.Field
            .GroupBy(unit => unit.Definition.Id.Value, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => (long)group.Count(), StringComparer.Ordinal);

        return new SimulationPlayerEntityTelemetry(
            Snapshot(counts.UnitOfferAppearances),
            Snapshot(counts.UnitAcquires),
            Snapshot(counts.UnitReleases),
            Snapshot(counts.UnitDeploys),
            Snapshot(counts.ActionOfferAppearances),
            Snapshot(counts.ActionAcquires),
            Snapshot(counts.ActionPlays),
            Snapshot(counts.CombineExecutions),
            Snapshot(finalBoard));
    }

    private void RecordOfferSnapshot(PlayerState player)
    {
        var counts = GetCounts(player.Id);
        foreach (var entry in player.PlayableOffer)
        {
            var target = entry.Kind == PlayableKind.Unit
                ? counts.UnitOfferAppearances
                : counts.ActionOfferAppearances;
            Increment(target, entry.Id);
        }
    }

    private static PendingEntityEvent? CaptureCommandEntity(PlayerState player, IPreparationCommand command)
    {
        switch (command)
        {
            case AcquireUnitCommand acquireUnit:
                return CaptureAcquire(player, acquireUnit.OfferSlot);
            case AcquirePlayableCommand acquirePlayable:
                return CaptureAcquire(player, acquirePlayable.OfferSlot);
            case ReleaseUnitCommand release when release.FieldSlot >= 0 && release.FieldSlot < player.Field.Count:
                return new PendingEntityEvent(EntityEventKind.UnitRelease, player.Field[release.FieldSlot].Definition.Id.Value);
            case DeployUnitCommand deploy when deploy.ReserveSlot >= 0 && deploy.ReserveSlot < player.Reserve.Count:
                return new PendingEntityEvent(EntityEventKind.UnitDeploy, player.Reserve[deploy.ReserveSlot].Definition.Id.Value);
            case PlayActionCommand play:
            {
                var entry = player.PlayableReserve.FirstOrDefault(value => value.Slot == play.ReserveSlot);
                return entry?.Action is null
                    ? null
                    : new PendingEntityEvent(EntityEventKind.ActionPlay, entry.Action.Definition.Id.Value);
            }
            case CombineUnitsCommand combine:
                return new PendingEntityEvent(EntityEventKind.Combine, combine.CombineId.Value);
            default:
                return null;
        }
    }

    private static PendingEntityEvent? CaptureAcquire(PlayerState player, int offerSlot)
    {
        var entry = player.PlayableOffer.FirstOrDefault(value => value.Slot == offerSlot);
        if (entry is null) return null;
        return new PendingEntityEvent(
            entry.Kind == PlayableKind.Unit ? EntityEventKind.UnitAcquire : EntityEventKind.ActionAcquire,
            entry.Id);
    }

    private void Commit(PlayerId playerId, PendingEntityEvent pending)
    {
        var counts = GetCounts(playerId);
        var target = pending.Kind switch
        {
            EntityEventKind.UnitAcquire => counts.UnitAcquires,
            EntityEventKind.UnitRelease => counts.UnitReleases,
            EntityEventKind.UnitDeploy => counts.UnitDeploys,
            EntityEventKind.ActionAcquire => counts.ActionAcquires,
            EntityEventKind.ActionPlay => counts.ActionPlays,
            EntityEventKind.Combine => counts.CombineExecutions,
            _ => throw new ArgumentOutOfRangeException(nameof(pending), pending.Kind, "Unsupported simulation entity event."),
        };
        Increment(target, pending.Id);
    }

    private PlayerEntityCounts GetCounts(PlayerId playerId)
    {
        if (_players.TryGetValue(playerId, out var counts)) return counts;
        counts = new PlayerEntityCounts();
        _players.Add(playerId, counts);
        return counts;
    }

    private static void Increment(Dictionary<string, long> values, string id) =>
        values[id] = values.GetValueOrDefault(id) + 1;

    private static IReadOnlyList<SimulationEntityCount> Snapshot(IReadOnlyDictionary<string, long> values) =>
        values
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .Select(value => new SimulationEntityCount(value.Key, value.Value))
            .ToArray();

    private enum EntityEventKind
    {
        UnitAcquire,
        UnitRelease,
        UnitDeploy,
        ActionAcquire,
        ActionPlay,
        Combine,
    }

    private sealed record PendingEntityEvent(EntityEventKind Kind, string Id);

    private sealed class PlayerEntityCounts
    {
        public Dictionary<string, long> UnitOfferAppearances { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> UnitAcquires { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> UnitReleases { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> UnitDeploys { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> ActionOfferAppearances { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> ActionAcquires { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> ActionPlays { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, long> CombineExecutions { get; } = new(StringComparer.Ordinal);
    }
}

internal static class SimulationEntityReportBuilder
{
    public static SimulationEntityReport Build(ModPackage mod, IReadOnlyCollection<SimulationPlayerResult> players)
    {
        ArgumentNullException.ThrowIfNull(mod);
        ArgumentNullException.ThrowIfNull(players);

        var units = mod.Units.All
            .OrderBy(unit => unit.Id.Value, StringComparer.Ordinal)
            .Select(unit => BuildUnit(unit.Id.Value, unit.Name, unit.Tier, players))
            .ToArray();
        var actions = mod.Actions.All
            .OrderBy(action => action.Id.Value, StringComparer.Ordinal)
            .Select(action => BuildAction(action.Id.Value, action.Name, action.Tier, players))
            .ToArray();
        var combines = mod.Combines.All
            .OrderBy(combine => combine.Id.Value, StringComparer.Ordinal)
            .Select(combine => new SimulationCombineSummary(
                combine.Id.Value,
                combine.Name,
                combine.SourceUnitId.Value,
                combine.RequiredCopies,
                combine.ResultUnitId.Value,
                players.Sum(player => Count(player.EntityTelemetry.CombineExecutions, combine.Id.Value))))
            .ToArray();
        var tiers = Enumerable.Range(1, mod.PreparationRules.MaximumTier)
            .Select(tier => new SimulationTierSummary(
                tier,
                units.Where(unit => unit.Tier == tier).Sum(unit => unit.OfferAppearances),
                units.Where(unit => unit.Tier == tier).Sum(unit => unit.Acquires),
                actions.Where(action => action.Tier == tier).Sum(action => action.OfferAppearances),
                actions.Where(action => action.Tier == tier).Sum(action => action.Acquires),
                units.Where(unit => unit.Tier == tier).Sum(unit => unit.FinalBoardCopies)))
            .ToArray();

        return new SimulationEntityReport(units, actions, combines, tiers);
    }

    private static SimulationUnitSummary BuildUnit(
        string id,
        string name,
        int tier,
        IReadOnlyCollection<SimulationPlayerResult> players)
    {
        var appearances = players.Sum(player => Count(player.EntityTelemetry.UnitOfferAppearances, id));
        var acquires = players.Sum(player => Count(player.EntityTelemetry.UnitAcquires, id));
        var releases = players.Sum(player => Count(player.EntityTelemetry.UnitReleases, id));
        var deploys = players.Sum(player => Count(player.EntityTelemetry.UnitDeploys, id));
        var finalPlayers = players
            .Where(player => Count(player.EntityTelemetry.FinalBoardUnitCopies, id) > 0)
            .ToArray();
        var finalCopies = finalPlayers.Sum(player => Count(player.EntityTelemetry.FinalBoardUnitCopies, id));
        var winnerFinalBoards = finalPlayers.Count(player => player.Placement == 1);

        return new SimulationUnitSummary(
            id,
            name,
            tier,
            appearances,
            acquires,
            Ratio(acquires, appearances),
            releases,
            deploys,
            finalPlayers.Length,
            finalCopies,
            winnerFinalBoards,
            Ratio(winnerFinalBoards, finalPlayers.Length),
            finalPlayers.Length == 0 ? null : finalPlayers.Average(player => player.Placement));
    }

    private static SimulationActionSummary BuildAction(
        string id,
        string name,
        int tier,
        IReadOnlyCollection<SimulationPlayerResult> players)
    {
        var appearances = players.Sum(player => Count(player.EntityTelemetry.ActionOfferAppearances, id));
        var acquires = players.Sum(player => Count(player.EntityTelemetry.ActionAcquires, id));
        var plays = players.Sum(player => Count(player.EntityTelemetry.ActionPlays, id));
        return new SimulationActionSummary(
            id,
            name,
            tier,
            appearances,
            acquires,
            Ratio(acquires, appearances),
            plays,
            Ratio(plays, acquires));
    }

    private static long Count(IReadOnlyList<SimulationEntityCount> values, string id) =>
        values.FirstOrDefault(value => string.Equals(value.Id, id, StringComparison.Ordinal))?.Count ?? 0;

    private static double Ratio(long numerator, long denominator) =>
        denominator == 0 ? 0d : numerator / (double)denominator;
}
