using System.Text.Json;
using System.Text.Json.Serialization;
using Battlegrounds.Core.Domain.Actions;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Combat;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Powers;
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
    public ModLoader(ModValidator? validator = null) => _validator = validator ?? new ModValidator();
    public ModValidationReport Validate(string modDirectory) => _validator.Validate(modDirectory);

    public ModPackage Load(string modDirectory)
    {
        var report = Validate(modDirectory);
        if (!report.IsValid) throw new ModValidationException(report);

        var manifest = ReadRequired<ModManifest>(Path.Combine(modDirectory, "mod.json"));
        var presentation = ModPresentationLoader.Load(modDirectory, manifest.Terminology);
        var setupRulesData = ReadRequired<SetupRulesData>(Path.Combine(modDirectory, "rules", "setup.json"));
        var matchRulesData = ReadRequired<MatchRulesData>(Path.Combine(modDirectory, "rules", "match.json"));
        var preparationRulesData = ReadRequired<PreparationRulesData>(Path.Combine(modDirectory, "rules", "preparation.json"));
        var combatRulesData = ReadRequired<CombatRulesData>(Path.Combine(modDirectory, "rules", "combat.json"));
        var behaviorData = ReadDirectory<BehaviorData>(Path.Combine(modDirectory, "content", "behaviors"));
        var powerData = ReadDirectory<PowerData>(Path.Combine(modDirectory, "content", "powers"));
        var actionData = ReadDirectory<ActionData>(Path.Combine(modDirectory, "content", "actions"));
        var leaderData = ReadDirectory<LeaderData>(Path.Combine(modDirectory, "content", "leaders"));
        var typeData = ReadDirectory<NamedIdData>(Path.Combine(modDirectory, "content", "types"));
        var tagData = ReadDirectory<NamedIdData>(Path.Combine(modDirectory, "content", "tags"));
        var unitData = ReadDirectory<UnitData>(Path.Combine(modDirectory, "content", "units"));
        var combineData = ReadDirectory<UnitCombineData>(Path.Combine(modDirectory, "content", "combines"));
        var poolData = ReadRequired<UnitPoolData[]>(Path.Combine(modDirectory, "content", "pool.json"));

        var leaderSelectionRules = new LeaderSelectionRules(setupRulesData.LeaderOfferSize, setupRulesData.LeaderOfferPolicy);
        var matchRules = new MatchRules(matchRulesData.MinimumPlayers, matchRulesData.MaximumPlayers, matchRulesData.StartingHealth);
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
            preparationRulesData.InitialUpgradeCostsByTier,
            preparationRulesData.ActionOfferSizesByTier);
        var combatRules = new CombatRules(combatRulesData.StartingSidePolicy, combatRulesData.PostCombatDamagePolicy);

        var behaviorCatalog = new BehaviorCatalog(behaviorData.Select(data =>
            new BehaviorDefinition(new BehaviorId(data.Id), data.Name, new NativeBehaviorKey(data.Handler))));
        var powerCatalog = new PowerCatalog(powerData.Select(data =>
            new PowerDefinition(
                new PowerId(data.Id),
                data.Name,
                data.Activation is null ? null : new PowerActivationDefinition(data.Activation.Cost, data.Activation.MaxUsesPerTurn, data.Activation.MaxUsesPerMatch),
                data.Triggers.Select(BuildTrigger))));
        var actionCatalog = new ActionCatalog(actionData.Select(data =>
            new ActionDefinition(new ActionId(data.Id), data.Name, data.Tier, data.Cost, data.Effects.Select(BuildEffect))));
        var leaderCatalog = new LeaderCatalog(leaderData.Select(data =>
            new LeaderDefinition(new LeaderId(data.Id), data.Name, data.HealthModifier, data.Armor, new PowerId(data.InitialPowerId))));
        var unitTypeCatalog = new UnitTypeCatalog(typeData.Select(data => new UnitTypeDefinition(new UnitTypeId(data.Id), data.Name)));
        var tagCatalog = new TagCatalog(tagData.Select(data => new TagDefinition(new TagId(data.Id), data.Name)));

        var definitions = unitData.Select(unit => new UnitDefinition(
            new UnitId(unit.Id),
            unit.Name,
            unit.Tier,
            unit.Attack,
            unit.Health,
            (unit.Behaviors ?? []).Select(id => behaviorCatalog.GetRequired(new BehaviorId(id))),
            (unit.Types ?? []).Select(id => unitTypeCatalog.GetRequired(new UnitTypeId(id))),
            (unit.Tags ?? []).Select(id => tagCatalog.GetRequired(new TagId(id))),
            (unit.Triggers ?? []).Select(BuildTrigger))).ToArray();

        var combineCatalog = new UnitCombineCatalog(combineData.Select(data =>
            new UnitCombineDefinition(
                new UnitCombineId(data.Id),
                data.Name,
                new UnitId(data.SourceUnitId),
                data.RequiredCopies,
                new UnitId(data.ResultUnitId))));
        var catalog = new UnitCatalog(definitions, combineCatalog);
        var poolEntries = poolData.Select(entry => new UnitPoolEntry(new UnitId(entry.UnitId), entry.Copies)).ToArray();
        _ = new UnitPool(catalog, poolEntries);

        return new ModPackage(
            manifest.Id,
            manifest.Name,
            manifest.Terminology,
            presentation,
            leaderSelectionRules,
            matchRules,
            preparationRules,
            combatRules,
            behaviorCatalog,
            leaderCatalog,
            powerCatalog,
            actionCatalog,
            unitTypeCatalog,
            tagCatalog,
            catalog,
            poolEntries);
    }

    private static TriggerDefinition BuildTrigger(TriggerData data) =>
        new(
            new NativeTriggerKey(data.Event),
            data.Effects.Select(BuildEffect),
            data.Count,
            (data.Conditions ?? []).Select(BuildCondition),
            data.ActivationLimit is null ? null : new TriggerActivationLimit(data.ActivationLimit.Scope, data.ActivationLimit.Count),
            data.Counter is null ? null : BuildHistoryQuery(data.Counter));

    private static EffectConditionDefinition BuildCondition(ConditionData data) =>
        data.Kind switch
        {
            "unitCount" => new UnitCountConditionDefinition(BuildQuery(data.Query), data.Comparison ?? throw new InvalidDataException("Validated unitCount condition is missing comparison."), data.Value ?? throw new InvalidDataException("Validated unitCount condition is missing value.")),
            "sourceStat" => new SourceStatConditionDefinition(data.Stat ?? throw new InvalidDataException("Validated sourceStat condition is missing stat."), data.Comparison ?? throw new InvalidDataException("Validated sourceStat condition is missing comparison."), data.Value ?? throw new InvalidDataException("Validated sourceStat condition is missing value.")),
            "value" => new ValueConditionDefinition(BuildRequiredValue(data.Left, "condition.left"), data.Comparison ?? throw new InvalidDataException("Validated value condition is missing comparison."), BuildRequiredValue(data.Right, "condition.right")),
            _ => throw new InvalidDataException($"Validated condition kind '{data.Kind}' is unsupported."),
        };

    private static EffectDefinition BuildEffect(EffectData data) =>
        data.Kind switch
        {
            "modifyStats" => new ModifyStatsEffectDefinition(BuildTarget(data.Target), BuildOptionalValue(data.Attack), BuildOptionalValue(data.Health)),
            "dealDamage" => new DealDamageEffectDefinition(BuildTarget(data.Target), BuildRequiredValue(data.Amount, "dealDamage.amount")),
            "destroyUnit" => new DestroyUnitEffectDefinition(BuildTarget(data.Target)),
            "triggerEvent" => new TriggerEventEffectDefinition(BuildTarget(data.Target), new NativeTriggerKey(data.Event ?? throw new InvalidDataException("Validated triggerEvent effect is missing event."))),
            "summonUnit" => new SummonUnitEffectDefinition(new UnitId(data.UnitId ?? throw new InvalidDataException("Validated summonUnit effect is missing unitId.")), data.Count is null ? new ConstantEffectValueExpression(1) : BuildRequiredValue(data.Count, "summonUnit.count")),
            "generateUnitToReserve" => new GenerateUnitToReserveEffectDefinition(new UnitId(data.UnitId ?? throw new InvalidDataException("Validated generateUnitToReserve effect is missing unitId.")), data.Count is null ? new ConstantEffectValueExpression(1) : BuildRequiredValue(data.Count, "generateUnitToReserve.count")),
            "generateUnitChoice" => new GenerateUnitChoiceEffectDefinition(BuildGenerationQuery(data.GenerationQuery), data.OptionCount ?? 3),
            "generateActionToReserve" => new GenerateActionToReserveEffectDefinition(new ActionId(data.ActionId ?? throw new InvalidDataException("Validated generateActionToReserve effect is missing actionId.")), data.Count is null ? new ConstantEffectValueExpression(1) : BuildRequiredValue(data.Count, "generateActionToReserve.count")),
            "generateActionChoice" => new GenerateActionChoiceEffectDefinition(BuildActionGenerationQuery(data.ActionQuery), data.OptionCount ?? 3),
            "transformUnit" => new TransformUnitEffectDefinition(
                BuildTarget(data.Target),
                new UnitId(data.UnitId ?? throw new InvalidDataException("Validated transformUnit effect is missing unitId."))),
            "copyUnitToReserve" => new CopyUnitToReserveEffectDefinition(BuildTarget(data.Target)),
            "applyUnitModifier" => new ApplyUnitModifierEffectDefinition(
                BuildTarget(data.Target),
                data.ModifierKey ?? throw new InvalidDataException("Validated applyUnitModifier effect is missing modifierKey."),
                BuildOptionalValue(data.Attack),
                BuildOptionalValue(data.Health)),
            "removeUnitModifier" => new RemoveUnitModifierEffectDefinition(
                BuildTarget(data.Target),
                data.ModifierKey ?? throw new InvalidDataException("Validated removeUnitModifier effect is missing modifierKey.")),
            "addBehavior" => new AddBehaviorEffectDefinition(BuildTarget(data.Target), new BehaviorId(data.BehaviorId ?? throw new InvalidDataException("Validated addBehavior effect is missing behaviorId."))),
            "removeBehavior" => new RemoveBehaviorEffectDefinition(BuildTarget(data.Target), new BehaviorId(data.BehaviorId ?? throw new InvalidDataException("Validated removeBehavior effect is missing behaviorId."))),
            "addResource" => new AddResourceEffectDefinition(BuildRequiredValue(data.Amount, "addResource.amount")),
            "setPower" => new SetPowerEffectDefinition(new PowerId(data.PowerId ?? throw new InvalidDataException("Validated setPower effect is missing powerId."))),
            _ => throw new InvalidDataException($"Validated effect kind '{data.Kind}' is unsupported."),
        };

    private static UnitDefinitionQuery BuildGenerationQuery(GenerationQueryData? data)
    {
        if (data is null) throw new InvalidDataException("Validated generateUnitChoice effect is missing query.");
        return new UnitDefinitionQuery(
            data.MinimumTier,
            data.MaximumTier,
            string.IsNullOrWhiteSpace(data.TypeId) ? null : new UnitTypeId(data.TypeId),
            string.IsNullOrWhiteSpace(data.TagId) ? null : new TagId(data.TagId),
            data.ExcludeSource ?? false);
    }

    private static ActionDefinitionQuery BuildActionGenerationQuery(ActionQueryData? data)
    {
        if (data is null) throw new InvalidDataException("Validated generateActionChoice effect is missing query.");
        return new ActionDefinitionQuery(
            data.MinimumTier,
            data.MaximumTier,
            string.IsNullOrWhiteSpace(data.ExcludeActionId) ? null : new ActionId(data.ExcludeActionId));
    }

    private static EffectValueExpression? BuildOptionalValue(JsonElement? data) => data is null ? null : BuildValue(data.Value);
    private static EffectValueExpression BuildRequiredValue(JsonElement? data, string label) =>
        data is null ? throw new InvalidDataException($"Validated effect value '{label}' is missing.") : BuildValue(data.Value);

    private static EffectValueExpression BuildValue(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Number && data.TryGetInt32(out var literal)) return new ConstantEffectValueExpression(literal);
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("kind", out var kindElement) || kindElement.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Validated effect value expression has an invalid shape.");
        return kindElement.GetString() switch
        {
            "sourceStat" => new SourceStatEffectValueExpression(ReadEnum<EffectStat>(data, "stat")),
            "targetStat" => new TargetStatEffectValueExpression(ReadEnum<EffectStat>(data, "stat")),
            "unitCount" => new UnitCountEffectValueExpression(BuildQuery(ReadObject(data, "query"))),
            "eventCount" => new EventCountEffectValueExpression(BuildHistoryQuery(data)),
            "add" => BuildComposite(EffectValueOperation.Add, data),
            "multiply" => BuildComposite(EffectValueOperation.Multiply, data),
            "min" => BuildComposite(EffectValueOperation.Min, data),
            "max" => BuildComposite(EffectValueOperation.Max, data),
            var kind => throw new InvalidDataException($"Validated effect value kind '{kind}' is unsupported."),
        };
    }

    private static EffectHistoryQuery BuildHistoryQuery(HistoryQueryData data) =>
        new(new NativeGameEventKey(data.Event), data.Scope,
            string.IsNullOrWhiteSpace(data.TypeId) ? null : new UnitTypeId(data.TypeId),
            string.IsNullOrWhiteSpace(data.TagId) ? null : new TagId(data.TagId));

    private static EffectHistoryQuery BuildHistoryQuery(JsonElement data)
    {
        var eventName = data.GetProperty("event").GetString() ?? throw new InvalidDataException("Validated eventCount expression is missing event.");
        var typeId = data.TryGetProperty("typeId", out var typeElement) ? typeElement.GetString() : null;
        var tagId = data.TryGetProperty("tagId", out var tagElement) ? tagElement.GetString() : null;
        return new EffectHistoryQuery(new NativeGameEventKey(eventName), ReadEnum<EffectHistoryScope>(data, "scope"),
            string.IsNullOrWhiteSpace(typeId) ? null : new UnitTypeId(typeId),
            string.IsNullOrWhiteSpace(tagId) ? null : new TagId(tagId));
    }

    private static CompositeEffectValueExpression BuildComposite(EffectValueOperation operation, JsonElement data) =>
        new(operation, ReadArray(data, "values").EnumerateArray().Select(BuildValue));

    private static TEnum ReadEnum<TEnum>(JsonElement data, string property) where TEnum : struct, Enum =>
        JsonSerializer.Deserialize<TEnum>(data.GetProperty(property).GetRawText(), JsonOptions);

    private static JsonElement ReadObject(JsonElement data, string property)
    {
        var element = data.GetProperty(property);
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"Validated property '{property}' must be an object.");
        return element;
    }

    private static JsonElement ReadArray(JsonElement data, string property)
    {
        var element = data.GetProperty(property);
        if (element.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"Validated property '{property}' must be an array.");
        return element;
    }

    private static EffectTargetSelector BuildTarget(TargetData? data)
    {
        if (data is null) throw new InvalidDataException("Validated targeted effect is missing target.");
        return new EffectTargetSelector(BuildQuery(data), data.Selection ?? EffectTargetSelection.All, data.Limit);
    }

    private static EffectUnitQuery BuildQuery(QueryData? data)
    {
        if (data is null) throw new InvalidDataException("Validated unit query is missing.");
        return new EffectUnitQuery(data.Scope, data.ExcludeSource ?? false,
            string.IsNullOrWhiteSpace(data.TypeId) ? null : new UnitTypeId(data.TypeId),
            string.IsNullOrWhiteSpace(data.TagId) ? null : new TagId(data.TagId));
    }

    private static EffectUnitQuery BuildQuery(JsonElement data)
    {
        var query = JsonSerializer.Deserialize<QueryData>(data.GetRawText(), JsonOptions)
            ?? throw new InvalidDataException("Validated unit query contained no data.");
        return BuildQuery(query);
    }

    private static T[] ReadDirectory<T>(string directory) =>
        Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Select(ReadRequired<T>)
            .ToArray();

    private static T ReadRequired<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidDataException($"Validated mod file '{path}' contained no data.");
    }

    private sealed record ModManifest(int SchemaVersion, string Id, string Name, Dictionary<string, string> Terminology);
    private sealed record SetupRulesData(int LeaderOfferSize, LeaderOfferPolicy LeaderOfferPolicy);
    private sealed record MatchRulesData(int MinimumPlayers, int MaximumPlayers, int StartingHealth);
    private sealed record PreparationRulesData(
        int StartingResource, int ResourcePerRound, int MaximumResource, int AcquireCost, int ReleaseValue,
        int RefreshCost, int FieldCapacity, int ReserveCapacity, int MaximumTier,
        int[] OfferSizesByTier, int[] InitialUpgradeCostsByTier, int[]? ActionOfferSizesByTier);
    private sealed record CombatRulesData(StartingSidePolicy StartingSidePolicy, PostCombatDamagePolicy PostCombatDamagePolicy);
    private sealed record BehaviorData(string Id, string Name, string Handler);
    private sealed record PowerActivationData(int Cost, int MaxUsesPerTurn, int? MaxUsesPerMatch);
    private sealed record PowerData(string Id, string Name, PowerActivationData? Activation, TriggerData[] Triggers);
    private sealed record ActionData(string Id, string Name, int Tier, int Cost, EffectData[] Effects);
    private sealed record LeaderData(string Id, string Name, int HealthModifier, int Armor, string InitialPowerId);
    private sealed record NamedIdData(string Id, string Name);
    private sealed record UnitData(string Id, string Name, int Tier, int Attack, int Health, string[]? Behaviors, string[]? Types, string[]? Tags, TriggerData[]? Triggers);
    private sealed record UnitCombineData(string Id, string Name, string SourceUnitId, int RequiredCopies, string ResultUnitId);
    private sealed record TriggerData(string Event, EffectData[] Effects, int? Count, ConditionData[]? Conditions, TriggerActivationLimitData? ActivationLimit, HistoryQueryData? Counter);
    private sealed record TriggerActivationLimitData(EffectHistoryScope Scope, int Count);
    private sealed record HistoryQueryData(string Event, EffectHistoryScope Scope, string? TypeId, string? TagId);
    private sealed record ConditionData(string Kind, QueryData? Query, EffectComparison? Comparison, int? Value, EffectStat? Stat, JsonElement? Left, JsonElement? Right);
    private sealed record EffectData(
        string Kind,
        TargetData? Target,
        JsonElement? Attack,
        JsonElement? Health,
        JsonElement? Amount,
        string? UnitId,
        JsonElement? Count,
        string? BehaviorId,
        string? Event,
        string? PowerId,
        GenerationQueryData? GenerationQuery,
        int? OptionCount,
        string? ActionId,
        ActionQueryData? ActionQuery,
        string? ModifierKey);
    private sealed record GenerationQueryData(int? MinimumTier, int? MaximumTier, string? TypeId, string? TagId, bool? ExcludeSource);
    private sealed record ActionQueryData(int? MinimumTier, int? MaximumTier, string? ExcludeActionId);
    private record QueryData(EffectTargetScope Scope, bool? ExcludeSource, string? TypeId, string? TagId);
    private sealed record TargetData(EffectTargetScope Scope, bool? ExcludeSource, string? TypeId, string? TagId, EffectTargetSelection? Selection, int? Limit)
        : QueryData(Scope, ExcludeSource, TypeId, TagId);
    private sealed record UnitPoolData(string UnitId, int Copies);
}
