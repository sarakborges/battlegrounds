using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class UnitCombineModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly string[] Keys = ["id", "name", "sourceUnitId", "requiredCopies", "resultUnitId"];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var directory = Path.Combine(modDirectory, "content", "combines");
        if (!Directory.Exists(directory)) return issues;

        var unitIds = ReadUnitIds(modDirectory);
        var combineIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var fullPath in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var relativePath = "content/combines/" + Path.GetFileName(fullPath);
            JsonElement root;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new("INVALID_TYPE", relativePath, "$", "Combine file must contain one JSON object."));
                    continue;
                }
                root = document.RootElement.Clone();
            }
            catch (JsonException exception)
            {
                issues.Add(new("INVALID_JSON", relativePath, exception.Path ?? "$", "File contains invalid JSON."));
                continue;
            }

            ValidateKeys(root, relativePath, issues);
            var id = ReadRequiredString(root, "id", relativePath, issues);
            var name = ReadRequiredString(root, "name", relativePath, issues);
            var sourceUnitId = ReadRequiredString(root, "sourceUnitId", relativePath, issues);
            var resultUnitId = ReadRequiredString(root, "resultUnitId", relativePath, issues);
            var requiredCopies = ReadRequiredInt(root, "requiredCopies", relativePath, issues);

            if (id is not null)
            {
                var fileStem = Path.GetFileNameWithoutExtension(fullPath);
                if (!string.Equals(id, fileStem, StringComparison.Ordinal))
                    issues.Add(new("ID_FILENAME_MISMATCH", relativePath, "$.id", $"Entity id '{id}' must match file name '{fileStem}.json'."));
                if (!combineIds.Add(id))
                    issues.Add(new("DUPLICATE_ID", relativePath, "$.id", $"Duplicate unit combine id '{id}'."));
            }

            _ = name;
            if (requiredCopies is not null && requiredCopies.Value < 2)
                issues.Add(new("INVALID_VALUE", relativePath, "$.requiredCopies", "requiredCopies must be at least 2."));
            if (sourceUnitId is not null && !unitIds.Contains(sourceUnitId))
                issues.Add(new("UNKNOWN_REFERENCE", relativePath, "$.sourceUnitId", $"Unknown unit id '{sourceUnitId}'."));
            if (resultUnitId is not null && !unitIds.Contains(resultUnitId))
                issues.Add(new("UNKNOWN_REFERENCE", relativePath, "$.resultUnitId", $"Unknown unit id '{resultUnitId}'."));
        }

        return issues;
    }

    private static HashSet<string> ReadUnitIds(string root)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var directory = Path.Combine(root, "content", "units");
        if (!Directory.Exists(directory)) return result;

        foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("id", out var id) &&
                    id.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(id.GetString()))
                    result.Add(id.GetString()!);
            }
            catch (JsonException)
            {
                // Base validation owns malformed unit diagnostics.
            }
        }

        return result;
    }

    private static void ValidateKeys(JsonElement root, string file, List<ModValidationIssue> issues)
    {
        var allowed = Keys.ToHashSet(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
            if (!allowed.Contains(property.Name))
                issues.Add(new("UNKNOWN_KEY", file, "$." + property.Name, $"Unknown property '{property.Name}'."));

        foreach (var key in Keys)
            if (!root.TryGetProperty(key, out _))
                issues.Add(new("MISSING_REQUIRED_PARAMETER", file, "$." + key, $"Missing required property '{key}'."));
    }

    private static string? ReadRequiredString(JsonElement root, string key, string file, List<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty(key, out var value)) return null;
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            issues.Add(new("INVALID_TYPE", file, "$." + key, $"{key} must be a non-empty string."));
            return null;
        }
        return value.GetString();
    }

    private static int? ReadRequiredInt(JsonElement root, string key, string file, List<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty(key, out var value)) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            issues.Add(new("INVALID_TYPE", file, "$." + key, $"{key} must be an integer."));
            return null;
        }
        return result;
    }
}
