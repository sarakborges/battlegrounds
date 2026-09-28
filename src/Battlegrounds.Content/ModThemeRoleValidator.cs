using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class ModThemeRoleValidator
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

        if (root.ValueKind != JsonValueKind.Object) return [];

        var issues = new List<ModValidationIssue>();
        ValidateComponents(root, issues);
        ValidateScreens(root, issues);
        return issues;
    }

    private static void ValidateComponents(JsonElement root, ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty("components", out var components) || components.ValueKind != JsonValueKind.Object)
            return;

        foreach (var pair in components.EnumerateObject())
        {
            if (ModThemeRoleNames.IsSupportedComponent(pair.Name)) continue;
            issues.Add(new(
                "UNKNOWN_THEME_COMPONENT_ROLE",
                ThemeFile,
                "$.components." + pair.Name,
                $"Theme component role '{pair.Name}' is not supported by theme schema v1."));
        }
    }

    private static void ValidateScreens(JsonElement root, ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty("screens", out var screens) || screens.ValueKind != JsonValueKind.Object)
            return;

        foreach (var pair in screens.EnumerateObject())
        {
            if (ModThemeRoleNames.IsSupportedScreen(pair.Name)) continue;
            issues.Add(new(
                "UNKNOWN_THEME_SCREEN_ROLE",
                ThemeFile,
                "$.screens." + pair.Name,
                $"Theme screen role '{pair.Name}' is not supported by theme schema v1."));
        }
    }
}
