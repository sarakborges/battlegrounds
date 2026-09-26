using System.Text.Json;
using Battlegrounds.Core.Domain.Behaviors;

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
        using var units = ReadRequired(modDirectory, "content/units.json", issues);
        using var pool = ReadRequired(modDirectory, "content/pool.json", issues);

        ValidateManifest(manifest, issues);
        ValidateMatch(match, issues);
        var maximumTier = ValidatePreparation(preparation, issues);
        ValidateCombat(combat, issues);
        var behaviorHandlers = ValidateBehaviors(behaviors, issues);
        var unitIds = ValidateUnits(units, maximumTier, behaviorHandlers, issues);
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

        if (TryInt(root, "schemaVersion", file, "$.schemaVersion", issues, out var schemaVersion) && schemaVersion != 1)
            issues.Add(new("UNSUPPORTED_SCHEMA_VERSION", file, "$.schemaVersion", $"Unsupported schema version {schemaVersion}."));
        RequireNonEmptyString(root, "id", file, "$.id", issues);
        RequireNonEmptyString(root, "name", file, "$.name", issues);

        if (root.TryGetProperty("terminology", out var terminology))
        {
            if (terminology.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", file, "$.terminology", "Expected an object."));
            }
            else if (!terminology.EnumerateObject().Any())
            {
                issues.Add(new("INVALID_VALUE", file, "$.terminology", "Terminology cannot be empty."));
            }
            else
            {
                foreach (var pair in terminology.EnumerateObject())
                {
                    if (string.IsNullOrWhiteSpace(pair.Name) || pair.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(pair.Value.GetString()))
                        issues.Add(new("INVALID_VALUE", file, $"$.terminology.{pair.Name}", "Terminology keys and values must be non-empty strings."));
                }
            }
        }
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
        foreach (var key in keys.Take(9))
            if (TryInt(root, key, file, "$." + key, issues, out var value)) values[key] = value;

        foreach (var key in new[] { "startingResource", "resourcePerRound", "acquireCost", "releaseValue", "refreshCost" })
            if (values.TryGetValue(key, out var value) && value < 0) issues.Add(new("INVALID_VALUE", file, "$." + key, $"{key} cannot be negative."));
        if (values.TryGetValue("fieldCapacity", out var field) && field <= 0) issues.Add(new("INVALID_VALUE", file, "$.fieldCapacity", "fieldCapacity must be positive."));
        if (values.TryGetValue("reserveCapacity", out var reserve) && reserve <= 0) issues.Add(new("INVALID_VALUE", file, "$.reserveCapacity", "reserveCapacity must be positive."));
        if (values.TryGetValue("maximumTier", out var maxTier) && maxTier <= 1) issues.Add(new("INVALID_VALUE", file, "$.maximumTier", "maximumTier must be greater than 1."));
        if (values.TryGetValue("startingResource", out var start) && values.TryGetValue("maximumResource", out var maxResource) && maxResource < start)
            issues.Add(new("INVALID_VALUE", file, "$.maximumResource", "maximumResource cannot be lower than startingResource."));

        var offerSizes = ValidateIntArray(root, "offerSizesByTier", file, issues, positiveOnly: true);
        var upgradeCosts = ValidateIntArray(root, "initialUpgradeCostsByTier", file, issues, positiveOnly: false);
        if (values.TryGetValue("maximumTier", out maxTier))
        {
            if (offerSizes is not null && offerSizes.Count != maxTier)
                issues.Add(new("INVALID_LENGTH", file, "$.offerSizesByTier", "Offer size must be defined for every tier."));
            if (upgradeCosts is not null && upgradeCosts.Count != maxTier - 1)
                issues.Add(new("INVALID_LENGTH", file, "$.initialUpgradeCostsByTier", "Upgrade cost must be defined for every non-maximum tier."));
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
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
                index++;
                continue;
            }
            ValidateKeys(item, file, path, ["id", "name", "handler"], ["id", "name", "handler"], issues);
            RequireNonEmptyString(item, "name", file, path + ".name", issues);
            var hasId = RequireNonEmptyString(item, "id", file, path + ".id", issues, out var id);
            var hasHandler = RequireNonEmptyString(item, "handler", file, path + ".handler", issues, out var handler);
            if (hasId && !result.TryAdd(id!, handler ?? string.Empty))
                issues.Add(new("DUPLICATE_ID", file, path + ".id", $"Duplicate behavior id '{id}'."));
            if (hasHandler)
            {
                var key = new NativeBehaviorKey(handler!);
                if (!NativeBehaviorKeys.IsSupported(key))
                    issues.Add(new("UNSUPPORTED_HANDLER", file, path + ".handler", $"Native behavior handler '{handler}' is not supported."));
            }
            index++;
        }
        return result;
    }

    private static HashSet<string> ValidateUnits(JsonDocument? document, int? maximumTier, IReadOnlyDictionary<string, string> behaviorHandlers, List<ModValidationIssue> issues)
    {
        const string file = "content/units.json";
        var unitIds = new HashSet<string>(StringComparer.Ordinal);
        if (!TryArray(document, file, "$", issues, out var root)) return unitIds;
        if (!root.EnumerateArray().Any()) issues.Add(new("INVALID_VALUE", file, "$", "A mod must define at least one unit."));

        var index = 0;
        foreach (var item in root.EnumerateArray())
        {
            var path = $"$[{index}]";
            if (item.ValueKind != JsonValueKind.Object) { issues.Add(new("INVALID_TYPE", file, path, "Expected an object.")); index++; continue; }
            ValidateKeys(item, file, path, ["id", "name", "tier", "attack", "health", "behaviors"], ["id", "name", "tier", "attack", "health"], issues);
            var hasId = RequireNonEmptyString(item, "id", file, path + ".id", issues, out var id);
            RequireNonEmptyString(item, "name", file, path + ".name", issues);
            if (hasId && !unitIds.Add(id!)) issues.Add(new("DUPLICATE_ID", file, path + ".id", $"Duplicate unit id '{id}'."));
            if (TryInt(item, "tier", file, path + ".tier", issues, out var tier))
            {
                if (tier <= 0) issues.Add(new("INVALID_VALUE", file, path + ".tier", "tier must be positive."));
                if (maximumTier is not null && tier > maximumTier) issues.Add(new("INVALID_VALUE", file, path + ".tier", $"tier {tier} exceeds maximumTier {maximumTier}."));
            }
            if (TryInt(item, "attack", file, path + ".attack", issues, out var attack) && attack < 0) issues.Add(new("INVALID_VALUE", file, path + ".attack", "attack cannot be negative."));
            if (TryInt(item, "health", file, path + ".health", issues, out var health) && health <= 0) issues.Add(new("INVALID_VALUE", file, path + ".health", "health must be positive."));

            if (item.TryGetProperty("behaviors", out var behaviors))
            {
                if (behaviors.ValueKind != JsonValueKind.Array) issues.Add(new("INVALID_TYPE", file, path + ".behaviors", "Expected an array."));
                else
                {
                    var seenIds = new HashSet<string>(StringComparer.Ordinal);
                    var seenHandlers = new HashSet<string>(StringComparer.Ordinal);
                    var behaviorIndex = 0;
                    foreach (var behavior in behaviors.EnumerateArray())
                    {
                        var behaviorPath = $"{path}.behaviors[{behaviorIndex}]";
                        if (behavior.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(behavior.GetString()))
                            issues.Add(new("INVALID_TYPE", file, behaviorPath, "Behavior reference must be a non-empty string."));
                        else
                        {
                            var behaviorId = behavior.GetString()!;
                            if (!seenIds.Add(behaviorId)) issues.Add(new("DUPLICATE_REFERENCE", file, behaviorPath, $"Behavior '{behaviorId}' is referenced more than once."));
                            if (!behaviorHandlers.TryGetValue(behaviorId, out var handler)) issues.Add(new("UNKNOWN_REFERENCE", file, behaviorPath, $"Unknown behavior '{behaviorId}'."));
                            else if (!seenHandlers.Add(handler)) issues.Add(new("DUPLICATE_NATIVE_BEHAVIOR", file, behaviorPath, $"Native behavior handler '{handler}' is attached more than once."));
                        }
                        behaviorIndex++;
                    }
                }
            }
            index++;
        }
        return unitIds;
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
        var present = element.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
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
