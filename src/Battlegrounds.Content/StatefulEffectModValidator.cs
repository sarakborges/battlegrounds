using System.Text.Json;

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

        foreach (var file in ReadEntityDirectory(modDirectory, "content/units"))
            ValidateTriggers(file, issues);
        foreach (var file in ReadEntityDirectory(modDirectory, "content/powers"))
            ValidateTriggers(file, issues);

        return issues;
    }

    private static void ValidateTriggers(EntityFile file, List<ModValidationIssue> issues)
    {
        if (!file.Root.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array)
            return;

        var index = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            if (trigger.ValueKind == JsonValueKind.Object &&
                trigger.TryGetProperty("activationLimit", out var limit))
            {
                ValidateActivationLimit(file.Path, limit, $"$.triggers[{index}].activationLimit", issues);
            }
            index++;
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

        ValidateKeys(limit, file, path, ["scope", "count"], ["scope", "count"], issues);

        if (TryRequiredString(limit, "scope", file, path + ".scope", issues, out var scope) &&
            !Scopes.Contains(scope!))
        {
            issues.Add(new("INVALID_VALUE", file, path + ".scope", $"Unknown activation-limit scope '{scope}'."));
        }

        if (TryRequiredInt(limit, "count", file, path + ".count", issues, out var count) && count <= 0)
        {
            issues.Add(new("INVALID_VALUE", file, path + ".count", "activationLimit count must be positive."));
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
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Required activation-limit parameter '{property}' is missing."));
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
