using System.Text.Json;
using System.Text.RegularExpressions;

namespace Battlegrounds.Content;

internal sealed class PresentationModValidator
{
    private static readonly Regex LocalePattern = new("^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant);
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        ValidateRequiredTerminology(modDirectory, issues);

        var localizationDirectory = Path.Combine(modDirectory, "localization");
        if (!Directory.Exists(localizationDirectory))
        {
            issues.Add(new(
                "MISSING_REQUIRED_DIRECTORY",
                "localization",
                "$",
                "Required directory 'localization' is missing."));
            return issues;
        }

        var presentationPath = Path.Combine(localizationDirectory, "presentation.json");
        var presentation = ReadObject(presentationPath, "localization/presentation.json", required: true, issues);
        string? defaultLocale = null;
        if (presentation is JsonElement presentationRoot)
        {
            ValidateExactKeys(presentationRoot, "localization/presentation.json", ["defaultLocale"], issues);
            if (presentationRoot.TryGetProperty("defaultLocale", out var localeElement) &&
                localeElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(localeElement.GetString()))
            {
                defaultLocale = localeElement.GetString();
                if (!LocalePattern.IsMatch(defaultLocale!))
                {
                    issues.Add(new(
                        "INVALID_LOCALE",
                        "localization/presentation.json",
                        "$.defaultLocale",
                        $"Locale '{defaultLocale}' is not a supported locale identifier."));
                }
            }
            else
            {
                issues.Add(new(
                    localeElement.ValueKind == JsonValueKind.Undefined ? "MISSING_REQUIRED_KEY" : "INVALID_VALUE",
                    "localization/presentation.json",
                    "$.defaultLocale",
                    "defaultLocale must be a non-empty locale string."));
            }
        }

        var localeFiles = Directory.GetFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFileName(path), "presentation.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToArray();

        var localeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localeContent = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in localeFiles)
        {
            var locale = Path.GetFileNameWithoutExtension(path);
            var relativePath = "localization/" + Path.GetFileName(path);
            if (!LocalePattern.IsMatch(locale))
            {
                issues.Add(new("INVALID_LOCALE", relativePath, "$", $"Locale filename '{locale}' is not a supported locale identifier."));
            }
            if (!localeNames.Add(locale))
            {
                issues.Add(new("DUPLICATE_LOCALE", relativePath, "$", $"Locale '{locale}' is defined more than once."));
                continue;
            }

            var root = ReadObject(path, relativePath, required: false, issues);
            if (root is null) continue;
            localeContent[locale] = root.Value;
            ValidateLocaleStrings(root.Value, relativePath, issues);
        }

        if (defaultLocale is not null && !localeContent.ContainsKey(defaultLocale))
        {
            issues.Add(new(
                "MISSING_DEFAULT_LOCALE",
                "localization/presentation.json",
                "$.defaultLocale",
                $"Default locale '{defaultLocale}' must have a matching localization/{defaultLocale}.json file."));
        }
        else if (defaultLocale is not null && localeContent.TryGetValue(defaultLocale, out var defaultStrings))
        {
            foreach (var key in ModPresentationKeys.RequiredDefaultStrings)
            {
                if (!defaultStrings.TryGetProperty(key, out var value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                {
                    issues.Add(new(
                        "MISSING_PRESENTATION_STRING",
                        $"localization/{defaultLocale}.json",
                        "$." + key,
                        $"Default locale must define presentation string '{key}'."));
                }
            }
        }

        return issues;
    }

    private static void ValidateRequiredTerminology(string modDirectory, ICollection<ModValidationIssue> issues)
    {
        var path = Path.Combine(modDirectory, "mod.json");
        if (!File.Exists(path)) return;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("terminology", out var terminology) ||
                terminology.ValueKind != JsonValueKind.Object)
                return;

            foreach (var key in ModPresentationKeys.RequiredTerminology)
            {
                if (!terminology.TryGetProperty(key, out var value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                {
                    issues.Add(new(
                        "MISSING_PRESENTATION_TERM",
                        "mod.json",
                        "$.terminology." + key,
                        $"Presentation terminology must define non-empty term '{key}'."));
                }
            }
        }
        catch (JsonException)
        {
            // The base directory validator owns invalid mod.json syntax/type reporting.
        }
    }

    private static void ValidateLocaleStrings(
        JsonElement root,
        string relativePath,
        ICollection<ModValidationIssue> issues)
    {
        foreach (var pair in root.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(pair.Name) ||
                pair.Value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(pair.Value.GetString()))
            {
                issues.Add(new(
                    "INVALID_PRESENTATION_STRING",
                    relativePath,
                    "$." + pair.Name,
                    "Localization keys and values must be non-empty strings."));
            }
        }
    }

    private static JsonElement? ReadObject(
        string fullPath,
        string relativePath,
        bool required,
        ICollection<ModValidationIssue> issues)
    {
        if (!File.Exists(fullPath))
        {
            if (required)
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

    private static void ValidateExactKeys(
        JsonElement root,
        string file,
        IReadOnlyCollection<string> required,
        ICollection<ModValidationIssue> issues)
    {
        var requiredSet = required.ToHashSet(StringComparer.Ordinal);
        foreach (var key in required)
        {
            if (!root.TryGetProperty(key, out _))
                issues.Add(new("MISSING_REQUIRED_KEY", file, "$." + key, $"Missing required key '{key}'."));
        }
        foreach (var pair in root.EnumerateObject())
        {
            if (!requiredSet.Contains(pair.Name))
                issues.Add(new("UNKNOWN_KEY", file, "$." + pair.Name, $"Unknown key '{pair.Name}'."));
        }
    }
}
