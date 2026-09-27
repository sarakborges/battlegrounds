using System.Collections.ObjectModel;
using Battlegrounds.AI;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Simulator;

public sealed record SimulationRunOptions(
    int Matches,
    int Players,
    int Seed,
    int MaximumCommandsPerPreparation,
    int MaximumRounds);

public sealed record SimulationPlayerResult(
    int MatchIndex,
    int Seed,
    int PlayerId,
    string LeaderId,
    string Personality,
    string Strategy,
    int Placement,
    int Rounds,
    int FinalTier,
    int FinalHealth,
    long PreparationCommands);

public sealed record SimulationMatchResult(
    int MatchIndex,
    int Seed,
    int Rounds,
    IReadOnlyList<SimulationPlayerResult> Players);

public sealed record SimulationGroupSummary(
    string Id,
    int Games,
    int Wins,
    double WinRate,
    double AveragePlacement,
    double AveragePreparationCommands);

public sealed record SimulationReport(
    string ModId,
    string ModName,
    int Matches,
    int PlayersPerMatch,
    int BaseSeed,
    double AverageRounds,
    long TotalPreparationCommands,
    IReadOnlyList<SimulationGroupSummary> Leaders,
    IReadOnlyList<SimulationGroupSummary> Personalities,
    IReadOnlyList<SimulationGroupSummary> Strategies,
    IReadOnlyList<SimulationMatchResult> MatchResults);

public sealed class SimulationRunner
{
    private static readonly PreparationAiPersonality[] AvailablePersonalities =
        Enum.GetValues<PreparationAiPersonality>();

    public SimulationReport Run(ModPackage mod, SimulationRunOptions options)
    {
        ArgumentNullException.ThrowIfNull(mod);
        ValidateOptions(mod, options);

        var results = new List<SimulationMatchResult>(options.Matches);
        for (var matchIndex = 0; matchIndex < options.Matches; matchIndex++)
        {
            var seed = unchecked(options.Seed + matchIndex);
            results.Add(RunMatch(mod, options, matchIndex, seed));
        }

        var players = results.SelectMany(result => result.Players).ToArray();
        return new SimulationReport(
            mod.Id,
            mod.Name,
            options.Matches,
            options.Players,
            options.Seed,
            results.Average(result => result.Rounds),
            players.Sum(player => player.PreparationCommands),
            Summarize(players, player => player.LeaderId),
            Summarize(players, player => player.Personality),
            Summarize(players, player => player.Strategy),
            new ReadOnlyCollection<SimulationMatchResult>(results));
    }

    private static SimulationMatchResult RunMatch(
        ModPackage mod,
        SimulationRunOptions options,
        int matchIndex,
        int seed)
    {
        var random = new SeededRandomSource(seed);
        var playerIds = Enumerable.Range(0, options.Players)
            .Select(value => new PlayerId(value))
            .ToArray();
        var selection = mod.CreateLeaderSelection(playerIds, random);
        var agent = new PreparationAiAgent(
            mod.PreparationRules,
            random,
            mod.Leaders,
            mod.Powers,
            mod.Combines);
        var engine = mod.CreateMatchEngine(random);
        var pairingPolicy = new HistoryAwareCombatPairingPolicy();

        var personalities = playerIds
            .OrderBy(id => id.Value)
            .ToDictionary(
                id => id,
                _ => AvailablePersonalities[random.NextInt(0, AvailablePersonalities.Length)]);
        var availableStrategies = BuildAvailableStrategies(mod);
        var strategies = playerIds
            .OrderBy(id => id.Value)
            .ToDictionary(
                id => id,
                _ => availableStrategies[random.NextInt(0, availableStrategies.Length)]);
        var leaders = new Dictionary<PlayerId, LeaderId>();
        foreach (var playerId in playerIds.OrderBy(id => id.Value))
            leaders.Add(playerId, agent.SelectLeader(selection, playerId));

        var match = engine.CreateMatch(selection.GetCompletedPlayerSetups());
        engine.BeginMatch(match);
        var preparationCommands = playerIds.ToDictionary(id => id, _ => 0L);

        while (match.Phase != MatchPhase.Finished)
        {
            if (match.Round > options.MaximumRounds)
            {
                throw new InvalidOperationException(
                    $"Simulation match {matchIndex} exceeded the {options.MaximumRounds}-round safety limit.");
            }
            if (match.Phase != MatchPhase.Preparation)
            {
                throw new InvalidOperationException(
                    $"Simulation expected Preparation at round {match.Round}, but match is in {match.Phase}.");
            }

            var initiative = match.Players
                .Where(player => !player.IsEliminated)
                .Select(player => player.Id)
                .ToArray();
            Shuffle(initiative, random);

            foreach (var playerId in initiative)
            {
                if (!match.TryGetPlayer(playerId, out var player) || player.IsEliminated)
                    continue;

                var result = agent.PlayPreparation(
                    engine,
                    match,
                    playerId,
                    options.MaximumCommandsPerPreparation,
                    personalities[playerId],
                    strategies[playerId]);
                preparationCommands[playerId] += result.CommandsExecuted;
            }

            if (match.Phase != MatchPhase.Combat)
            {
                throw new InvalidOperationException(
                    $"Simulation round {match.Round} did not reach Combat after every active AI completed Preparation.");
            }

            var pairings = pairingPolicy.CreatePairings(match, random);
            engine.ResolveCombatRound(match, pairings);
        }

        var playerResults = playerIds
            .OrderBy(id => id.Value)
            .Select(playerId =>
            {
                if (!match.TryGetPlayer(playerId, out var player))
                    throw new InvalidOperationException($"Simulation lost player '{playerId}'.");
                if (!match.TryGetPlacement(playerId, out var placement))
                    throw new InvalidOperationException($"Simulation finished without placement for player '{playerId}'.");

                return new SimulationPlayerResult(
                    matchIndex,
                    seed,
                    playerId.Value,
                    leaders[playerId].Value,
                    personalities[playerId].ToString(),
                    strategies[playerId].Id,
                    placement,
                    match.Round,
                    player.Tier,
                    player.Health,
                    preparationCommands[playerId]);
            })
            .ToArray();

        return new SimulationMatchResult(matchIndex, seed, match.Round, Array.AsReadOnly(playerResults));
    }

    private static PreparationAiStrategy[] BuildAvailableStrategies(ModPackage mod)
    {
        var typeStrategies = mod.Units.All
            .SelectMany(unit => unit.Types)
            .Select(type => type.Id)
            .Distinct()
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .Select(PreparationAiStrategy.PreferType);

        return new[] { PreparationAiStrategy.Balanced }
            .Concat(typeStrategies)
            .ToArray();
    }

    private static IReadOnlyList<SimulationGroupSummary> Summarize(
        IReadOnlyCollection<SimulationPlayerResult> players,
        Func<SimulationPlayerResult, string> selector) =>
        players
            .GroupBy(selector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var values = group.ToArray();
                var wins = values.Count(player => player.Placement == 1);
                return new SimulationGroupSummary(
                    group.Key,
                    values.Length,
                    wins,
                    wins / (double)values.Length,
                    values.Average(player => player.Placement),
                    values.Average(player => player.PreparationCommands));
            })
            .ToArray();

    private static void Shuffle<T>(T[] values, IRandomSource random)
    {
        for (var index = values.Length - 1; index > 0; index--)
        {
            var swapIndex = random.NextInt(0, index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private static void ValidateOptions(ModPackage mod, SimulationRunOptions options)
    {
        if (options.Matches <= 0) throw new ArgumentOutOfRangeException(nameof(options.Matches));
        if (options.Players < mod.MatchRules.MinimumPlayers || options.Players > mod.MatchRules.MaximumPlayers)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.Players),
                $"Mod '{mod.Id}' requires between {mod.MatchRules.MinimumPlayers} and {mod.MatchRules.MaximumPlayers} players.");
        }
        if (options.Players % 2 != 0)
        {
            throw new ArgumentException(
                "The current combat contract requires an even initial player count because round one has no eliminated-opponent snapshot.",
                nameof(options.Players));
        }
        if (options.MaximumCommandsPerPreparation <= 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumCommandsPerPreparation));
        if (options.MaximumRounds <= 0) throw new ArgumentOutOfRangeException(nameof(options.MaximumRounds));
    }
}
