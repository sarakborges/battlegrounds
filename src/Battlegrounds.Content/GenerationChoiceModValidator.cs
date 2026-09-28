using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class GenerationChoiceModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> UnitPreparationEvents =
    [
        "onAcquire",
        "onPlay",
        "onTurnStart",
        "onTurnEnd",
    ];

    private static readonly HashSet<string> PowerPreparationEvents =
    [
        "onActivate",
        "onMatchStart",
        "onTurnStart",
        "onTurnEnd",
    ];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var unitIds = ReadEntityIds(modDirectory, "content/units");
        var typeIds = ReadEntityIds(modDirectory, "content/types");
        var tagIds = ReadEntityIds(modDirectory, "content/tags");

        foreach (var file in ReadEntityDirectory(modDirectory, "content/units"))
            ValidateTriggers(file, unitIds, typeIds, tagIds, powerMode: false, issues);
        foreach (var file in ReadEntityDirectory(modDirectory, "content/powers"))
            ValidateTriggers(file, unitIds, typeIds, tagIds, powerMode: true, issues);

        return issues;
    }

    private static void ValidateTriggers(
        EntityFile file,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        bool powerMode,
        List<ModValidationIssue> issues)
    {
        if (!file.Root.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array)
            return;

        var triggerIndex = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            if (trigger.ValueKind != JsonValueKind.Object)
            {
                triggerIndex++;
                continue;
            }

            var eventName = trigger.TryGetProperty("event", out var eventElement) && eventElement.ValueKind == JsonValueKind.String
                ? eventElement.GetString()
                : null;
            if (!trigger.TryGetProperty("effects", out var effects) || effects.ValueKind != JsonValueKind.Array)
            {
                triggerIndex++;
                continue;
            }

            var effectIndex = 0;
            foreach (var effect in effects.EnumerateArray())
            {
                if (effect.ValueKind == JsonValueKind.Object &&
                    effect.TryGetProperty("kind", out var kindElement) &&
                    kindElement.ValueKind == JsonValueKind.String)
                {
                    var path = $"$.triggers[{triggerIndex}].effects[{effectIndex}]";
                    var kind = kindElement.GetString();
                    if (kind == "generateUnitToReserve")
                    {
                        ValidatePreparationContext(file.Path, path, eventName, powerMode, issues);
                        ValidateGenerateToReserve(file.Path, effect, path, unitIds, issues);
                    }
                    else if (kind == "generateUnitChoice")
                    {
                        ValidatePreparationContext(file.Path, path, eventName, powerMode, issues);
                        ValidateGenerateChoice(file.Path, effect, path, typeIds, tagIds, issues);
                    }
                }
                effectIndex++;
            }

            triggerIndex++;
        }
    }

    private static void ValidatePreparationContext(
        string file,
        string path,
        string? eventName,
        bool powerMode,
        List<ModValidationIssue> issues)
    {
        var allowed = powerMode ? PowerPreparationEvents : UnitPreparationEvents;
        if (eventName is null || !allowed.Contains(eventName))
        {
            issues.Add(new(
                "INVALID_EFFECT_CONTEXT",
                file,
                path + ".kind",
                "Reserve generation and pending choices are only valid in preparation-only triggers."));
        }
    }

    private static void ValidateGenerateToReserve(
        string file,
        JsonElement effect,
        string path,
        IReadOnlySet<string> unitIds,
        List<ModValidationIssue> issues)
    {
        ValidateKeys(effect, file, path, ["kind", "unitId", "count"], ["kind", "unitId"], issues);
        if (TryRequiredString(effect, "unitId", file, path + ".unitId", issues, out var unitId) &&
            !unitIds.Contains(unitId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, path + ".unitId", $"Unknown unit '{unitId}'."));
        }

        if (effect.TryGetProperty("count", out var count))
        {
            if (count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out var value))
            {
                issues.Add(new("INVALID_TYPE", file, path + ".count", "Generation count currently requires an integer literal."));
            }
            else if (value <= 0)
            {
                issues.Add(new("INVALID_VALUE", file, path + ".count", "Generation count must be positive."));
            }
        }
    }

    private static void ValidateGenerateChoice(
        string file,
        JsonElement effect,
        string path,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        ValidateKeys(
            effect,
            file,
            path,
            ["kind", "generationQuery", "optionCount"],
            ["kind", "generationQuery"],
            issues);

        if (effect.TryGetProperty("optionCount", out var optionCount))
        {
            if (optionCount.ValueKind != JsonValueKind.Number || !optionCount.TryGetInt32(out var value))
                issues.Add(new("INVALID_TYPE", file, path + ".optionCount", "Expected an integer."));
            else if (value <= 0)
                issues.Add(new("INVALID_VALUE", file, path + ".optionCount", "optionCount must be positive."));
        }

        if (!effect.TryGetProperty("generationQuery", out var query)) return;
        if (query.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path + ".generationQuery", "Expected an object."));
            return;
        }

        var queryPath = path + ".generationQuery";
        ValidateKeys(
            query,
            file,
            queryPath,
            ["minimumTier", "maximumTier", "typeId", "tagId", "excludeSource"],
            [],
            issues);

        var minimum = TryOptionalPositiveInt(query, "minimumTier", file, queryPath, issues);
        var maximum = TryOptionalPositiveInt(query, "maximumTier", file, queryPath, issues);
        if (minimum is not null && maximum is not null && minimum.Value > maximum.Value)
        {
            issues.Add(new("INVALID_VALUE", file, queryPath, "minimumTier cannot exceed maximumTier."));
        }

        if (query.TryGetProperty("typeId", out _) &&
            TryRequiredString(query, "typeId", file, queryPath + ".typeId", issues, out var typeId) &&
            !typeIds.Contains(typeId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, queryPath + ".typeId", $"Unknown unit type '{typeId}'."));
        }

        if (query.TryGetProperty("tagId", out _) &&
            TryRequiredString(query, "tagId", file, queryPath + ".tagId", issues, out var tagId) &&
            !tagIds.Contains(tagId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, queryPath + ".tagId", $"Unknown tag '{tagId}'."));
        }

        if (query.TryGetProperty("excludeSource", out var exclude) &&
            exclude.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            issues.Add(new("INVALID_TYPE", file, queryPath + ".excludeSource", "Expected a boolean."));
        }
    }

    private static int? TryOptionalPositiveInt(
        JsonElement parent,
        string property,
        string file,
        string path,
        List<ModValidationIssue> issues)
    {
        if (!parent.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
        {
            issues.Add(new("INVALID_TYPE", file, path + "." + property, "Expected an integer."));
            return null;
        }
        if (parsed <= 0)
        {
            issues.Add(new("INVALID_VALUE", file, path + "." + property, property + " must be positive."));
            return null;
        }
        return parsed;
    }

    private static HashSet<string> ReadEntityIds(string root, string relativeDirectory) =>
        ReadEntityDirectory(root, relativeDirectory)
            .Select(file => file.Root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

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
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Required parameter '{property}' is missing."));
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

    private sealed record EntityFile(string Path, JsonElement Root);
}
