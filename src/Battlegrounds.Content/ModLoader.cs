using System.Text.Json;
using System.Text.Json.Serialization;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Content;

public sealed class ModLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public ModPackage Load(string modDirectory)
    {
        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            throw new ArgumentException("Mod directory cannot be empty.", nameof(modDirectory));
        }

        if (!Directory.Exists(modDirectory))
        {
            throw new DirectoryNotFoundException($"Mod directory '{modDirectory}' does not exist.");
        }

        var manifest = ReadRequired<ModManifest>(Path.Combine(modDirectory, "mod.json"));
        var matchRulesData = ReadRequired<MatchRulesData>(
            Path.Combine(modDirectory, "rules", "match.json"));
        var rulesData = ReadRequired<PreparationRulesData>(
            Path.Combine(modDirectory, "rules", "preparation.json"));
        var unitData = ReadRequired<UnitData[]>(
            Path.Combine(modDirectory, "content", "units.json"));
        var poolData = ReadRequired<UnitPoolData[]>(
            Path.Combine(modDirectory, "content", "pool.json"));

        ValidateManifest(manifest);

        var matchRules = new MatchRules(matchRulesData.MinimumPlayers, matchRulesData.MaximumPlayers);
        var rules = new PreparationRules(
            rulesData.StartingResource,
            rulesData.ResourcePerRound,
            rulesData.MaximumResource,
            rulesData.AcquireCost,
            rulesData.ReleaseValue,
            rulesData.RefreshCost,
            rulesData.FieldCapacity,
            rulesData.ReserveCapacity,
            rulesData.MaximumTier,
            rulesData.OfferSizesByTier,
            rulesData.InitialUpgradeCostsByTier);

        if (unitData.Length == 0)
        {
            throw new InvalidDataException("A mod must define at least one unit.");
        }

        var definitions = unitData.Select(unit =>
        {
            var definition = new UnitDefinition(
                new UnitId(unit.Id),
                unit.Name,
                unit.Tier,
                unit.Attack,
                unit.Health);

            if (definition.Tier > rules.MaximumTier)
            {
                throw new InvalidDataException(
                    $"Unit '{definition.Id}' uses tier {definition.Tier}, above maximum tier {rules.MaximumTier}.");
            }

            return definition;
        }).ToArray();

        var catalog = new UnitCatalog(definitions);
        var poolEntries = poolData
            .Select(entry => new UnitPoolEntry(new UnitId(entry.UnitId), entry.Copies))
            .ToArray();

        _ = new UnitPool(catalog, poolEntries);

        return new ModPackage(
            manifest.Id,
            manifest.Name,
            manifest.Terminology,
            matchRules,
            rules,
            catalog,
            poolEntries);
    }

    private static T ReadRequired<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required mod file '{path}' was not found.", path);
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new InvalidDataException($"Mod file '{path}' contained no data.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Mod file '{path}' contains invalid JSON.", exception);
        }
    }

    private static void ValidateManifest(ModManifest manifest)
    {
        if (manifest.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Unsupported mod schema version {manifest.SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            throw new InvalidDataException("Mod id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            throw new InvalidDataException("Mod name cannot be empty.");
        }

        if (manifest.Terminology is null || manifest.Terminology.Count == 0 ||
            manifest.Terminology.Any(pair =>
                string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
        {
            throw new InvalidDataException("Mod terminology must contain non-empty keys and values.");
        }
    }

    private sealed record ModManifest(
        int SchemaVersion,
        string Id,
        string Name,
        Dictionary<string, string> Terminology);

    private sealed record MatchRulesData(int MinimumPlayers, int MaximumPlayers);

    private sealed record PreparationRulesData(
        int StartingResource,
        int ResourcePerRound,
        int MaximumResource,
        int AcquireCost,
        int ReleaseValue,
        int RefreshCost,
        int FieldCapacity,
        int ReserveCapacity,
        int MaximumTier,
        int[] OfferSizesByTier,
        int[] InitialUpgradeCostsByTier);

    private sealed record UnitData(
        string Id,
        string Name,
        int Tier,
        int Attack,
        int Health);

    private sealed record UnitPoolData(string UnitId, int Copies);
}
