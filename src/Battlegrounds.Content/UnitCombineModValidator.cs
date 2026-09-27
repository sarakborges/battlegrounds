using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class UnitCombineModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> AllowedKeys =
    [
        "id", "name", "sourceUnitId", "requiredCopies", "resultUnitId",
    ];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var relativeDirectory = Path.Combine("content", "combines");
        var directory = Path.Combine(modDirectory, relativeDirectory);
        if (!Directory.Exists(directory))
        {
            issues.Add(new("MISSING_REQUIRED_PATH", "content/combines", "$", "Missing content/combines directory."));
            return issues;
        }

        var unitIds = ReadUnitIds(modDirectory);
        var combineIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(value => Path.GetFileName(value), StringComparer.Ordinal))
        {
            var file = Path.GetRelativePath(modDirectory, path).Replace('\\', '/');
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            }
            catch (JsonException exception)
            {
                issues.Add(new("INVALID_JSON", file, "$", exception.Message));
                continue;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new("INVALID_TYPE", file, "$", "Expected an object."));
                    continue;
                }

                foreach (var property in root.EnumerateObject())
                    if (!AllowedKeys.Contains(property.Name))
                        issues.Add(new("UNKNOWN_KEY", file, "$." + property.Name, $"Unknown key '{property.Name}'."));

                var id = ReadRequiredString(root, "id", file, issues);
                _ = ReadRequiredString(root, "name", file, issues);
                var sourceUnitId = ReadRequiredString(root, "sourceUnitId", file, issues);
                var resultUnitId = ReadRequiredString(root, "resultUnitId", file, issues);

                if (id is not null)
                {
                    var expectedId = Path.GetFileNameWithoutExtension(path);
                    if (!string.Equals(id, expectedId, StringComparison.Ordinal))
                        issues.Add(new("ID_FILENAME_MISMATCH", file, "$.id", $"Entity id '{id}' must match file name '{expectedId}.json'."));
                    if (!combineIds.Add(id))
                        issues.Add(new("DUPLICATE_ID", file, "$.id", $"Duplicate unit combine id '{id}'."));
                }

                if (!root.TryGetProperty("requiredCopies", out var requiredCopies))
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file, "$.requiredCopies", "requiredCopies is required."));
                else if (requiredCopies.ValueKind != JsonValueKind.Number || !requiredCopies.TryGetInt32(out var count) || count < 2)
                    issues.Add(new("INVALID_VALUE", file, "$.requiredCopies", "requiredCopies must be an integer of at least 2."));

                if (sourceUnitId is not null && !unitIds.Contains(sourceUnitId))
                    issues.Add(new("UNKNOWN_REFERENCE", file, "$.sourceUnitId", $"Unknown unit '{sourceUnitId}'."));
                if (resultUnitId is not null && !unitIds.Contains(resultUnitId))
                    issues.Add(new("UNKNOWN_REFERENCE", file, "$.resultUnitId", $"Unknown unit '{resultUnitId}'."));
            }
        }

        return issues;
    }

    private static string? ReadRequiredString(
        JsonElement root,
        string property,
        string file,
        List<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty(property, out var value))
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, "$." + property, property + " is required."));
            return null;
        }
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            issues.Add(new("INVALID_VALUE", file, "$." + property, property + " must be a non-empty string."));
            return null;
        }
        return value.GetString();
    }

    private static HashSet<string> ReadUnitIds(string root)
    {
        var directory = Path.Combine(root, "content", "units");
        if (!Directory.Exists(directory)) return [];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
                if (document.RootElement.ValueKind == JsonValueKind.Object &&
                    document.RootElement.TryGetProperty("id", out var id) &&
                    id.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(id.GetString()))
                    ids.Add(id.GetString()!);
            }
            catch (JsonException)
            {
            }
        }
        return ids;
    }
}
