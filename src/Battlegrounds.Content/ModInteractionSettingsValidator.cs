using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class ModInteractionSettingsValidator
{
    private const string InteractionFile = "presentation/interaction.json";
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };
    private static readonly HashSet<string> RootKeys = ["version", "combineMode"];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return [];
        var fullPath = Path.Combine(modDirectory, "presentation", "interaction.json");
        if (!File.Exists(fullPath)) return [];

        var issues = new List<ModValidationIssue>();
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            issues.Add(new("INVALID_JSON", InteractionFile, exception.Path ?? "$", "File contains invalid JSON."));
            return issues;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", InteractionFile, "$", "Interaction settings must contain one JSON object."));
            return issues;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!RootKeys.Contains(property.Name))
                issues.Add(new("UNKNOWN_KEY", InteractionFile, "$." + property.Name, $"Unknown interaction setting '{property.Name}'."));
        }

        if (!root.TryGetProperty("version", out var version))
        {
            issues.Add(new("MISSING_REQUIRED_KEY", InteractionFile, "$.version", "Interaction settings version is required."));
        }
        else if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var versionValue))
        {
            issues.Add(new("INVALID_TYPE", InteractionFile, "$.version", "Interaction settings version must be an integer."));
        }
        else if (versionValue != 1)
        {
            issues.Add(new("UNSUPPORTED_SCHEMA_VERSION", InteractionFile, "$.version", $"Unsupported interaction settings version {versionValue}."));
        }

        if (root.TryGetProperty("combineMode", out var combineMode))
        {
            if (combineMode.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(combineMode.GetString()))
            {
                issues.Add(new("INVALID_TYPE", InteractionFile, "$.combineMode", "combineMode must be a non-empty string."));
            }
            else if (combineMode.GetString() is not ("manual" or "automatic"))
            {
                issues.Add(new("INVALID_VALUE", InteractionFile, "$.combineMode", "combineMode must be 'manual' or 'automatic'."));
            }
        }

        return issues;
    }
}
