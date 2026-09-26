using System.Text.Json;
using System.Text.Json.Serialization;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Domain.Taxonomy;
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
        if (!report.IsValid) throw new ModValidationException(report);

        var manifest = ReadRequired<ModManifest>(Path.Combine(modDirectory, "mod.json"));
        var matchRulesData = ReadRequired<MatchRulesData>(Path.Combine(modDirectory, "rules", "match.json"));
        var preparationRulesData = ReadRequired<PreparationRulesData>(Path.Combine(modDirectory, "rules", "preparation.json"));
        var combatRulesData = ReadRequired<CombatRulesData>(Path.Combine(modDirectory, "rules", "combat.json"));
        var behaviorData = ReadRequired<BehaviorData[]>(Path.Combine(modDirectory, "content", "behaviors.json"));
        var typeData = ReadRequired<NamedIdData[]>(Path.Combine(modDirectory, "content", "types.json"));
        var tagData = ReadRequired<NamedIdData[]>(Path.Combine(modDirectory, "content", "tags.json"));
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
        var unitTypeCatalog = new UnitTypeCatalog(typeData.Select(data =>
            new UnitTypeDefinition(new UnitTypeId(data.Id), data.Name)));
        var tagCatalog = new TagCatalog(tagData.Select(data =>
            new TagDefinition(new TagId(data.Id), data.Name)));

        var definitions = unitData.Select(unit => new UnitDefinition(
            new UnitId(unit.Id),
            unit.Name,
            unit.Tier,
            unit.Attack,
            unit.Health,
            (unit.Behaviors ?? []).Select(id => behaviorCatalog.GetRequired(new BehaviorId(id))),
            (unit.Types ?? []).Select(id => unitTypeCatalog.GetRequired(new UnitTypeId(id))),
            (unit.Tags ?? []).Select(id => tagCatalog.GetRequired(new TagId(id))),
            (unit.Triggers ?? []).Select(BuildTrigger)))
            .ToArray();

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
            unitTypeCatalog,
            tagCatalog,
            catalog,
            poolEntries);
    }

    private static TriggerDefinition BuildTrigger(TriggerData data) =>
        new(new NativeTriggerKey(data.Event), data.Effects.Select(BuildEffect));

    private static EffectDefinition BuildEffect(EffectData data) =>
        data.Kind switch
        {
            "modifyStats" => new ModifyStatsEffectDefinition(
                BuildTarget(data.Target),
                data.Attack ?? 0,
                data.Health ?? 0),
            "dealDamage" => new DealDamageEffectDefinition(
                BuildTarget(data.Target),
                data.Amount ?? throw new InvalidDataException("Validated dealDamage effect is missing amount.")),
            "summonUnit" => new SummonUnitEffectDefinition(
                new UnitId(data.UnitId ?? throw new InvalidDataException("Validated summonUnit effect is missing unitId.")),
                data.Count ?? 1),
            "addBehavior" => new AddBehaviorEffectDefinition(
                BuildTarget(data.Target),
                new BehaviorId(data.BehaviorId ?? throw new InvalidDataException("Validated addBehavior effect is missing behaviorId."))),
            "removeBehavior" => new RemoveBehaviorEffectDefinition(
                BuildTarget(data.Target),
                new BehaviorId(data.BehaviorId ?? throw new InvalidDataException("Validated removeBehavior effect is missing behaviorId."))),
            "addResource" => new AddResourceEffectDefinition(
                data.Amount ?? throw new InvalidDataException("Validated addResource effect is missing amount.")),
            _ => throw new InvalidDataException($"Validated effect kind '{data.Kind}' is unsupported."),
        };

    private static EffectTargetSelector BuildTarget(TargetData? data)
    {
        if (data is null) throw new InvalidDataException("Validated targeted effect is missing target.");
        return new EffectTargetSelector(
            data.Scope,
            string.IsNullOrWhiteSpace(data.TypeId) ? null : new UnitTypeId(data.TypeId),
            string.IsNullOrWhiteSpace(data.TagId) ? null : new TagId(data.TagId));
    }

    private static T ReadRequired<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidDataException($"Validated mod file '{path}' contained no data.");
    }

    private sealed record ModManifest(int SchemaVersion, string Id, string Name, Dictionary<string, string> Terminology);
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
    private sealed record NamedIdData(string Id, string Name);
    private sealed record UnitData(
        string Id,
        string Name,
        int Tier,
        int Attack,
        int Health,
        string[]? Behaviors,
        string[]? Types,
        string[]? Tags,
        TriggerData[]? Triggers);
    private sealed record TriggerData(string Event, EffectData[] Effects);
    private sealed record EffectData(
        string Kind,
        TargetData? Target,
        int? Attack,
        int? Health,
        int? Amount,
        string? UnitId,
        int? Count,
        string? BehaviorId);
    private sealed record TargetData(EffectTargetScope Scope, string? TypeId, string? TagId);
    private sealed record UnitPoolData(string UnitId, int Copies);
}
