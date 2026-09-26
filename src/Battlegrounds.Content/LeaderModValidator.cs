using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class LeaderModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory))
        {
            return issues;
        }

        const string file = "content/leaders.json";
        var path = Path.Combine(modDirectory, "content", "leaders.json");
        if (!File.Exists(path))
        {
            issues.Add(new("MISSING_REQUIRED_FILE", file, "$", $"Required file '{file}' is missing."));
            return issues;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
        }
        catch (JsonException exception)
        {
            issues.Add(new("INVALID_JSON", file, exception.Path ?? "$", "File contains invalid JSON."));
            return issues;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new("INVALID_TYPE", file, "$", "Expected an array."));
                return issues;
            }

            var root = document.RootElement;
            if (root.GetArrayLength() == 0)
            {
                issues.Add(new("INVALID_VALUE", file, "$", "A mod must define at least one leader."));
                return issues;
            }

            var startingHealth = TryReadStartingHealth(modDirectory);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var item in root.EnumerateArray())
            {
                var itemPath = $"$[{index}]";
                if (item.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new("INVALID_TYPE", file, itemPath, "Expected an object."));
                    index++;
                    continue;
                }

                ValidateKeys(
                    item,
                    file,
                    itemPath,
                    ["id", "name", "healthModifier", "armor"],
                    ["id", "name", "healthModifier", "armor"],
                    issues);

                if (TryRequiredString(item, "id", file, itemPath + ".id", issues, out var id) && !ids.Add(id!))
                {
                    issues.Add(new("DUPLICATE_ID", file, itemPath + ".id", $"Duplicate leader id '{id}'."));
                }

                TryRequiredString(item, "name", file, itemPath + ".name", issues, out _);

                var hasHealthModifier = TryRequiredInt(
                    item,
                    "healthModifier",
                    file,
                    itemPath + ".healthModifier",
                    issues,
                    out var healthModifier);
                var hasArmor = TryRequiredInt(
                    item,
                    "armor",
                    file,
                    itemPath + ".armor",
                    issues,
                    out var armor);

                if (hasArmor && armor < 0)
                {
                    issues.Add(new("INVALID_VALUE", file, itemPath + ".armor", "armor cannot be negative."));
                }

                if (hasHealthModifier && startingHealth is not null && startingHealth.Value + healthModifier <= 0)
                {
                    issues.Add(new(
                        "INVALID_VALUE",
                        file,
                        itemPath + ".healthModifier",
                        "healthModifier must leave the player with positive starting Health."));
                }

                index++;
            }
        }

        return issues;
    }

    private static int? TryReadStartingHealth(string modDirectory)
    {
        var path = Path.Combine(modDirectory, "rules", "match.json");
        if (!File.Exists(path)) return null;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("startingHealth", out var value) ||
                value.ValueKind != JsonValueKind.Number ||
                !value.TryGetInt32(out var startingHealth))
            {
                return null;
            }

            return startingHealth;
        }
        catch (JsonException)
        {
            return null;
        }
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
        {
            issues.Add(new("UNKNOWN_KEY", file, path + "." + property, $"Unknown key '{property}'."));
        }

        foreach (var property in required.Where(name => !present.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
        {
            issues.Add(new("MISSING_REQUIRED_KEY", file, path + "." + property, $"Required key '{property}' is missing."));
        }
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
}
