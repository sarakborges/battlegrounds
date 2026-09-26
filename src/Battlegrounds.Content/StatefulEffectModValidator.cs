using System.Text.Json;
using Battlegrounds.Core.Domain.Effects;

namespace Battlegrounds.Content;

internal sealed class StatefulEffectModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> Scopes = ["turn", "combat", "match"];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var typeIds = ReadEntityIds(modDirectory, "content/types");
        var tagIds = ReadEntityIds(modDirectory, "content/tags");

        foreach (var file in ReadEntityDirectory(modDirectory, "content/units"))
            ValidateTriggers(file, typeIds, tagIds, issues);
        foreach (var file in ReadEntityDirectory(modDirectory, "content/powers"))
            ValidateTriggers(file, typeIds, tagIds, issues);

        return issues;
    }

    private static void ValidateTriggers(
        EntityFile file,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (!file.Root.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            if (trigger.ValueKind != JsonValueKind.Object)
            {
                index++;
                continue;
            }

            var path = $"$.triggers[{index}]";
            var eventName = trigger.TryGetProperty("event", out var eventElement) &&
                            eventElement.ValueKind == JsonValueKind.String
                ? eventElement.GetString()
                : null;

            if (trigger.TryGetProperty("activationLimit", out var limit))
                ValidateActivationLimit(file.Path, limit, path + ".activationLimit", issues);

            ValidateCountedTrigger(
                file.Path,
                trigger,
                path,
                eventName,
                typeIds,
                tagIds,
                issues);

            index++;
        }
    }

    private static void ValidateCountedTrigger(
        string file,
        JsonElement trigger,
        string path,
        string? eventName,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        var isFriendlyDeaths = eventName == NativeTriggerKeys.AfterFriendlyDeaths.Value;
        var isEventCount = eventName == NativeTriggerKeys.AfterEventCount.Value;

        if (isFriendlyDeaths || isEventCount)
        {
            if (!trigger.TryGetProperty("count", out var countElement))
            {
                issues.Add(new(
                    "MISSING_REQUIRED_PARAMETER",
                    file,
                    path + ".count",
                    $"{eventName} requires 'count'."));
            }
            else if (countElement.ValueKind != JsonValueKind.Number || !countElement.TryGetInt32(out var count))
            {
                issues.Add(new("INVALID_TYPE", file, path + ".count", "Expected an integer."));
            }
            else if (count <= 0)
            {
                issues.Add(new("INVALID_VALUE", file, path + ".count", "count must be positive."));
            }
        }
        else if (trigger.TryGetProperty("count", out _))
        {
            issues.Add(new(
                "INVALID_PARAMETER",
                file,
                path + ".count",
                "count is only valid for counted triggers."));
        }

        if (isEventCount)
        {
            if (!trigger.TryGetProperty("counter", out var counter))
            {
                issues.Add(new(
                    "MISSING_REQUIRED_PARAMETER",
                    file,
                    path + ".counter",
                    "afterEventCount requires 'counter'."));
            }
            else
            {
                ValidateHistoryQuery(file, counter, path + ".counter", typeIds, tagIds, issues);
            }
        }
        else if (trigger.TryGetProperty("counter", out _))
        {
            issues.Add(new(
                "INVALID_PARAMETER",
                file,
                path + ".counter",
                "counter is only valid for afterEventCount triggers."));
        }
    }

    private static void ValidateHistoryQuery(
        string file,
        JsonElement query,
        string path,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (query.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
            return;
        }

        ValidateKeys(
            query,
            file,
            path,
            ["event", "scope", "typeId", "tagId"],
            ["event", "scope"],
            issues,
            "history-counter");

        if (TryRequiredString(query, "event", file, path + ".event", issues, out var eventName))
        {
            var key = new NativeGameEventKey(eventName!);
            if (!NativeGameEventKeys.IsSupported(key))
                issues.Add(new("INVALID_VALUE", file, path + ".event", $"Unknown game event '{eventName}'."));
        }

        if (TryRequiredString(query, "scope", file, path + ".scope", issues, out var scope) &&
            !Scopes.Contains(scope!))
        {
            issues.Add(new("INVALID_VALUE", file, path + ".scope", $"Unknown history scope '{scope}'."));
        }

        if (query.TryGetProperty("typeId", out _) &&
            TryRequiredString(query, "typeId", file, path + ".typeId", issues, out var typeId) &&
            !typeIds.Contains(typeId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, path + ".typeId", $"Unknown unit type '{typeId}'."));
        }

        if (query.TryGetProperty("tagId", out _) &&
            TryRequiredString(query, "tagId", file, path + ".tagId", issues, out var tagId) &&
            !tagIds.Contains(tagId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, path + ".tagId", $"Unknown tag '{tagId}'."));
        }
    }

    private static void ValidateActivationLimit(
        string file,
        JsonElement limit,
        string path,
        List<ModValidationIssue> issues)
    {
        if (limit.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
            return;
        }

        ValidateKeys(
            limit,
            file,
            path,
            ["scope", "count"],
            ["scope", "count"],
            issues,
            "activation-limit");

        if (TryRequiredString(limit, "scope", file, path + ".scope", issues, out var scope) &&
            !Scopes.Contains(scope!))
        {
            issues.Add(new("INVALID_VALUE", file, path + ".scope", $"Unknown activation-limit scope '{scope}'."));
        }

        if (TryRequiredInt(limit, "count", file, path + ".count", issues, out var count) && count <= 0)
            issues.Add(new("INVALID_VALUE", file, path + ".count", "activationLimit count must be positive."));
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
                // Base validators own malformed JSON reporting.
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
        List<ModValidationIssue> issues,
        string label)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        var present = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var property in present.Where(name => !allowedSet.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("UNKNOWN_KEY", file, path + "." + property, $"Unknown key '{property}'."));
        foreach (var property in required.Where(name => !present.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Required {label} parameter '{property}' is missing."));
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
