using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class AdvancedEffectModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> Scopes = ["self", "selected", "friendly", "enemy"];
    private static readonly HashSet<string> Selections =
    [
        "all", "random", "lowestAttack", "highestAttack", "lowestHealth", "highestHealth",
        "leftmost", "rightmost", "adjacent", "leftAdjacent", "rightAdjacent",
    ];
    private static readonly HashSet<string> RelativeTargets = ["source", "selected"];
    private static readonly HashSet<string> Zones = ["field", "reserve"];
    private static readonly HashSet<string> Comparisons =
    [
        "equal", "notEqual", "lessThan", "lessThanOrEqual", "greaterThan", "greaterThanOrEqual",
    ];
    private static readonly HashSet<string> Stats = ["attack", "health"];
    private static readonly HashSet<string> CombatOutcomes = ["win", "loss", "draw"];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var typeIds = ReadEntityIds(modDirectory, "content/types");
        var tagIds = ReadEntityIds(modDirectory, "content/tags");

        foreach (var file in ReadEntityDirectory(modDirectory, "content/units"))
            ValidateTriggers(file, typeIds, tagIds, powerMode: false, issues);
        foreach (var file in ReadEntityDirectory(modDirectory, "content/powers"))
            ValidateTriggers(file, typeIds, tagIds, powerMode: true, issues);
        foreach (var file in ReadEntityDirectory(modDirectory, "content/actions"))
            ValidateActionTargets(file, typeIds, tagIds, issues);

        return issues;
    }

    private static void ValidateActionTargets(
        EntityFile file,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (!file.Root.TryGetProperty("effects", out var effects) || effects.ValueKind != JsonValueKind.Array) return;
        var effectIndex = 0;
        foreach (var effect in effects.EnumerateArray())
        {
            if (effect.ValueKind == JsonValueKind.Object && effect.TryGetProperty("target", out var target))
            {
                ValidateTarget(
                    file.Path,
                    target,
                    $"$.effects[{effectIndex}].target",
                    allowSelected: true,
                    typeIds,
                    tagIds,
                    issues);
            }
            effectIndex++;
        }
    }

    private static void ValidateTriggers(
        EntityFile file,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        bool powerMode,
        List<ModValidationIssue> issues)
    {
        if (!file.Root.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array) return;

        var triggerIndex = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            if (trigger.ValueKind != JsonValueKind.Object)
            {
                triggerIndex++;
                continue;
            }

            var triggerPath = $"$.triggers[{triggerIndex}]";
            var eventName = trigger.TryGetProperty("event", out var eventElement) && eventElement.ValueKind == JsonValueKind.String
                ? eventElement.GetString()
                : null;
            var allowSelected =
                (powerMode && eventName == "onActivate") ||
                (!powerMode && (eventName == "onPlay" || eventName == "onAttack" || eventName == "onDamage"));

            if (trigger.TryGetProperty("conditions", out var conditions))
                ValidateConditions(file.Path, conditions, triggerPath + ".conditions", allowSelected, typeIds, tagIds, issues);

            if (trigger.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Array)
            {
                var effectIndex = 0;
                foreach (var effect in effects.EnumerateArray())
                {
                    if (effect.ValueKind == JsonValueKind.Object && effect.TryGetProperty("target", out var target))
                    {
                        ValidateTarget(
                            file.Path,
                            target,
                            $"{triggerPath}.effects[{effectIndex}].target",
                            allowSelected,
                            typeIds,
                            tagIds,
                            issues);
                    }
                    effectIndex++;
                }
            }

            triggerIndex++;
        }
    }

    private static void ValidateConditions(
        string file,
        JsonElement conditions,
        string path,
        bool allowSelected,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (conditions.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an array."));
            return;
        }
        if (conditions.GetArrayLength() == 0)
        {
            issues.Add(new("INVALID_VALUE", file, path, "conditions cannot be empty."));
            return;
        }

        var index = 0;
        foreach (var condition in conditions.EnumerateArray())
        {
            var conditionPath = $"{path}[{index}]";
            if (condition.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", file, conditionPath, "Expected an object."));
                index++;
                continue;
            }

            if (!TryRequiredString(condition, "kind", file, conditionPath + ".kind", issues, out var kind))
            {
                index++;
                continue;
            }

            switch (kind)
            {
                case "combatOutcome":
                    ValidateKeys(condition, file, conditionPath, ["kind", "outcome"], ["kind", "outcome"], issues);
                    if (TryRequiredString(condition, "outcome", file, conditionPath + ".outcome", issues, out var outcome) && !CombatOutcomes.Contains(outcome!))
                        issues.Add(new("INVALID_VALUE", file, conditionPath + ".outcome", $"Unknown combat outcome '{outcome}'."));
                    break;

                case "unitCount":
                    ValidateKeys(condition, file, conditionPath, ["kind", "query", "comparison", "value"], ["kind", "query", "comparison", "value"], issues);
                    if (condition.TryGetProperty("query", out var query))
                        ValidateQuery(file, query, conditionPath + ".query", allowSelected, typeIds, tagIds, issues);
                    ValidateComparison(condition, file, conditionPath, issues);
                    if (TryRequiredInt(condition, "value", file, conditionPath + ".value", issues, out var count) && count < 0)
                        issues.Add(new("INVALID_VALUE", file, conditionPath + ".value", "unitCount value cannot be negative."));
                    break;

                case "sourceStat":
                    ValidateKeys(condition, file, conditionPath, ["kind", "stat", "comparison", "value"], ["kind", "stat", "comparison", "value"], issues);
                    if (TryRequiredString(condition, "stat", file, conditionPath + ".stat", issues, out var stat) && !Stats.Contains(stat!))
                        issues.Add(new("INVALID_VALUE", file, conditionPath + ".stat", $"Unknown stat '{stat}'."));
                    ValidateComparison(condition, file, conditionPath, issues);
                    TryRequiredInt(condition, "value", file, conditionPath + ".value", issues, out _);
                    break;

                case "value":
                    ValidateKeys(condition, file, conditionPath, ["kind", "left", "comparison", "right"], ["kind", "left", "comparison", "right"], issues);
                    ValidateComparison(condition, file, conditionPath, issues);
                    break;

                default:
                    issues.Add(new("UNSUPPORTED_CONDITION", file, conditionPath + ".kind", $"Condition kind '{kind}' is not supported."));
                    break;
            }

            index++;
        }
    }

    private static void ValidateComparison(
        JsonElement condition,
        string file,
        string path,
        List<ModValidationIssue> issues)
    {
        if (TryRequiredString(condition, "comparison", file, path + ".comparison", issues, out var comparison) &&
            !Comparisons.Contains(comparison!))
        {
            issues.Add(new("INVALID_VALUE", file, path + ".comparison", $"Unknown comparison '{comparison}'."));
        }
    }

    private static void ValidateTarget(
        string file,
        JsonElement target,
        string path,
        bool allowSelected,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (target.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
            return;
        }

        ValidateKeys(
            target,
            file,
            path,
            ["scope", "selection", "excludeSource", "limit", "typeId", "tagId", "relativeTo", "zone"],
            ["scope"],
            issues);

        var scope = ValidateScope(target, file, path, allowSelected, issues);
        var selection = "all";
        if (target.TryGetProperty("selection", out _) &&
            TryRequiredString(target, "selection", file, path + ".selection", issues, out var parsedSelection))
        {
            selection = parsedSelection!;
            if (!Selections.Contains(selection))
                issues.Add(new("INVALID_VALUE", file, path + ".selection", $"Unknown target selection '{selection}'."));
        }

        var zone = ValidateZone(target, file, path, issues);
        var hasRelativeTo = target.TryGetProperty("relativeTo", out _);
        var relativeTo = "source";
        if (hasRelativeTo &&
            TryRequiredString(target, "relativeTo", file, path + ".relativeTo", issues, out var parsedRelativeTo))
        {
            relativeTo = parsedRelativeTo!;
            if (!RelativeTargets.Contains(relativeTo))
                issues.Add(new("INVALID_VALUE", file, path + ".relativeTo", $"Unknown target anchor '{relativeTo}'."));
        }

        var excludeSource = false;
        if (target.TryGetProperty("excludeSource", out var excludeElement))
        {
            if (excludeElement.ValueKind != JsonValueKind.True && excludeElement.ValueKind != JsonValueKind.False)
                issues.Add(new("INVALID_TYPE", file, path + ".excludeSource", "Expected a boolean."));
            else
                excludeSource = excludeElement.GetBoolean();
        }

        var hasLimit = target.TryGetProperty("limit", out _);
        if (hasLimit && TryRequiredInt(target, "limit", file, path + ".limit", issues, out var limit) && limit <= 0)
            issues.Add(new("INVALID_VALUE", file, path + ".limit", "limit must be positive."));

        ValidateSelectorCombination(file, path, scope, selection, relativeTo, zone, hasRelativeTo, allowSelected, excludeSource, hasLimit, issues);
        ValidateReferences(target, file, path, typeIds, tagIds, issues);
    }

    private static void ValidateQuery(
        string file,
        JsonElement query,
        string path,
        bool allowSelected,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (query.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
            return;
        }

        ValidateKeys(query, file, path, ["scope", "excludeSource", "typeId", "tagId", "zone"], ["scope"], issues);
        var scope = ValidateScope(query, file, path, allowSelected, issues);
        ValidateZone(query, file, path, issues);
        var excludeSource = false;
        if (query.TryGetProperty("excludeSource", out var excludeElement))
        {
            if (excludeElement.ValueKind != JsonValueKind.True && excludeElement.ValueKind != JsonValueKind.False)
                issues.Add(new("INVALID_TYPE", file, path + ".excludeSource", "Expected a boolean."));
            else
                excludeSource = excludeElement.GetBoolean();
        }
        if (excludeSource && scope != "friendly")
            issues.Add(new("INVALID_PARAMETER", file, path + ".excludeSource", "excludeSource is only valid for friendly queries."));
        ValidateReferences(query, file, path, typeIds, tagIds, issues);
    }

    private static string ValidateZone(JsonElement element, string file, string path, List<ModValidationIssue> issues)
    {
        if (!element.TryGetProperty("zone", out _)) return "field";
        if (!TryRequiredString(element, "zone", file, path + ".zone", issues, out var zone)) return "field";
        if (!Zones.Contains(zone!))
            issues.Add(new("INVALID_VALUE", file, path + ".zone", $"Unknown target zone '{zone}'."));
        return zone!;
    }

    private static string? ValidateScope(
        JsonElement element,
        string file,
        string path,
        bool allowSelected,
        List<ModValidationIssue> issues)
    {
        if (!TryRequiredString(element, "scope", file, path + ".scope", issues, out var scope)) return null;
        if (!Scopes.Contains(scope!))
            issues.Add(new("INVALID_VALUE", file, path + ".scope", $"Unknown target scope '{scope}'."));
        else if (scope == "selected" && !allowSelected)
            issues.Add(new("INVALID_VALUE", file, path + ".scope", "selected requires a trigger context target, currently unit onAttack, unit onDamage, or activatable power onActivate."));
        return scope;
    }

    private static void ValidateSelectorCombination(
        string file,
        string path,
        string? scope,
        string selection,
        string relativeTo,
        string zone,
        bool hasRelativeTo,
        bool allowSelected,
        bool excludeSource,
        bool hasLimit,
        List<ModValidationIssue> issues)
    {
        if (excludeSource && scope != "friendly")
            issues.Add(new("INVALID_PARAMETER", file, path + ".excludeSource", "excludeSource is only valid for friendly targets."));

        if (scope is "self" or "selected")
        {
            if (selection != "all")
                issues.Add(new("INVALID_PARAMETER", file, path + ".selection", "self/selected targets cannot use selection."));
            if (hasLimit)
                issues.Add(new("INVALID_PARAMETER", file, path + ".limit", "self/selected targets cannot use limit."));
        }

        var isAdjacentSelection = selection is "adjacent" or "leftAdjacent" or "rightAdjacent";
        if (isAdjacentSelection && zone != "field")
            issues.Add(new("INVALID_PARAMETER", file, path + ".zone", "Adjacent targeting is only valid on the Field."));
        if (hasRelativeTo && !isAdjacentSelection)
            issues.Add(new("INVALID_PARAMETER", file, path + ".relativeTo", "relativeTo is only valid for adjacent selections."));
        if (relativeTo == "selected" && !allowSelected)
            issues.Add(new("INVALID_VALUE", file, path + ".relativeTo", "selected relative targeting requires a context target."));
        if (isAdjacentSelection && relativeTo == "source" && scope != "friendly")
            issues.Add(new("INVALID_PARAMETER", file, path + ".selection", "Source-relative adjacent selection is only valid for friendly targets."));
    }

    private static void ValidateReferences(
        JsonElement element,
        string file,
        string path,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (element.TryGetProperty("typeId", out _) &&
            TryRequiredString(element, "typeId", file, path + ".typeId", issues, out var typeId) &&
            !typeIds.Contains(typeId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, path + ".typeId", $"Unknown unit type '{typeId}'."));
        }
        if (element.TryGetProperty("tagId", out _) &&
            TryRequiredString(element, "tagId", file, path + ".tagId", issues, out var tagId) &&
            !tagIds.Contains(tagId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, path + ".tagId", $"Unknown tag '{tagId}'."));
        }
    }

    private static IReadOnlyList<EntityFile> ReadEntityDirectory(string root, string relativeDirectory)
    {
        var directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory)) return [];

        var result = new List<EntityFile>();
        foreach (var fullPath in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    result.Add(new EntityFile(
                        relativeDirectory + "/" + Path.GetFileName(fullPath),
                        document.RootElement.Clone()));
                }
            }
            catch (JsonException)
            {
            }
        }
        return result;
    }

    private static HashSet<string> ReadEntityIds(string root, string relativeDirectory)
    {
        var directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory)) return [];
        return Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void ValidateKeys(
        JsonElement element,
        string file,
        string path,
        IEnumerable<string> allowed,
        IEnumerable<string> required,
        List<ModValidationIssue> issues)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        var present = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var property in present.Where(name => !allowedSet.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("UNKNOWN_KEY", file, path + "." + property, $"Unknown key '{property}'."));
        foreach (var property in required.Where(name => !present.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("MISSING_REQUIRED_KEY", file, path + "." + property, $"Required key '{property}' is missing."));
    }

    private static bool TryRequiredString(
        JsonElement parent,
        string property,
        string file,
        string path,
        List<ModValidationIssue> issues,
        out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(property, out var element)) return false;
        if (element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString()))
        {
            value = element.GetString();
            return true;
        }
        issues.Add(new("INVALID_TYPE", file, path, "Expected a non-empty string."));
        return false;
    }

    private static bool TryRequiredInt(
        JsonElement parent,
        string property,
        string file,
        string path,
        List<ModValidationIssue> issues,
        out int value)
    {
        value = default;
        if (!parent.TryGetProperty(property, out var element)) return false;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value)) return true;
        issues.Add(new("INVALID_TYPE", file, path, "Expected an integer."));
        return false;
    }

    private sealed record EntityFile(string Path, JsonElement Root);
}
