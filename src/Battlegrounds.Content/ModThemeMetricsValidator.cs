using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class ModThemeMetricsValidator
{
    private const string ThemeFile = "presentation/theme.json";
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return [];
        var fullPath = Path.Combine(modDirectory, "presentation", "theme.json");
        if (!File.Exists(fullPath)) return [];

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return [];
        }

        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("metrics", out var metrics))
            return [];

        var issues = new List<ModValidationIssue>();
        if (metrics.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, "$.metrics", "Theme metrics must be an object."));
            return issues;
        }

        foreach (var pair in metrics.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(pair.Name))
            {
                issues.Add(new("INVALID_VALUE", ThemeFile, "$.metrics", "Theme metric names cannot be empty."));
                continue;
            }

            if (pair.Value.ValueKind != JsonValueKind.Number ||
                !pair.Value.TryGetDouble(out var value) ||
                !double.IsFinite(value) ||
                value < -8192 ||
                value > 8192)
            {
                issues.Add(new(
                    "INVALID_VALUE",
                    ThemeFile,
                    "$.metrics." + pair.Name,
                    "Theme metric must be a finite number from -8192 through 8192."));
            }
        }

        return issues;
    }
}
