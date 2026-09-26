using System.Text.Json;
using System.Text.Json.Serialization;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
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
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private readonly ModValidator _validator;

    public ModLoader(ModValidator? validator = null)
    {
        _validator = validator ?? new ModValidator();
    }

    public ModValidationReport Validate(string modDirectory) => _validator.Validate(modDirectory);

    public ModPackage Load(string modDirectory)
    {
        var report = _validator.Validate(modDirectory);
        if (!report.IsValid)
        {
            throw new ModValidationException(report);
        }

        var manifest = ReadRequired<ModManifest>(Path.Combine(modDirectory, "mod.json"));
        var matchRulesData = ReadRequired<MatchRulesData>(Path.Combine(modDirectory, "rules", "match.json"));
        var preparationRulesData = ReadRequired<PreparationRulesData>(Path.Combine(modDirectory, "rules", "preparation.json"));
        var combatRulesData = ReadRequired<CombatRulesData>(Path.Combine(modDirectory, "rules", "combat.json"));
        var behaviorData = ReadRequired<BehaviorData[]>(Path.Combine(modDirectory, "content", "behaviors.json"));
        var unitData = ReadRequired<UnitData[]>(Path.Combine(modDirectory, "content", "units.json"));
        var poolData = ReadRequired<UnitPoolData[]>(Path.Combine(modDirectory, "content", "pool.json"));

        var matchRules = new MatchRules(matchRulesData.MinimumPlayers, matchRulesData.MaximumPlayers);
        var preparationRules = new PreparationRules(
            preparationRulesData.StartingResource,
            preparationRulesData.ResourcePerRound,
            preparationRulesData.MaximumResource,
            preparationRulesData.AcquireCost,
            preparationRulesData.ReleaseValue,
            preparationRulesData.RefreshCost,
            preparationRulesData.FieldCapacity,
            preparationRulesData.ReserveCapacity,
            preparationRulesData.MaximumTier,
            preparationRulesData.OfferSizesByTier,
            preparationRulesData.InitialUpgradeCostsByTier);
        var combatRules = new CombatRules(combatRulesData.StartingSidePolicy);

        var behaviorCatalog = new BehaviorCatalog(behaviorData.Select(data =>
            new BehaviorDefinition(new BehaviorId(data.Id), data.Name, new NativeBehaviorKey(data.Handler))));

        var definitions = unitData.Select(unit =>
        {
            var attachedBehaviors = (unit.Behaviors ?? [])
                .Select(id => behaviorCatalog.GetRequired(new BehaviorId(id)))
                .ToArray();

            return new UnitDefinition(
                new UnitId(unit.Id),
                unit.Name,
                unit.Tier,
                unit.Attack,
                unit.Health,
                attachedBehaviors);
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
            preparationRules,
            combatRules,
            behaviorCatalog,
            catalog,
            poolEntries);
    }

    private static T ReadRequired<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidDataException($"Validated mod file '{path}' contained no data.");
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

    private sealed record CombatRulesData(StartingSidePolicy StartingSidePolicy);

    private sealed record BehaviorData(string Id, string Name, string Handler);

    private sealed record UnitData(
        string Id,
        string Name,
        int Tier,
        int Attack,
        int Health,
        string[]? Behaviors);

    private sealed record UnitPoolData(string UnitId, int Copies);
}
