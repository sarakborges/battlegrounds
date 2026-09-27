using System.Globalization;
using System.Text;
using System.Text.Json;
using Battlegrounds.AI;
using Battlegrounds.Content;
using Battlegrounds.Simulator;

return SimulatorProgram.Run(args);

internal static class SimulatorProgram
{
    public static int Run(string[] args)
    {
        try
        {
            var cli = Parse(args);
            if (cli.ShowHelp)
            {
                PrintHelp();
                return 0;
            }

            var mod = new ModLoader().Load(cli.ModPath);
            var players = cli.Players ?? ChooseDefaultPlayerCount(mod);
            var options = new SimulationRunOptions(
                cli.Matches,
                players,
                cli.Seed,
                cli.MaximumCommandsPerPreparation,
                cli.MaximumRounds);
            var report = new SimulationRunner().Run(mod, options);

            PrintReport(report);
            if (cli.JsonPath is not null) WriteJson(report, cli.JsonPath);
            if (cli.CsvPath is not null) WriteCsv(report, cli.CsvPath);
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
        {
            Console.Error.WriteLine($"error: {exception.Message}");
            Console.Error.WriteLine("Use --help for usage.");
            return 2;
        }
    }

    private static CliOptions Parse(IReadOnlyList<string> args)
    {
        var result = new CliOptions();
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "-h" or "--help":
                    result.ShowHelp = true;
                    break;
                case "--mod":
                    result.ModPath = RequireValue(args, ref index, argument);
                    break;
                case "--matches":
                    result.Matches = ParsePositiveInt(RequireValue(args, ref index, argument), argument);
                    break;
                case "--players":
                    result.Players = ParsePositiveInt(RequireValue(args, ref index, argument), argument);
                    break;
                case "--seed":
                    result.Seed = ParseInt(RequireValue(args, ref index, argument), argument);
                    break;
                case "--max-commands":
                    result.MaximumCommandsPerPreparation = ParsePositiveInt(RequireValue(args, ref index, argument), argument);
                    break;
                case "--max-rounds":
                    result.MaximumRounds = ParsePositiveInt(RequireValue(args, ref index, argument), argument);
                    break;
                case "--json":
                    result.JsonPath = RequireValue(args, ref index, argument);
                    break;
                case "--csv":
                    result.CsvPath = RequireValue(args, ref index, argument);
                    break;
                default:
                    throw new ArgumentException($"Unknown argument '{argument}'.");
            }
        }

        return result;
    }

    private static string RequireValue(IReadOnlyList<string> args, ref int index, string argument)
    {
        if (index + 1 >= args.Count) throw new ArgumentException($"Argument '{argument}' requires a value.");
        index++;
        return args[index];
    }

    private static int ParsePositiveInt(string value, string argument)
    {
        var parsed = ParseInt(value, argument);
        if (parsed <= 0) throw new ArgumentException($"Argument '{argument}' must be greater than zero.");
        return parsed;
    }

    private static int ParseInt(string value, string argument)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw new ArgumentException($"Argument '{argument}' expects an integer, got '{value}'.");
        return parsed;
    }

    private static int ChooseDefaultPlayerCount(ModPackage mod)
    {
        var candidates = Enumerable
            .Range(mod.MatchRules.MinimumPlayers, mod.MatchRules.MaximumPlayers - mod.MatchRules.MinimumPlayers + 1)
            .Where(value => value % 2 == 0)
            .OrderBy(value => value)
            .ToArray();
        if (candidates.Length == 0)
            throw new InvalidOperationException($"Mod '{mod.Id}' has no supported even player count under the current round-one combat contract.");
        return candidates[0];
    }

    private static void PrintReport(SimulationReport report)
    {
        Console.WriteLine($"Mod: {report.ModName} ({report.ModId})");
        Console.WriteLine($"Matches: {report.Matches}");
        Console.WriteLine($"Players/match: {report.PlayersPerMatch}");
        Console.WriteLine($"Base seed: {report.BaseSeed}");
        Console.WriteLine($"Average rounds: {report.AverageRounds:F2}");
        Console.WriteLine($"Preparation commands: {report.TotalPreparationCommands}");
        Console.WriteLine();
        PrintCommandCounts(report.TotalCommandCounts);
        PrintGroup("Leaders", report.Leaders);
        PrintGroup("Personalities", report.Personalities);
        PrintGroup("Strategies", report.Strategies);
        PrintUnits(report.EntityTelemetry.Units);
        PrintActions(report.EntityTelemetry.Actions);
        PrintCombines(report.EntityTelemetry.Combines);
        PrintTiers(report.EntityTelemetry.Tiers);
    }

    private static void PrintCommandCounts(PreparationAiCommandCounts counts)
    {
        Console.WriteLine("Command mix");
        Console.WriteLine($"  acquire:        {counts.Acquires}");
        Console.WriteLine($"  release:        {counts.Releases}");
        Console.WriteLine($"  deploy:         {counts.Deploys}");
        Console.WriteLine($"  action:         {counts.ActionsPlayed}");
        Console.WriteLine($"  combine:        {counts.Combines}");
        Console.WriteLine($"  refresh:        {counts.Refreshes}");
        Console.WriteLine($"  upgrade:        {counts.Upgrades}");
        Console.WriteLine($"  power:          {counts.PowersUsed}");
        Console.WriteLine($"  unit choice:    {counts.UnitChoicesResolved}");
        Console.WriteLine($"  action choice:  {counts.ActionChoicesResolved}");
        Console.WriteLine($"  freeze:         {counts.Freezes}");
        Console.WriteLine($"  unfreeze:       {counts.Unfreezes}");
        Console.WriteLine($"  end:            {counts.Ends}");
        Console.WriteLine();
    }

    private static void PrintGroup(string title, IReadOnlyList<SimulationGroupSummary> summaries)
    {
        Console.WriteLine(title);
        Console.WriteLine("  id                         games   wins   win%   avg place   avg cmds   buy   roll   upg");
        foreach (var summary in summaries)
        {
            Console.WriteLine(
                $"  {summary.Id,-26} {summary.Games,5} {summary.Wins,6} {summary.WinRate * 100,6:F1} {summary.AveragePlacement,10:F2} {summary.AveragePreparationCommands,10:F1} {summary.AverageCommands.Acquires,5:F1} {summary.AverageCommands.Refreshes,6:F1} {summary.AverageCommands.Upgrades,5:F1}");
        }
        Console.WriteLine();
    }

    private static void PrintUnits(IReadOnlyList<SimulationUnitSummary> units)
    {
        Console.WriteLine("Units (buy rate)");
        Console.WriteLine("  id                         tier   offer   buys   buy%   sell   deploy   finalP   final#   win%   avg place");
        foreach (var unit in units.OrderByDescending(value => value.AcquireRate).ThenByDescending(value => value.OfferAppearances).ThenBy(value => value.Id, StringComparer.Ordinal))
        {
            var averagePlacement = unit.AveragePlacementWhenOnFinalBoard?.ToString("F2", CultureInfo.InvariantCulture) ?? "-";
            Console.WriteLine(
                $"  {unit.Id,-26} {unit.Tier,4} {unit.OfferAppearances,7} {unit.Acquires,6} {unit.AcquireRate * 100,6:F1} {unit.Releases,6} {unit.Deploys,8} {unit.FinalBoardPlayers,8} {unit.FinalBoardCopies,8} {unit.FinalBoardWinRate * 100,6:F1} {averagePlacement,10}");
        }
        Console.WriteLine();
    }

    private static void PrintActions(IReadOnlyList<SimulationActionSummary> actions)
    {
        if (actions.Count == 0) return;
        Console.WriteLine("Actions (acquire/use rate)");
        Console.WriteLine("  id                         tier   offer   buys   buy%   plays   play/buy");
        foreach (var action in actions.OrderByDescending(value => value.AcquireRate).ThenByDescending(value => value.OfferAppearances).ThenBy(value => value.Id, StringComparer.Ordinal))
        {
            Console.WriteLine(
                $"  {action.Id,-26} {action.Tier,4} {action.OfferAppearances,7} {action.Acquires,6} {action.AcquireRate * 100,6:F1} {action.Plays,7} {action.PlaysPerAcquire,10:F2}");
        }
        Console.WriteLine();
    }

    private static void PrintCombines(IReadOnlyList<SimulationCombineSummary> combines)
    {
        if (combines.Count == 0) return;
        Console.WriteLine("Combines");
        Console.WriteLine("  id                         source                     -> result                     exec");
        foreach (var combine in combines.OrderByDescending(value => value.Executions).ThenBy(value => value.Id, StringComparer.Ordinal))
            Console.WriteLine($"  {combine.Id,-26} {combine.SourceUnitId,-26} -> {combine.ResultUnitId,-26} {combine.Executions,5}");
        Console.WriteLine();
    }

    private static void PrintTiers(IReadOnlyList<SimulationTierSummary> tiers)
    {
        Console.WriteLine("Tier distribution");
        Console.WriteLine("  tier   unit offer   unit buys   action offer   action buys   final units");
        foreach (var tier in tiers)
            Console.WriteLine($"  {tier.Tier,4} {tier.UnitOfferAppearances,12} {tier.UnitAcquires,11} {tier.ActionOfferAppearances,14} {tier.ActionAcquires,13} {tier.FinalBoardUnitCopies,13}");
        Console.WriteLine();
    }

    private static void WriteJson(SimulationReport report, string path)
    {
        EnsureParentDirectory(path);
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json + Environment.NewLine, Encoding.UTF8);
        Console.WriteLine($"JSON: {path}");
    }

    private static void WriteCsv(SimulationReport report, string path)
    {
        EnsureParentDirectory(path);
        var lines = new List<string>
        {
            "match,seed,player,leader,personality,strategy,placement,rounds,finalTier,finalHealth,preparationCommands,acquires,releases,deploys,actionsPlayed,combines,refreshes,upgrades,powersUsed,unitChoicesResolved,actionChoicesResolved,freezes,unfreezes,ends"
        };

        foreach (var match in report.MatchResults)
        {
            foreach (var player in match.Players)
            {
                var counts = player.CommandCounts;
                lines.Add(string.Join(',',
                    player.MatchIndex.ToString(CultureInfo.InvariantCulture),
                    player.Seed.ToString(CultureInfo.InvariantCulture),
                    player.PlayerId.ToString(CultureInfo.InvariantCulture),
                    Csv(player.LeaderId),
                    Csv(player.Personality),
                    Csv(player.Strategy),
                    player.Placement.ToString(CultureInfo.InvariantCulture),
                    player.Rounds.ToString(CultureInfo.InvariantCulture),
                    player.FinalTier.ToString(CultureInfo.InvariantCulture),
                    player.FinalHealth.ToString(CultureInfo.InvariantCulture),
                    player.PreparationCommands.ToString(CultureInfo.InvariantCulture),
                    counts.Acquires.ToString(CultureInfo.InvariantCulture),
                    counts.Releases.ToString(CultureInfo.InvariantCulture),
                    counts.Deploys.ToString(CultureInfo.InvariantCulture),
                    counts.ActionsPlayed.ToString(CultureInfo.InvariantCulture),
                    counts.Combines.ToString(CultureInfo.InvariantCulture),
                    counts.Refreshes.ToString(CultureInfo.InvariantCulture),
                    counts.Upgrades.ToString(CultureInfo.InvariantCulture),
                    counts.PowersUsed.ToString(CultureInfo.InvariantCulture),
                    counts.UnitChoicesResolved.ToString(CultureInfo.InvariantCulture),
                    counts.ActionChoicesResolved.ToString(CultureInfo.InvariantCulture),
                    counts.Freezes.ToString(CultureInfo.InvariantCulture),
                    counts.Unfreezes.ToString(CultureInfo.InvariantCulture),
                    counts.Ends.ToString(CultureInfo.InvariantCulture)));
            }
        }

        File.WriteAllLines(path, lines, Encoding.UTF8);
        Console.WriteLine($"CSV: {path}");
        WriteEntityCsvFiles(report.EntityTelemetry, path);
    }

    private static void WriteEntityCsvFiles(SimulationEntityReport telemetry, string playerCsvPath)
    {
        var unitsPath = SidecarCsvPath(playerCsvPath, "units");
        var actionsPath = SidecarCsvPath(playerCsvPath, "actions");
        var combinesPath = SidecarCsvPath(playerCsvPath, "combines");
        var tiersPath = SidecarCsvPath(playerCsvPath, "tiers");

        File.WriteAllLines(unitsPath,
            new[] { "id,name,tier,offerAppearances,acquires,acquireRate,releases,deploys,finalBoardPlayers,finalBoardCopies,winnerFinalBoards,finalBoardWinRate,averagePlacementWhenOnFinalBoard" }
                .Concat(telemetry.Units.Select(unit => string.Join(',',
                    Csv(unit.Id), Csv(unit.Name), unit.Tier,
                    unit.OfferAppearances, unit.Acquires, F(unit.AcquireRate), unit.Releases, unit.Deploys,
                    unit.FinalBoardPlayers, unit.FinalBoardCopies, unit.WinnerFinalBoards, F(unit.FinalBoardWinRate),
                    unit.AveragePlacementWhenOnFinalBoard is double placement ? F(placement) : string.Empty))),
            Encoding.UTF8);

        File.WriteAllLines(actionsPath,
            new[] { "id,name,tier,offerAppearances,acquires,acquireRate,plays,playsPerAcquire" }
                .Concat(telemetry.Actions.Select(action => string.Join(',',
                    Csv(action.Id), Csv(action.Name), action.Tier,
                    action.OfferAppearances, action.Acquires, F(action.AcquireRate), action.Plays, F(action.PlaysPerAcquire)))),
            Encoding.UTF8);

        File.WriteAllLines(combinesPath,
            new[] { "id,name,sourceUnitId,requiredCopies,resultUnitId,executions" }
                .Concat(telemetry.Combines.Select(combine => string.Join(',',
                    Csv(combine.Id), Csv(combine.Name), Csv(combine.SourceUnitId), combine.RequiredCopies, Csv(combine.ResultUnitId), combine.Executions))),
            Encoding.UTF8);

        File.WriteAllLines(tiersPath,
            new[] { "tier,unitOfferAppearances,unitAcquires,actionOfferAppearances,actionAcquires,finalBoardUnitCopies" }
                .Concat(telemetry.Tiers.Select(tier => string.Join(',',
                    tier.Tier, tier.UnitOfferAppearances, tier.UnitAcquires, tier.ActionOfferAppearances, tier.ActionAcquires, tier.FinalBoardUnitCopies))),
            Encoding.UTF8);

        Console.WriteLine($"Unit CSV: {unitsPath}");
        Console.WriteLine($"Action CSV: {actionsPath}");
        Console.WriteLine($"Combine CSV: {combinesPath}");
        Console.WriteLine($"Tier CSV: {tiersPath}");
    }

    private static string SidecarCsvPath(string path, string suffix)
    {
        var fullPath = Path.GetFullPath(path);
        var extension = Path.GetExtension(fullPath);
        var basePath = extension.Length == 0 ? fullPath : fullPath[..^extension.Length];
        return $"{basePath}.{suffix}.csv";
    }

    private static string F(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static void EnsureParentDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Battlegrounds headless AI simulator");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project tools/Battlegrounds.Simulator -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --mod <path>          Mod directory. Default: mods/example");
        Console.WriteLine("  --matches <n>         Number of matches. Default: 100");
        Console.WriteLine("  --players <n>         Players per match. Default: smallest supported even count");
        Console.WriteLine("  --seed <n>            Base seed; each match uses seed+n. Default: 12345");
        Console.WriteLine("  --max-commands <n>    AI safety limit per Preparation. Default: 128");
        Console.WriteLine("  --max-rounds <n>      Match safety limit. Default: 200");
        Console.WriteLine("  --json <path>         Write full report as JSON");
        Console.WriteLine("  --csv <path>          Write player CSV plus .units/.actions/.combines/.tiers CSV sidecars");
        Console.WriteLine("  -h, --help            Show this help");
    }

    private sealed class CliOptions
    {
        public string ModPath { get; set; } = "mods/example";
        public int Matches { get; set; } = 100;
        public int? Players { get; set; }
        public int Seed { get; set; } = 12345;
        public int MaximumCommandsPerPreparation { get; set; } = 128;
        public int MaximumRounds { get; set; } = 200;
        public string? JsonPath { get; set; }
        public string? CsvPath { get; set; }
        public bool ShowHelp { get; set; }
    }
}
