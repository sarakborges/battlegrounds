using System.Text.Json;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;

namespace Battlegrounds.Content;

public sealed class ModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public ModValidationReport Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            issues.Add(new("MOD_DIRECTORY_INVALID", "", "$", "Mod directory cannot be empty."));
            return new ModValidationReport(issues);
        }
        if (!Directory.Exists(modDirectory))
        {
            issues.Add(new("MOD_DIRECTORY_MISSING", "", "$", $"Mod directory '{modDirectory}' does not exist."));
            return new ModValidationReport(issues);
        }

        using var manifest = ReadRequired(modDirectory, "mod.json", issues);
        using var match = ReadRequired(modDirectory, "rules/match.json", issues);
        using var preparation = ReadRequired(modDirectory, "rules/preparation.json", issues);
        using var combat = ReadRequired(modDirectory, "rules/combat.json", issues);
        using var behaviors = ReadRequired(modDirectory, "content/behaviors.json", issues);
        using var types = ReadRequired(modDirectory, "content/types.json", issues);
        using var tags = ReadRequired(modDirectory, "content/tags.json", issues);
        using var units = ReadRequired(modDirectory, "content/units.json", issues);
        using var pool = ReadRequired(modDirectory, "content/pool.json", issues);

        ValidateManifest(manifest, issues);
        ValidateMatch(match, issues);
        var maximumTier = ValidatePreparation(preparation, issues);
        ValidateCombat(combat, issues);
        var behaviorHandlers = ValidateBehaviors(behaviors, issues);
        var typeIds = ValidateNamedIds(types, "content/types.json", "unit type", issues);
        var tagIds = ValidateNamedIds(tags, "content/tags.json", "tag", issues);
        var unitIds = ValidateUnits(units, maximumTier, behaviorHandlers, typeIds, tagIds, issues);
        ValidatePool(pool, unitIds, issues);

        return new ModValidationReport(issues);
    }

    private static JsonDocument? ReadRequired(string root, string relativePath, List<ModValidationIssue> issues)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            issues.Add(new("MISSING_REQUIRED_FILE", relativePath, "$", $"Required file '{relativePath}' is missing."));
            return null;
        }
        try
        {
            return JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
        }
        catch (JsonException exception)
        {
            issues.Add(new("INVALID_JSON", relativePath, exception.Path ?? "$", "File contains invalid JSON."));
            return null;
        }
    }

    private static void ValidateManifest(JsonDocument? document, List<ModValidationIssue> issues)
    {
        const string file = "mod.json";
        if (!TryObject(document, file, "$", issues, out var root)) return;
        ValidateKeys(root, file, "$", ["schemaVersion", "id", "name", "terminology"], ["schemaVersion", "id", "name", "terminology"], issues);
        if (TryInt(root, "schemaVersion", file, "$.schemaVersion", issues, out var version) && version != 1)
            issues.Add(new("UNSUPPORTED_SCHEMA_VERSION", file, "$.schemaVersion", $"Unsupported schema version {version}."));
        RequireNonEmptyString(root, "id", file, "$.id", issues);
        RequireNonEmptyString(root, "name", file, "$.name", issues);
        if (!root.TryGetProperty("terminology", out var terminology)) return;
        if (terminology.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, "$.terminology", "Expected an object."));
            return;
        }
        if (!terminology.EnumerateObject().Any()) issues.Add(new("INVALID_VALUE", file, "$.terminology", "Terminology cannot be empty."));
        foreach (var pair in terminology.EnumerateObject())
            if (string.IsNullOrWhiteSpace(pair.Name) || pair.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(pair.Value.GetString()))
                issues.Add(new("INVALID_VALUE", file, $"$.terminology.{pair.Name}", "Terminology keys and values must be non-empty strings."));
    }

    private static void ValidateMatch(JsonDocument? document, List<ModValidationIssue> issues)
    {
        const string file = "rules/match.json";
        if (!TryObject(document, file, "$", issues, out var root)) return;
        ValidateKeys(root, file, "$", ["minimumPlayers", "maximumPlayers"], ["minimumPlayers", "maximumPlayers"], issues);
        var hasMin = TryInt(root, "minimumPlayers", file, "$.minimumPlayers", issues, out var min);
        var hasMax = TryInt(root, "maximumPlayers", file, "$.maximumPlayers", issues, out var max);
        if (hasMin && min <= 0) issues.Add(new("INVALID_VALUE", file, "$.minimumPlayers", "minimumPlayers must be positive."));
        if (hasMin && hasMax && max < min) issues.Add(new("INVALID_VALUE", file, "$.maximumPlayers", "maximumPlayers cannot be lower than minimumPlayers."));
    }

    private static int? ValidatePreparation(JsonDocument? document, List<ModValidationIssue> issues)
    {
        const string file = "rules/preparation.json";
        if (!TryObject(document, file, "$", issues, out var root)) return null;
        string[] keys = ["startingResource", "resourcePerRound", "maximumResource", "acquireCost", "releaseValue", "refreshCost", "fieldCapacity", "reserveCapacity", "maximumTier", "offerSizesByTier", "initialUpgradeCostsByTier"];
        ValidateKeys(root, file, "$", keys, keys, issues);
        var values = new Dictionary<string, int>();
        foreach (var key in keys.Take(9)) if (TryInt(root, key, file, "$." + key, issues, out var value)) values[key] = value;
        foreach (var key in new[] { "startingResource", "resourcePerRound", "acquireCost", "releaseValue", "refreshCost" })
            if (values.TryGetValue(key, out var value) && value < 0) issues.Add(new("INVALID_VALUE", file, "$." + key, $"{key} cannot be negative."));
        if (values.TryGetValue("fieldCapacity", out var field) && field <= 0) issues.Add(new("INVALID_VALUE", file, "$.fieldCapacity", "fieldCapacity must be positive."));
        if (values.TryGetValue("reserveCapacity", out var reserve) && reserve <= 0) issues.Add(new("INVALID_VALUE", file, "$.reserveCapacity", "reserveCapacity must be positive."));
        if (values.TryGetValue("maximumTier", out var maxTier) && maxTier <= 1) issues.Add(new("INVALID_VALUE", file, "$.maximumTier", "maximumTier must be greater than 1."));
        if (values.TryGetValue("startingResource", out var start) && values.TryGetValue("maximumResource", out var maxResource) && maxResource < start)
            issues.Add(new("INVALID_VALUE", file, "$.maximumResource", "maximumResource cannot be lower than startingResource."));
        var offerSizes = ValidateIntArray(root, "offerSizesByTier", file, issues, true);
        var upgradeCosts = ValidateIntArray(root, "initialUpgradeCostsByTier", file, issues, false);
        if (values.TryGetValue("maximumTier", out maxTier))
        {
            if (offerSizes is not null && offerSizes.Count != maxTier) issues.Add(new("INVALID_LENGTH", file, "$.offerSizesByTier", "Offer size must be defined for every tier."));
            if (upgradeCosts is not null && upgradeCosts.Count != maxTier - 1) issues.Add(new("INVALID_LENGTH", file, "$.initialUpgradeCostsByTier", "Upgrade cost must be defined for every non-maximum tier."));
        }
        if (offerSizes is not null)
            for (var i = 1; i < offerSizes.Count; i++)
                if (offerSizes[i] < offerSizes[i - 1]) issues.Add(new("INVALID_VALUE", file, $"$.offerSizesByTier[{i}]", "Offer sizes cannot decrease at higher tiers."));
        return values.TryGetValue("maximumTier", out maxTier) ? maxTier : null;
    }

    private static void ValidateCombat(JsonDocument? document, List<ModValidationIssue> issues)
    {
        const string file = "rules/combat.json";
        if (!TryObject(document, file, "$", issues, out var root)) return;
        ValidateKeys(root, file, "$", ["startingSidePolicy"], ["startingSidePolicy"], issues);
        if (RequireNonEmptyString(root, "startingSidePolicy", file, "$.startingSidePolicy", issues, out var value) &&
            value is not ("random" or "largerFieldThenRandom"))
            issues.Add(new("INVALID_VALUE", file, "$.startingSidePolicy", $"Unknown startingSidePolicy '{value}'."));
    }

    private static Dictionary<string, string> ValidateBehaviors(JsonDocument? document, List<ModValidationIssue> issues)
    {
        const string file = "content/behaviors.json";
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!TryArray(document, file, "$", issues, out var root)) return result;
        var index = 0;
        foreach (var item in root.EnumerateArray())
        {
            var path = $"$[{index}]";
            if (item.ValueKind != JsonValueKind.Object) { issues.Add(new("INVALID_TYPE", file, path, "Expected an object.")); index++; continue; }
            ValidateKeys(item, file, path, ["id", "name", "handler"], ["id", "name", "handler"], issues);
            RequireNonEmptyString(item, "name", file, path + ".name", issues);
            var hasId = RequireNonEmptyString(item, "id", file, path + ".id", issues, out var id);
            var hasHandler = RequireNonEmptyString(item, "handler", file, path + ".handler", issues, out var handler);
            if (hasId && !result.TryAdd(id!, handler ?? string.Empty)) issues.Add(new("DUPLICATE_ID", file, path + ".id", $"Duplicate behavior id '{id}'."));
            if (hasHandler && !NativeBehaviorKeys.IsSupported(new NativeBehaviorKey(handler!))) issues.Add(new("UNSUPPORTED_HANDLER", file, path + ".handler", $"Native behavior handler '{handler}' is not supported."));
            index++;
        }
        return result;
    }

    private static HashSet<string> ValidateNamedIds(JsonDocument? document, string file, string label, List<ModValidationIssue> issues)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!TryArray(document, file, "$", issues, out var root)) return ids;
        var index = 0;
        foreach (var item in root.EnumerateArray())
        {
            var path = $"$[{index}]";
            if (item.ValueKind != JsonValueKind.Object) { issues.Add(new("INVALID_TYPE", file, path, "Expected an object.")); index++; continue; }
            ValidateKeys(item, file, path, ["id", "name"], ["id", "name"], issues);
            if (RequireNonEmptyString(item, "id", file, path + ".id", issues, out var id) && !ids.Add(id!))
                issues.Add(new("DUPLICATE_ID", file, path + ".id", $"Duplicate {label} id '{id}'."));
            RequireNonEmptyString(item, "name", file, path + ".name", issues);
            index++;
        }
        return ids;
    }

    private static HashSet<string> ValidateUnits(
        JsonDocument? document,
        int? maximumTier,
        IReadOnlyDictionary<string, string> behaviorHandlers,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        const string file = "content/units.json";
        var unitIds = new HashSet<string>(StringComparer.Ordinal);
        var triggerSources = new List<(JsonElement Unit, string Path)>();
        if (!TryArray(document, file, "$", issues, out var root)) return unitIds;
        if (!root.EnumerateArray().Any()) issues.Add(new("INVALID_VALUE", file, "$", "A mod must define at least one unit."));
        var index = 0;
        foreach (var item in root.EnumerateArray())
        {
            var path = $"$[{index}]";
            if (item.ValueKind != JsonValueKind.Object) { issues.Add(new("INVALID_TYPE", file, path, "Expected an object.")); index++; continue; }
            ValidateKeys(item, file, path, ["id", "name", "tier", "attack", "health", "behaviors", "types", "tags", "triggers"], ["id", "name", "tier", "attack", "health"], issues);
            if (RequireNonEmptyString(item, "id", file, path + ".id", issues, out var id) && !unitIds.Add(id!)) issues.Add(new("DUPLICATE_ID", file, path + ".id", $"Duplicate unit id '{id}'."));
            RequireNonEmptyString(item, "name", file, path + ".name", issues);
            if (TryInt(item, "tier", file, path + ".tier", issues, out var tier))
            {
                if (tier <= 0) issues.Add(new("INVALID_VALUE", file, path + ".tier", "tier must be positive."));
                if (maximumTier is not null && tier > maximumTier) issues.Add(new("INVALID_VALUE", file, path + ".tier", $"tier {tier} exceeds maximumTier {maximumTier}."));
            }
            if (TryInt(item, "attack", file, path + ".attack", issues, out var attack) && attack < 0) issues.Add(new("INVALID_VALUE", file, path + ".attack", "attack cannot be negative."));
            if (TryInt(item, "health", file, path + ".health", issues, out var health) && health <= 0) issues.Add(new("INVALID_VALUE", file, path + ".health", "health must be positive."));
            ValidateBehaviorReferences(item, path, behaviorHandlers, issues);
            ValidateReferenceArray(item, "types", path, typeIds, "unit type", issues);
            ValidateReferenceArray(item, "tags", path, tagIds, "tag", issues);
            triggerSources.Add((item, path));
            index++;
        }
        foreach (var source in triggerSources)
            ValidateTriggers(source.Unit, source.Path, unitIds, behaviorHandlers.Keys.ToHashSet(StringComparer.Ordinal), typeIds, tagIds, issues);
        return unitIds;
    }

    private static void ValidateBehaviorReferences(JsonElement item, string path, IReadOnlyDictionary<string, string> behaviorHandlers, List<ModValidationIssue> issues)
    {
        const string file = "content/units.json";
        if (!item.TryGetProperty("behaviors", out var behaviors)) return;
        if (behaviors.ValueKind != JsonValueKind.Array) { issues.Add(new("INVALID_TYPE", file, path + ".behaviors", "Expected an array.")); return; }
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenHandlers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var behavior in behaviors.EnumerateArray())
        {
            var behaviorPath = $"{path}.behaviors[{index}]";
            if (behavior.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(behavior.GetString())) issues.Add(new("INVALID_TYPE", file, behaviorPath, "Behavior reference must be a non-empty string."));
            else
            {
                var id = behavior.GetString()!;
                if (!seenIds.Add(id)) issues.Add(new("DUPLICATE_REFERENCE", file, behaviorPath, $"Behavior '{id}' is referenced more than once."));
                if (!behaviorHandlers.TryGetValue(id, out var handler)) issues.Add(new("UNKNOWN_REFERENCE", file, behaviorPath, $"Unknown behavior '{id}'."));
                else if (!seenHandlers.Add(handler)) issues.Add(new("DUPLICATE_NATIVE_BEHAVIOR", file, behaviorPath, $"Native behavior handler '{handler}' is attached more than once."));
            }
            index++;
        }
    }

    private static void ValidateReferenceArray(JsonElement item, string property, string path, IReadOnlySet<string> known, string label, List<ModValidationIssue> issues)
    {
        const string file = "content/units.json";
        if (!item.TryGetProperty(property, out var values)) return;
        if (values.ValueKind != JsonValueKind.Array) { issues.Add(new("INVALID_TYPE", file, path + "." + property, "Expected an array.")); return; }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            var itemPath = $"{path}.{property}[{index}]";
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) issues.Add(new("INVALID_TYPE", file, itemPath, $"{label} reference must be a non-empty string."));
            else
            {
                var id = value.GetString()!;
                if (!seen.Add(id)) issues.Add(new("DUPLICATE_REFERENCE", file, itemPath, $"{label} '{id}' is referenced more than once."));
                if (!known.Contains(id)) issues.Add(new("UNKNOWN_REFERENCE", file, itemPath, $"Unknown {label} '{id}'."));
            }
            index++;
        }
    }

    private static void ValidateTriggers(
        JsonElement unit,
        string unitPath,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> behaviorIds,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        const string file = "content/units.json";
        if (!unit.TryGetProperty("triggers", out var triggers)) return;
        if (triggers.ValueKind != JsonValueKind.Array) { issues.Add(new("INVALID_TYPE", file, unitPath + ".triggers", "Expected an array.")); return; }
        var triggerIndex = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            var path = $"{unitPath}.triggers[{triggerIndex}]";
            if (trigger.ValueKind != JsonValueKind.Object) { issues.Add(new("INVALID_TYPE", file, path, "Expected an object.")); triggerIndex++; continue; }
            ValidateKeys(trigger, file, path, ["event", "effects"], ["event"], issues);
            if (RequireNonEmptyString(trigger, "event", file, path + ".event", issues, out var eventName) && !NativeTriggerKeys.IsSupported(new NativeTriggerKey(eventName!)))
                issues.Add(new("UNSUPPORTED_TRIGGER", file, path + ".event", $"Trigger event '{eventName}' is not supported."));
            if (!trigger.TryGetProperty("effects", out var effects))
            {
                issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + ".effects", "Trigger requires 'effects'."));
                triggerIndex++;
                continue;
            }
            if (effects.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new("INVALID_TYPE", file, path + ".effects", "Expected an array."));
                triggerIndex++;
                continue;
            }
            if (effects.GetArrayLength() == 0) issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + ".effects", "Trigger requires at least one effect."));
            var effectIndex = 0;
            foreach (var effect in effects.EnumerateArray())
            {
                ValidateEffect(effect, $"{path}.effects[{effectIndex}]", unitIds, behaviorIds, typeIds, tagIds, issues);
                effectIndex++;
            }
            triggerIndex++;
        }
    }

    private static void ValidateEffect(
        JsonElement effect,
        string path,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> behaviorIds,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        const string file = "content/units.json";
        if (effect.ValueKind != JsonValueKind.Object) { issues.Add(new("INVALID_TYPE", file, path, "Expected an object.")); return; }
        if (!RequireNonEmptyString(effect, "kind", file, path + ".kind", issues, out var kind))
        {
            ValidateKeys(effect, file, path, ["kind"], ["kind"], issues);
            return;
        }
        switch (kind)
        {
            case "modifyStats":
                ValidateKeys(effect, file, path, ["kind", "target", "attack", "health"], ["kind"], issues);
                ValidateRequiredTarget(effect, path, typeIds, tagIds, issues);
                var hasAttack = TryOptionalInt(effect, "attack", file, path + ".attack", issues, out var attack);
                var hasHealth = TryOptionalInt(effect, "health", file, path + ".health", issues, out var health);
                if (!effect.TryGetProperty("attack", out _) && !effect.TryGetProperty("health", out _))
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path, "modifyStats requires 'attack' and/or 'health'."));
                else if ((!hasAttack || attack == 0) && (!hasHealth || health == 0))
                    issues.Add(new("INVALID_VALUE", file, path, "modifyStats requires a non-zero attack or health delta."));
                break;
            case "dealDamage":
                ValidateKeys(effect, file, path, ["kind", "target", "amount"], ["kind"], issues);
                ValidateRequiredTarget(effect, path, typeIds, tagIds, issues);
                ValidateRequiredPositiveInt(effect, "amount", path, issues);
                break;
            case "summonUnit":
                ValidateKeys(effect, file, path, ["kind", "unitId", "count"], ["kind"], issues);
                if (RequireParameterString(effect, "unitId", path, issues, out var unitId) && !unitIds.Contains(unitId!))
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".unitId", $"Unknown unit '{unitId}'."));
                if (TryOptionalInt(effect, "count", file, path + ".count", issues, out var count) && count <= 0)
                    issues.Add(new("INVALID_VALUE", file, path + ".count", "count must be positive."));
                break;
            case "addBehavior":
            case "removeBehavior":
                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId"], ["kind"], issues);
                ValidateRequiredTarget(effect, path, typeIds, tagIds, issues);
                if (RequireParameterString(effect, "behaviorId", path, issues, out var behaviorId) && !behaviorIds.Contains(behaviorId!))
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".behaviorId", $"Unknown behavior '{behaviorId}'."));
                break;
            case "addResource":
                ValidateKeys(effect, file, path, ["kind", "amount"], ["kind"], issues);
                if (RequireParameterInt(effect, "amount", path, issues, out var resourceAmount) && resourceAmount == 0)
                    issues.Add(new("INVALID_VALUE", file, path + ".amount", "amount cannot be zero."));
                break;
            default:
                ValidateKeys(effect, file, path, ["kind"], ["kind"], issues);
                issues.Add(new("UNSUPPORTED_EFFECT", file, path + ".kind", $"Effect kind '{kind}' is not supported."));
                break;
        }
    }

    private static void ValidateRequiredTarget(JsonElement effect, string path, IReadOnlySet<string> typeIds, IReadOnlySet<string> tagIds, List<ModValidationIssue> issues)
    {
        const string file = "content/units.json";
        if (!effect.TryGetProperty("target", out var target))
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + ".target", "Effect requires 'target'."));
            return;
        }
        if (target.ValueKind != JsonValueKind.Object) { issues.Add(new("INVALID_TYPE", file, path + ".target", "Expected an object.")); return; }
        var targetPath = path + ".target";
        ValidateKeys(target, file, targetPath, ["scope", "typeId", "tagId"], ["scope"], issues);
        if (RequireNonEmptyString(target, "scope", file, targetPath + ".scope", issues, out var scope) &&
            scope is not ("self" or "randomFriendly" or "randomEnemy" or "allFriendly" or "allEnemy"))
            issues.Add(new("INVALID_VALUE", file, targetPath + ".scope", $"Unknown target scope '{scope}'."));
        if (target.TryGetProperty("typeId", out _) && RequireNonEmptyString(target, "typeId", file, targetPath + ".typeId", issues, out var typeId) && !typeIds.Contains(typeId!))
            issues.Add(new("UNKNOWN_REFERENCE", file, targetPath + ".typeId", $"Unknown unit type '{typeId}'."));
        if (target.TryGetProperty("tagId", out _) && RequireNonEmptyString(target, "tagId", file, targetPath + ".tagId", issues, out var tagId) && !tagIds.Contains(tagId!))
            issues.Add(new("UNKNOWN_REFERENCE", file, targetPath + ".tagId", $"Unknown tag '{tagId}'."));
    }

    private static bool RequireParameterString(JsonElement effect, string property, string path, List<ModValidationIssue> issues, out string? value)
    {
        const string file = "content/units.json";
        value = null;
        if (!effect.TryGetProperty(property, out _))
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Effect requires '{property}'."));
            return false;
        }
        return RequireNonEmptyString(effect, property, file, path + "." + property, issues, out value);
    }

    private static bool RequireParameterInt(JsonElement effect, string property, string path, List<ModValidationIssue> issues, out int value)
    {
        const string file = "content/units.json";
        value = default;
        if (!effect.TryGetProperty(property, out _))
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Effect requires '{property}'."));
            return false;
        }
        return TryInt(effect, property, file, path + "." + property, issues, out value);
    }

    private static void ValidateRequiredPositiveInt(JsonElement effect, string property, string path, List<ModValidationIssue> issues)
    {
        const string file = "content/units.json";
        if (RequireParameterInt(effect, property, path, issues, out var value) && value <= 0)
            issues.Add(new("INVALID_VALUE", file, path + "." + property, $"{property} must be positive."));
    }

    private static void ValidatePool(JsonDocument? document, IReadOnlySet<string> unitIds, List<ModValidationIssue> issues)
    {
        const string file = "content/pool.json";
        if (!TryArray(document, file, "$", issues, out var root)) return;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in root.EnumerateArray())
        {
            var path = $"$[{index}]";
            if (item.ValueKind != JsonValueKind.Object) { issues.Add(new("INVALID_TYPE", file, path, "Expected an object.")); index++; continue; }
            ValidateKeys(item, file, path, ["unitId", "copies"], ["unitId", "copies"], issues);
            if (RequireNonEmptyString(item, "unitId", file, path + ".unitId", issues, out var unitId))
            {
                if (!seen.Add(unitId!)) issues.Add(new("DUPLICATE_ID", file, path + ".unitId", $"Duplicate pool entry for '{unitId}'."));
                if (!unitIds.Contains(unitId!)) issues.Add(new("UNKNOWN_REFERENCE", file, path + ".unitId", $"Unknown unit '{unitId}'."));
            }
            if (TryInt(item, "copies", file, path + ".copies", issues, out var copies) && copies <= 0) issues.Add(new("INVALID_VALUE", file, path + ".copies", "copies must be positive."));
            index++;
        }
    }

    private static void ValidateKeys(JsonElement element, string file, string path, IEnumerable<string> allowed, IEnumerable<string> required, List<ModValidationIssue> issues)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        var present = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var property in present.Where(name => !allowedSet.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("UNKNOWN_KEY", file, path + "." + property, $"Unknown key '{property}'."));
        foreach (var property in required.Where(name => !present.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("MISSING_REQUIRED_KEY", file, path + "." + property, $"Required key '{property}' is missing."));
    }

    private static bool TryObject(JsonDocument? document, string file, string path, List<ModValidationIssue> issues, out JsonElement element)
    {
        element = default;
        if (document is null) return false;
        element = document.RootElement;
        if (element.ValueKind == JsonValueKind.Object) return true;
        issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
        return false;
    }

    private static bool TryArray(JsonDocument? document, string file, string path, List<ModValidationIssue> issues, out JsonElement element)
    {
        element = default;
        if (document is null) return false;
        element = document.RootElement;
        if (element.ValueKind == JsonValueKind.Array) return true;
        issues.Add(new("INVALID_TYPE", file, path, "Expected an array."));
        return false;
    }

    private static bool TryInt(JsonElement parent, string property, string file, string path, List<ModValidationIssue> issues, out int value)
    {
        value = default;
        if (!parent.TryGetProperty(property, out var element)) return false;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value)) return true;
        issues.Add(new("INVALID_TYPE", file, path, "Expected an integer."));
        return false;
    }

    private static bool TryOptionalInt(JsonElement parent, string property, string file, string path, List<ModValidationIssue> issues, out int value)
    {
        value = default;
        if (!parent.TryGetProperty(property, out _)) return false;
        return TryInt(parent, property, file, path, issues, out value);
    }

    private static bool RequireNonEmptyString(JsonElement parent, string property, string file, string path, List<ModValidationIssue> issues) =>
        RequireNonEmptyString(parent, property, file, path, issues, out _);

    private static bool RequireNonEmptyString(JsonElement parent, string property, string file, string path, List<ModValidationIssue> issues, out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(property, out var element)) return false;
        if (element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString())) { value = element.GetString(); return true; }
        issues.Add(new("INVALID_TYPE", file, path, "Expected a non-empty string."));
        return false;
    }

    private static List<int>? ValidateIntArray(JsonElement parent, string property, string file, List<ModValidationIssue> issues, bool positiveOnly)
    {
        if (!parent.TryGetProperty(property, out var element)) return null;
        var path = "$." + property;
        if (element.ValueKind != JsonValueKind.Array) { issues.Add(new("INVALID_TYPE", file, path, "Expected an array.")); return null; }
        var result = new List<int>();
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var value)) issues.Add(new("INVALID_TYPE", file, $"{path}[{index}]", "Expected an integer."));
            else
            {
                result.Add(value);
                if (positiveOnly ? value <= 0 : value < 0) issues.Add(new("INVALID_VALUE", file, $"{path}[{index}]", positiveOnly ? "Value must be positive." : "Value cannot be negative."));
            }
            index++;
        }
        return result;
    }
}
