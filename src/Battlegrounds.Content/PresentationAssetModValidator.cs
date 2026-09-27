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

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav",
    };

    private static readonly HashSet<string> AnimationNames = new(StringComparer.Ordinal)
    {
        "none", "pulse", "shake", "lunge", "fade", "pop",
    };

    private static readonly IReadOnlyDictionary<string, HashSet<string>> CueRolesByCategory =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["leaders"] = new(StringComparer.Ordinal)
            {
                ModPresentationCueRoles.UiSelect,
            },
            ["units"] = new(StringComparer.Ordinal)
            {
                ModPresentationCueRoles.UiSelect,
                ModPresentationCueRoles.UiAcquire,
                ModPresentationCueRoles.UiDeploy,
                ModPresentationCueRoles.UiRelease,
                ModPresentationCueRoles.CombatAttack,
                ModPresentationCueRoles.CombatTarget,
                ModPresentationCueRoles.CombatSummon,
                ModPresentationCueRoles.CombatStats,
                ModPresentationCueRoles.CombatDamage,
                ModPresentationCueRoles.CombatDestroy,
                ModPresentationCueRoles.CombatDeath,
                ModPresentationCueRoles.CombatRevive,
                ModPresentationCueRoles.CombatTrigger,
                ModPresentationCueRoles.CombatBehavior,
            },
            ["actions"] = new(StringComparer.Ordinal)
            {
                ModPresentationCueRoles.UiSelect,
                ModPresentationCueRoles.UiAcquire,
                ModPresentationCueRoles.UiPlay,
            },
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
                    $"Presentation metadata references unknown {category} id '{entity.Name}'."));
            }

            if (entity.Value.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", "assets/presentation.json", entityPath, "Entity presentation metadata must be an object."));
                continue;
            }

            foreach (var property in entity.Value.EnumerateObject())
            {
                var propertyPath = entityPath + "." + property.Name;
                if (string.Equals(property.Name, expectedSlot, StringComparison.Ordinal))
                {
                    ValidateAssetPath(modDirectory, property.Value, propertyPath, ModPresentationAssetType.Image, ImageExtensions, issues);
                    continue;
                }

                if (string.Equals(property.Name, "cues", StringComparison.Ordinal))
                {
                    ValidateCues(modDirectory, category, property.Value, propertyPath, issues);
                    continue;
                }

                issues.Add(new(
                    "UNKNOWN_PRESENTATION_ASSET_SLOT",
                    "assets/presentation.json",
                    propertyPath,
                    $"Unknown presentation metadata slot '{property.Name}' for category '{category}'."));
            }
        }
    }

    private static void ValidateCues(
        string modDirectory,
        string category,
        JsonElement cues,
        string jsonPath,
        ICollection<ModValidationIssue> issues)
    {
        if (cues.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", "assets/presentation.json", jsonPath, "'cues' must be an object keyed by presentation event role."));
            return;
        }

        var validRoles = CueRolesByCategory[category];
        foreach (var cueProperty in cues.EnumerateObject())
        {
            var cuePath = jsonPath + "." + cueProperty.Name;
            if (!validRoles.Contains(cueProperty.Name))
            {
                issues.Add(new(
                    "UNKNOWN_PRESENTATION_CUE_ROLE",
                    "assets/presentation.json",
                    cuePath,
                    $"Presentation cue role '{cueProperty.Name}' is not supported for category '{category}'."));
            }

            if (cueProperty.Value.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", "assets/presentation.json", cuePath, "Presentation cue metadata must be an object."));
                continue;
            }

            var hasAnimation = false;
            var hasAudio = false;
            foreach (var property in cueProperty.Value.EnumerateObject())
            {
                var propertyPath = cuePath + "." + property.Name;
                switch (property.Name)
                {
                    case "animation":
                        hasAnimation = true;
                        if (property.Value.ValueKind != JsonValueKind.String ||
                            string.IsNullOrWhiteSpace(property.Value.GetString()) ||
                            !AnimationNames.Contains(property.Value.GetString()!))
                        {
                            issues.Add(new(
                                "INVALID_PRESENTATION_CUE_ANIMATION",
                                "assets/presentation.json",
                                propertyPath,
                                $"Animation must be one of: {string.Join(", ", AnimationNames.OrderBy(value => value, StringComparer.Ordinal))}."));
                        }
                        break;
                    case "durationSeconds":
                        if (property.Value.ValueKind != JsonValueKind.Number ||
                            !property.Value.TryGetDouble(out var duration) ||
                            !double.IsFinite(duration) || duration <= 0 || duration > 5)
                        {
                            issues.Add(new(
                                "INVALID_PRESENTATION_CUE_DURATION",
                                "assets/presentation.json",
                                propertyPath,
                                "Cue durationSeconds must be greater than 0 and at most 5 seconds."));
                        }
                        break;
                    case "audio":
                        hasAudio = true;
                        ValidateAssetPath(modDirectory, property.Value, propertyPath, ModPresentationAssetType.Audio, AudioExtensions, issues);
                        break;
                    default:
                        issues.Add(new("UNKNOWN_KEY", "assets/presentation.json", propertyPath, $"Unknown presentation cue key '{property.Name}'."));
                        break;
                }
            }

            if (!hasAnimation && !hasAudio)
            {
                issues.Add(new(
                    "EMPTY_PRESENTATION_CUE",
                    "assets/presentation.json",
                    cuePath,
                    "A presentation cue must define at least animation or audio."));
            }

            if (!hasAnimation && cueProperty.Value.TryGetProperty("durationSeconds", out _))
            {
                issues.Add(new(
                    "INVALID_PRESENTATION_CUE_DURATION",
                    "assets/presentation.json",
                    cuePath + ".durationSeconds",
                    "durationSeconds requires an animation cue."));
            }
        }
    }

    private static void ValidateAssetPath(
        string modDirectory,
        JsonElement value,
        string jsonPath,
        ModPresentationAssetType type,
        IReadOnlySet<string> extensions,
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

        if (!extensions.Contains(Path.GetExtension(relativePath)))
        {
            var media = type == ModPresentationAssetType.Image ? "image" : "audio";
            issues.Add(new(
                "INVALID_PRESENTATION_ASSET_TYPE",
                "assets/presentation.json",
                jsonPath,
                $"Presentation {media} '{relativePath}' must use one of: {string.Join(", ", extensions.OrderBy(item => item, StringComparer.Ordinal))}."));
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
