using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class PresentationAssetModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".svg",
    };

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var manifestPath = Path.Combine(modDirectory, "assets", "presentation.json");
        if (!File.Exists(manifestPath)) return issues;

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath), DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", "assets/presentation.json", "$", "Expected an object."));
                return issues;
            }
            root = document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            issues.Add(new("INVALID_JSON", "assets/presentation.json", exception.Path ?? "$", "File contains invalid JSON."));
            return issues;
        }

        var knownCategories = new HashSet<string>(StringComparer.Ordinal) { "leaders", "units", "actions" };
        foreach (var pair in root.EnumerateObject())
        {
            if (!knownCategories.Contains(pair.Name))
                issues.Add(new("UNKNOWN_KEY", "assets/presentation.json", "$." + pair.Name, $"Unknown presentation asset category '{pair.Name}'."));
        }

        ValidateCategory(modDirectory, root, "leaders", "content/leaders", ModPresentationAssetSlots.Portrait, issues);
        ValidateCategory(modDirectory, root, "units", "content/units", ModPresentationAssetSlots.Art, issues);
        ValidateCategory(modDirectory, root, "actions", "content/actions", ModPresentationAssetSlots.Art, issues);
        return issues;
    }

    private static void ValidateCategory(
        string modDirectory,
        JsonElement root,
        string category,
        string contentDirectory,
        string expectedSlot,
        ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty(category, out var categoryElement)) return;
        var categoryPath = "$." + category;
        if (categoryElement.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", "assets/presentation.json", categoryPath, $"'{category}' must be an object keyed by stable entity id."));
            return;
        }

        foreach (var entity in categoryElement.EnumerateObject())
        {
            var entityPath = categoryPath + "." + entity.Name;
            var authoredEntityPath = Path.Combine(modDirectory, contentDirectory.Replace('/', Path.DirectorySeparatorChar), entity.Name + ".json");
            if (!File.Exists(authoredEntityPath))
            {
                issues.Add(new(
                    "UNKNOWN_PRESENTATION_ASSET_REFERENCE",
                    "assets/presentation.json",
                    entityPath,
                    $"Presentation assets reference unknown {category} id '{entity.Name}'."));
            }

            if (entity.Value.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", "assets/presentation.json", entityPath, "Entity presentation metadata must be an object."));
                continue;
            }

            foreach (var slot in entity.Value.EnumerateObject())
            {
                var slotPath = entityPath + "." + slot.Name;
                if (!string.Equals(slot.Name, expectedSlot, StringComparison.Ordinal))
                {
                    issues.Add(new(
                        "UNKNOWN_PRESENTATION_ASSET_SLOT",
                        "assets/presentation.json",
                        slotPath,
                        $"Unknown presentation asset slot '{slot.Name}' for category '{category}'."));
                    continue;
                }

                ValidateImagePath(modDirectory, slot.Value, slotPath, issues);
            }
        }
    }

    private static void ValidateImagePath(
        string modDirectory,
        JsonElement value,
        string jsonPath,
        ICollection<ModValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            issues.Add(new(
                "INVALID_PRESENTATION_ASSET_PATH",
                "assets/presentation.json",
                jsonPath,
                "Presentation asset path must be a non-empty mod-relative string."));
            return;
        }

        var relativePath = value.GetString()!;
        if (!TryResolveInsideMod(modDirectory, relativePath, out var fullPath))
        {
            issues.Add(new(
                "INVALID_PRESENTATION_ASSET_PATH",
                "assets/presentation.json",
                jsonPath,
                $"Presentation asset path '{relativePath}' must stay inside the mod and use a forward-slash relative path under assets/."));
            return;
        }

        if (!ImageExtensions.Contains(Path.GetExtension(relativePath)))
        {
            issues.Add(new(
                "INVALID_PRESENTATION_ASSET_TYPE",
                "assets/presentation.json",
                jsonPath,
                $"Presentation image '{relativePath}' must use one of: {string.Join(", ", ImageExtensions.OrderBy(value => value, StringComparer.Ordinal))}."));
            return;
        }

        if (!File.Exists(fullPath))
        {
            issues.Add(new(
                "MISSING_PRESENTATION_ASSET",
                "assets/presentation.json",
                jsonPath,
                $"Presentation asset '{relativePath}' does not exist."));
        }
    }

    private static bool TryResolveInsideMod(string modDirectory, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (Path.IsPathRooted(relativePath) || relativePath.Contains('\\', StringComparison.Ordinal)) return false;
        if (!relativePath.StartsWith("assets/", StringComparison.Ordinal)) return false;

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment is "." or "..")) return false;

        try
        {
            var root = Path.GetFullPath(modDirectory);
            var candidate = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(rootWithSeparator, StringComparison.Ordinal) &&
                !string.Equals(candidate, root, StringComparison.Ordinal))
                return false;
            fullPath = candidate;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
