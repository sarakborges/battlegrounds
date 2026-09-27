using System.Text.Json;
using System.Text.RegularExpressions;

namespace Battlegrounds.Content;

internal sealed partial class ModThemeValidator
{
    private const string ThemeFile = "presentation/theme.json";
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> RootKeys = ["version", "colors", "typography", "spacing", "shape", "components", "screens"];
    private static readonly HashSet<string> TypographyKeys = ["fonts", "sizes"];
    private static readonly HashSet<string> FontEntryKeys = ["asset"];
    private static readonly HashSet<string> PaddingKeys = ["horizontal", "vertical"];
    private static readonly HashSet<string> SliceKeys = ["left", "top", "right", "bottom"];
    private static readonly HashSet<string> StyleKeys = [
        "font", "fontSize", "textColor", "backgroundColor", "borderColor", "borderWidth", "radius",
        "padding", "backgroundAsset", "slice", "opacity", "states"
    ];
    private static readonly HashSet<string> StateStyleKeys = [
        "font", "fontSize", "textColor", "backgroundColor", "borderColor", "borderWidth", "radius",
        "padding", "backgroundAsset", "slice", "opacity"
    ];
    private static readonly HashSet<string> ScreenKeys = ["backgroundColor", "backgroundAsset"];
    private static readonly HashSet<string> StateNames = ["normal", "hover", "pressed", "disabled", "focus"];
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".webp", ".svg" };
    private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase) { ".ttf", ".otf", ".woff", ".woff2" };

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return [];
        var fullPath = Path.Combine(modDirectory, "presentation", "theme.json");
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
            issues.Add(new("INVALID_JSON", ThemeFile, exception.Path ?? "$", "File contains invalid JSON."));
            return issues;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, "$", "Theme must contain one JSON object."));
            return issues;
        }

        ValidateUnknownKeys(root, RootKeys, "$", issues);
        ValidateVersion(root, issues);

        var colors = ValidateStringMap(root, "colors", issues, ValidateColorLiteral);
        var spacing = ValidateIntMap(root, "spacing", min: 0, max: 512, issues);
        var radii = ValidateIntMap(root, "shape", min: 0, max: 256, issues);
        var fonts = new HashSet<string>(StringComparer.Ordinal);
        var sizes = new HashSet<string>(StringComparer.Ordinal);
        ValidateTypography(root, modDirectory, fonts, sizes, issues);
        ValidateComponents(root, modDirectory, colors, fonts, sizes, spacing, radii, issues);
        ValidateScreens(root, modDirectory, colors, issues);
        return issues;
    }

    private static void ValidateVersion(JsonElement root, ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty("version", out var version))
        {
            issues.Add(new("MISSING_REQUIRED_KEY", ThemeFile, "$.version", "Theme version is required."));
            return;
        }
        if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var value))
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, "$.version", "Theme version must be an integer."));
            return;
        }
        if (value != 1) issues.Add(new("UNSUPPORTED_SCHEMA_VERSION", ThemeFile, "$.version", $"Unsupported theme version {value}."));
    }

    private static HashSet<string> ValidateStringMap(
        JsonElement root,
        string property,
        ICollection<ModValidationIssue> issues,
        Func<string, bool>? valueValidator = null)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(property, out var map)) return keys;
        if (map.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, "$." + property, "Expected an object."));
            return keys;
        }
        foreach (var pair in map.EnumerateObject())
        {
            keys.Add(pair.Name);
            if (pair.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(pair.Value.GetString()))
            {
                issues.Add(new("INVALID_VALUE", ThemeFile, $"$.{property}.{pair.Name}", "Expected a non-empty string."));
                continue;
            }
            var value = pair.Value.GetString()!;
            if (valueValidator is not null && !valueValidator(value))
                issues.Add(new("INVALID_VALUE", ThemeFile, $"$.{property}.{pair.Name}", $"'{value}' is not a supported color literal."));
        }
        return keys;
    }

    private static HashSet<string> ValidateIntMap(
        JsonElement root,
        string property,
        int min,
        int max,
        ICollection<ModValidationIssue> issues)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        if (!root.TryGetProperty(property, out var map)) return keys;
        if (map.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, "$." + property, "Expected an object."));
            return keys;
        }
        foreach (var pair in map.EnumerateObject())
        {
            keys.Add(pair.Name);
            if (pair.Value.ValueKind != JsonValueKind.Number || !pair.Value.TryGetInt32(out var value) || value < min || value > max)
                issues.Add(new("INVALID_VALUE", ThemeFile, $"$.{property}.{pair.Name}", $"Expected an integer from {min} through {max}."));
        }
        return keys;
    }

    private static void ValidateTypography(
        JsonElement root,
        string modDirectory,
        ISet<string> fonts,
        ISet<string> sizes,
        ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty("typography", out var typography)) return;
        if (typography.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, "$.typography", "Expected an object."));
            return;
        }
        ValidateUnknownKeys(typography, TypographyKeys, "$.typography", issues);

        if (typography.TryGetProperty("fonts", out var fontMap))
        {
            if (fontMap.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", ThemeFile, "$.typography.fonts", "Expected an object."));
            }
            else
            {
                foreach (var pair in fontMap.EnumerateObject())
                {
                    fonts.Add(pair.Name);
                    var path = $"$.typography.fonts.{pair.Name}";
                    if (pair.Value.ValueKind != JsonValueKind.Object)
                    {
                        issues.Add(new("INVALID_TYPE", ThemeFile, path, "Font entry must be an object."));
                        continue;
                    }
                    ValidateUnknownKeys(pair.Value, FontEntryKeys, path, issues);
                    if (!pair.Value.TryGetProperty("asset", out var asset) || asset.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(asset.GetString()))
                    {
                        issues.Add(new("MISSING_REQUIRED_KEY", ThemeFile, path + ".asset", "Font asset is required."));
                        continue;
                    }
                    ValidateAssetPath(modDirectory, asset.GetString()!, FontExtensions, path + ".asset", issues);
                }
            }
        }

        if (typography.TryGetProperty("sizes", out var sizeMap))
        {
            if (sizeMap.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", ThemeFile, "$.typography.sizes", "Expected an object."));
            }
            else
            {
                foreach (var pair in sizeMap.EnumerateObject())
                {
                    sizes.Add(pair.Name);
                    if (pair.Value.ValueKind != JsonValueKind.Number || !pair.Value.TryGetInt32(out var value) || value < 1 || value > 256)
                        issues.Add(new("INVALID_VALUE", ThemeFile, $"$.typography.sizes.{pair.Name}", "Font size must be an integer from 1 through 256."));
                }
            }
        }
    }

    private static void ValidateComponents(
        JsonElement root,
        string modDirectory,
        ISet<string> colors,
        ISet<string> fonts,
        ISet<string> sizes,
        ISet<string> spacing,
        ISet<string> radii,
        ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty("components", out var components)) return;
        if (components.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, "$.components", "Expected an object."));
            return;
        }
        foreach (var pair in components.EnumerateObject())
        {
            var path = $"$.components.{pair.Name}";
            if (pair.Value.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", ThemeFile, path, "Component style must be an object."));
                continue;
            }
            ValidateStyle(pair.Value, path, allowStates: true, modDirectory, colors, fonts, sizes, spacing, radii, issues);
        }
    }

    private static void ValidateStyle(
        JsonElement style,
        string path,
        bool allowStates,
        string modDirectory,
        ISet<string> colors,
        ISet<string> fonts,
        ISet<string> sizes,
        ISet<string> spacing,
        ISet<string> radii,
        ICollection<ModValidationIssue> issues)
    {
        ValidateUnknownKeys(style, allowStates ? StyleKeys : StateStyleKeys, path, issues);
        ValidateReference(style, "font", fonts, path, issues);
        ValidateReference(style, "fontSize", sizes, path, issues);
        ValidateColorReference(style, "textColor", colors, path, issues);
        ValidateColorReference(style, "backgroundColor", colors, path, issues);
        ValidateColorReference(style, "borderColor", colors, path, issues);
        ValidateReference(style, "radius", radii, path, issues);

        if (style.TryGetProperty("borderWidth", out var borderWidth) &&
            (borderWidth.ValueKind != JsonValueKind.Number || !borderWidth.TryGetInt32(out var width) || width < 0 || width > 64))
            issues.Add(new("INVALID_VALUE", ThemeFile, path + ".borderWidth", "borderWidth must be an integer from 0 through 64."));

        if (style.TryGetProperty("opacity", out var opacity) &&
            (opacity.ValueKind != JsonValueKind.Number || !opacity.TryGetDouble(out var alpha) || alpha < 0 || alpha > 1))
            issues.Add(new("INVALID_VALUE", ThemeFile, path + ".opacity", "opacity must be between 0 and 1."));

        if (style.TryGetProperty("padding", out var padding))
        {
            if (padding.ValueKind != JsonValueKind.Object)
                issues.Add(new("INVALID_TYPE", ThemeFile, path + ".padding", "padding must be an object."));
            else
            {
                ValidateUnknownKeys(padding, PaddingKeys, path + ".padding", issues);
                ValidateRequiredReference(padding, "horizontal", spacing, path + ".padding", issues);
                ValidateRequiredReference(padding, "vertical", spacing, path + ".padding", issues);
            }
        }

        if (style.TryGetProperty("backgroundAsset", out var backgroundAsset))
        {
            if (backgroundAsset.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(backgroundAsset.GetString()))
                issues.Add(new("INVALID_VALUE", ThemeFile, path + ".backgroundAsset", "backgroundAsset must be a non-empty string."));
            else
                ValidateAssetPath(modDirectory, backgroundAsset.GetString()!, ImageExtensions, path + ".backgroundAsset", issues);
        }

        if (style.TryGetProperty("slice", out var slice)) ValidateSlice(slice, path + ".slice", issues);

        if (!allowStates || !style.TryGetProperty("states", out var states)) return;
        if (states.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, path + ".states", "states must be an object."));
            return;
        }
        foreach (var state in states.EnumerateObject())
        {
            var statePath = path + ".states." + state.Name;
            if (!StateNames.Contains(state.Name))
                issues.Add(new("INVALID_VALUE", ThemeFile, statePath, $"Unsupported component state '{state.Name}'."));
            if (state.Value.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", ThemeFile, statePath, "State style must be an object."));
                continue;
            }
            ValidateStyle(state.Value, statePath, allowStates: false, modDirectory, colors, fonts, sizes, spacing, radii, issues);
        }
    }

    private static void ValidateScreens(
        JsonElement root,
        string modDirectory,
        ISet<string> colors,
        ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty("screens", out var screens)) return;
        if (screens.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, "$.screens", "Expected an object."));
            return;
        }
        foreach (var pair in screens.EnumerateObject())
        {
            var path = "$.screens." + pair.Name;
            if (pair.Value.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", ThemeFile, path, "Screen style must be an object."));
                continue;
            }
            ValidateUnknownKeys(pair.Value, ScreenKeys, path, issues);
            ValidateColorReference(pair.Value, "backgroundColor", colors, path, issues);
            if (pair.Value.TryGetProperty("backgroundAsset", out var asset))
            {
                if (asset.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(asset.GetString()))
                    issues.Add(new("INVALID_VALUE", ThemeFile, path + ".backgroundAsset", "backgroundAsset must be a non-empty string."));
                else
                    ValidateAssetPath(modDirectory, asset.GetString()!, ImageExtensions, path + ".backgroundAsset", issues);
            }
        }
    }

    private static void ValidateReference(JsonElement root, string property, ISet<string> valid, string path, ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty(property, out var element)) return;
        ValidateReferenceValue(element, property, valid, path, issues);
    }

    private static void ValidateRequiredReference(JsonElement root, string property, ISet<string> valid, string path, ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty(property, out var element))
        {
            issues.Add(new("MISSING_REQUIRED_KEY", ThemeFile, path + "." + property, $"{property} is required."));
            return;
        }
        ValidateReferenceValue(element, property, valid, path, issues);
    }

    private static void ValidateReferenceValue(JsonElement element, string property, ISet<string> valid, string path, ICollection<ModValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            issues.Add(new("INVALID_VALUE", ThemeFile, path + "." + property, $"{property} must be a non-empty token name."));
            return;
        }
        var value = element.GetString()!;
        if (!valid.Contains(value)) issues.Add(new("UNKNOWN_THEME_TOKEN", ThemeFile, path + "." + property, $"Theme token '{value}' is not defined."));
    }

    private static void ValidateColorReference(JsonElement root, string property, ISet<string> colors, string path, ICollection<ModValidationIssue> issues)
    {
        if (!root.TryGetProperty(property, out var element)) return;
        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            issues.Add(new("INVALID_VALUE", ThemeFile, path + "." + property, $"{property} must be a color token or literal."));
            return;
        }
        var value = element.GetString()!;
        if (!value.StartsWith('#') && !colors.Contains(value))
            issues.Add(new("UNKNOWN_THEME_TOKEN", ThemeFile, path + "." + property, $"Color token '{value}' is not defined."));
        else if (value.StartsWith('#') && !ValidateColorLiteral(value))
            issues.Add(new("INVALID_VALUE", ThemeFile, path + "." + property, $"'{value}' is not a supported color literal."));
    }

    private static void ValidateSlice(JsonElement slice, string path, ICollection<ModValidationIssue> issues)
    {
        if (slice.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", ThemeFile, path, "slice must be an object."));
            return;
        }
        ValidateUnknownKeys(slice, SliceKeys, path, issues);
        foreach (var side in new[] { "left", "top", "right", "bottom" })
        {
            if (!slice.TryGetProperty(side, out var element))
            {
                issues.Add(new("MISSING_REQUIRED_KEY", ThemeFile, path + "." + side, $"slice.{side} is required."));
                continue;
            }
            if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value) || value < 0 || value > 4096)
                issues.Add(new("INVALID_VALUE", ThemeFile, path + "." + side, $"slice.{side} must be an integer from 0 through 4096."));
        }
    }

    private static void ValidateAssetPath(
        string modDirectory,
        string relativePath,
        ISet<string> allowedExtensions,
        string jsonPath,
        ICollection<ModValidationIssue> issues)
    {
        if (Path.IsPathRooted(relativePath))
        {
            issues.Add(new("INVALID_ASSET_PATH", ThemeFile, jsonPath, "Theme asset paths must be relative to the mod directory."));
            return;
        }

        var root = Path.GetFullPath(modDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(modDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(root, StringComparison.Ordinal))
        {
            issues.Add(new("ASSET_PATH_ESCAPE", ThemeFile, jsonPath, "Theme asset path must stay inside the mod directory."));
            return;
        }
        if (!allowedExtensions.Contains(Path.GetExtension(fullPath)))
        {
            issues.Add(new("UNSUPPORTED_ASSET_TYPE", ThemeFile, jsonPath, $"Unsupported theme asset type '{Path.GetExtension(fullPath)}'."));
            return;
        }
        if (!File.Exists(fullPath)) issues.Add(new("MISSING_ASSET", ThemeFile, jsonPath, $"Theme asset '{relativePath}' does not exist."));
    }

    private static void ValidateUnknownKeys(JsonElement root, ISet<string> allowed, string path, ICollection<ModValidationIssue> issues)
    {
        foreach (var pair in root.EnumerateObject())
        {
            if (!allowed.Contains(pair.Name))
                issues.Add(new("UNKNOWN_KEY", ThemeFile, path + "." + pair.Name, $"Unknown theme property '{pair.Name}'."));
        }
    }

    private static bool ValidateColorLiteral(string value) => ColorLiteralRegex().IsMatch(value);

    [GeneratedRegex("^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")]
    private static partial Regex ColorLiteralRegex();
}
