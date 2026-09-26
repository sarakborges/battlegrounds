using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class LeaderSelectionModValidator
{
    private const string SetupFile = "rules/setup.json";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public IReadOnlyList<ModValidationIssue> Validate(
        string modDirectory,
        ModValidationReport baseReport)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory))
        {
            return issues;
        }

        var setup = ReadObject(modDirectory, SetupFile, issues);
        if (setup is null)
        {
            return issues;
        }

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "leaderOfferSize",
            "leaderOfferPolicy",
        };
        var present = setup.Value.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var key in present.Where(key => !allowed.Contains(key)).OrderBy(key => key, StringComparer.Ordinal))
        {
            issues.Add(new("UNKNOWN_KEY", SetupFile, "$." + key, $"Unknown key '{key}'."));
        }

        foreach (var key in allowed.Where(key => !present.Contains(key)).OrderBy(key => key, StringComparer.Ordinal))
        {
            issues.Add(new("MISSING_REQUIRED_KEY", SetupFile, "$." + key, $"Required key '{key}' is missing."));
        }

        var offerSize = ReadPositiveInt(setup.Value, "leaderOfferSize", issues);
        var offerPolicy = ReadPolicy(setup.Value, issues);
        if (offerSize is null || offerPolicy is null)
        {
            return issues;
        }

        if (baseReport.Issues.Any(issue => issue.File.StartsWith("content/leaders", StringComparison.Ordinal)))
        {
            return issues;
        }

        var leaderDirectory = Path.Combine(modDirectory, "content", "leaders");
        if (!Directory.Exists(leaderDirectory))
        {
            return issues;
        }

        var leaderCount = Directory.GetFiles(leaderDirectory, "*.json", SearchOption.TopDirectoryOnly).Length;
        var required = offerPolicy == "uniqueAcrossMatch"
            ? GetMaximumPlayers(modDirectory) is int maximumPlayers
                ? checked(maximumPlayers * offerSize.Value)
                : (int?)null
            : offerSize.Value;

        if (required is not null && leaderCount < required.Value)
        {
            issues.Add(new(
                "INSUFFICIENT_LEADERS_FOR_OFFERS",
                SetupFile,
                "$.leaderOfferSize",
                $"Leader offer policy '{offerPolicy}' requires at least {required.Value} leaders, but the mod defines {leaderCount}."));
        }

        return issues;
    }

    private static JsonElement? ReadObject(
        string root,
        string relativePath,
        ICollection<ModValidationIssue> issues)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            issues.Add(new("MISSING_REQUIRED_FILE", relativePath, "$", $"Required file '{relativePath}' is missing."));
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", relativePath, "$", "Expected an object."));
                return null;
            }

            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            issues.Add(new("INVALID_JSON", relativePath, exception.Path ?? "$", "File contains invalid JSON."));
            return null;
        }
    }

    private static int? ReadPositiveInt(
        JsonElement root,
        string property,
        ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty(property, out var element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
        {
            issues.Add(new("INVALID_TYPE", SetupFile, "$." + property, "Expected an integer."));
            return null;
        }

        if (value <= 0)
        {
            issues.Add(new("INVALID_VALUE", SetupFile, "$." + property, property + " must be positive."));
            return null;
        }

        return value;
    }

    private static string? ReadPolicy(
        JsonElement root,
        ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty("leaderOfferPolicy", out var element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            issues.Add(new("INVALID_TYPE", SetupFile, "$.leaderOfferPolicy", "Expected a non-empty string."));
            return null;
        }

        var value = element.GetString()!;
        if (value is not ("independentPerPlayer" or "uniqueAcrossMatch"))
        {
            issues.Add(new("INVALID_VALUE", SetupFile, "$.leaderOfferPolicy", $"Unknown leaderOfferPolicy '{value}'."));
            return null;
        }

        return value;
    }

    private static int? GetMaximumPlayers(string modDirectory)
    {
        var path = Path.Combine(modDirectory, "rules", "match.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("maximumPlayers", out var maximum) ||
                maximum.ValueKind != JsonValueKind.Number ||
                !maximum.TryGetInt32(out var value) ||
                value <= 0)
            {
                return null;
            }

            return value;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
