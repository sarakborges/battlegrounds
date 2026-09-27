using System.Globalization;
using System.Text;
using System.Text.Json;
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
        for (var value = mod.MatchRules.MinimumPlayers; value <= mod.MatchRules.MaximumPlayers; value++)
        {
            if (value % 2 == 0) return value;
        }

        throw new InvalidOperationException(
            $"Mod '{mod.Id}' has no supported even player count under the current round-one combat contract.");
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
        PrintGroup("Leaders", report.Leaders);
        PrintGroup("Personalities", report.Personalities);
        PrintGroup("Strategies", report.Strategies);
    }

    private static void PrintGroup(string title, IReadOnlyList<SimulationGroupSummary> summaries)
    {
        Console.WriteLine(title);
        Console.WriteLine("  id                         games   wins   win%   avg place   avg cmds");
        foreach (var summary in summaries)
        {
            Console.WriteLine(
                $"  {summary.Id,-26} {summary.Games,5} {summary.Wins,6} {summary.WinRate * 100,6:F1} {summary.AveragePlacement,10:F2} {summary.AveragePreparationCommands,10:F1}");
        }
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
            "match,seed,player,leader,personality,strategy,placement,rounds,finalTier,finalHealth,preparationCommands"
        };

        foreach (var match in report.MatchResults)
        {
            foreach (var player in match.Players)
            {
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
                    player.PreparationCommands.ToString(CultureInfo.InvariantCulture)));
            }
        }

        File.WriteAllLines(path, lines, Encoding.UTF8);
        Console.WriteLine($"CSV: {path}");
    }

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
        Console.WriteLine("  --csv <path>          Write one row per simulated player as CSV");
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
