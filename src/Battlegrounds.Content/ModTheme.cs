using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Battlegrounds.Content;

public static class ModThemeComponentRoles
{
    public const string Button = "button";
    public const string ButtonPrimary = "button.primary";
    public const string Card = "card";
    public const string CardBoard = "card.board";
    public const string Input = "input";
    public const string Panel = "panel";
}

public static class ModThemeScreenRoles
{
    public const string Launcher = "launcher";
    public const string Preparation = "preparation";
    public const string Combat = "combat";
}

public sealed record ModThemeFont(string RelativePath);
public sealed record ModThemeSlice(int Left, int Top, int Right, int Bottom);
public sealed record ModThemePadding(string Horizontal, string Vertical);

public sealed class ModThemeStyle
{
    private readonly ReadOnlyDictionary<string, ModThemeStyle> _states;

    public string? Font { get; }
    public string? FontSize { get; }
    public string? TextColor { get; }
    public string? BackgroundColor { get; }
    public string? BorderColor { get; }
    public int? BorderWidth { get; }
    public string? Radius { get; }
    public ModThemePadding? Padding { get; }
    public string? BackgroundAsset { get; }
    public ModThemeSlice? Slice { get; }
    public double? Opacity { get; }
    public IReadOnlyDictionary<string, ModThemeStyle> States => _states;

    internal ModThemeStyle(
        string? font,
        string? fontSize,
        string? textColor,
        string? backgroundColor,
        string? borderColor,
        int? borderWidth,
        string? radius,
        ModThemePadding? padding,
        string? backgroundAsset,
        ModThemeSlice? slice,
        double? opacity,
        IReadOnlyDictionary<string, ModThemeStyle>? states = null)
    {
        Font = font;
        FontSize = fontSize;
        TextColor = textColor;
        BackgroundColor = backgroundColor;
        BorderColor = borderColor;
        BorderWidth = borderWidth;
        Radius = radius;
        Padding = padding;
        BackgroundAsset = backgroundAsset;
        Slice = slice;
        Opacity = opacity;
        _states = new ReadOnlyDictionary<string, ModThemeStyle>(
            new Dictionary<string, ModThemeStyle>(states ?? new Dictionary<string, ModThemeStyle>(), StringComparer.Ordinal));
    }
}

public sealed record ModThemeScreenStyle(string? BackgroundColor, string? BackgroundAsset);

public sealed partial class ModThemeCatalog
{
    private readonly ReadOnlyDictionary<string, string> _colors;
    private readonly ReadOnlyDictionary<string, ModThemeFont> _fonts;
    private readonly ReadOnlyDictionary<string, int> _fontSizes;
    private readonly ReadOnlyDictionary<string, int> _spacing;
    private readonly ReadOnlyDictionary<string, int> _radii;
    private readonly ReadOnlyDictionary<string, double> _metrics;
    private readonly ReadOnlyDictionary<string, ModThemeStyle> _components;
    private readonly ReadOnlyDictionary<string, ModThemeScreenStyle> _screens;

    public int Version { get; }
    public IReadOnlyDictionary<string, string> Colors => _colors;
    public IReadOnlyDictionary<string, ModThemeFont> Fonts => _fonts;
    public IReadOnlyDictionary<string, int> FontSizes => _fontSizes;
    public IReadOnlyDictionary<string, int> Spacing => _spacing;
    public IReadOnlyDictionary<string, int> Radii => _radii;
    public IReadOnlyDictionary<string, double> Metrics => _metrics;
    public IReadOnlyDictionary<string, ModThemeStyle> Components => _components;
    public IReadOnlyDictionary<string, ModThemeScreenStyle> Screens => _screens;
    public bool IsEmpty => _colors.Count == 0 && _fonts.Count == 0 && _fontSizes.Count == 0 && _spacing.Count == 0 && _radii.Count == 0 && _metrics.Count == 0 && _components.Count == 0 && _screens.Count == 0;

    public ModThemeCatalog(
        int version,
        IReadOnlyDictionary<string, string>? colors = null,
        IReadOnlyDictionary<string, ModThemeFont>? fonts = null,
        IReadOnlyDictionary<string, int>? fontSizes = null,
        IReadOnlyDictionary<string, int>? spacing = null,
        IReadOnlyDictionary<string, int>? radii = null,
        IReadOnlyDictionary<string, double>? metrics = null,
        IReadOnlyDictionary<string, ModThemeStyle>? components = null,
        IReadOnlyDictionary<string, ModThemeScreenStyle>? screens = null)
    {
        Version = version;
        _colors = Copy(colors);
        _fonts = Copy(fonts);
        _fontSizes = Copy(fontSizes);
        _spacing = Copy(spacing);
        _radii = Copy(radii);
        _metrics = Copy(metrics);
        _components = Copy(components);
        _screens = Copy(screens);
    }

    public bool TryResolveColor(string? tokenOrLiteral, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(tokenOrLiteral)) return false;
        if (tokenOrLiteral.StartsWith('#'))
        {
            value = tokenOrLiteral;
            return true;
        }

        return _colors.TryGetValue(tokenOrLiteral, out value!);
    }

    public static ModThemeCatalog Layer(ModThemeCatalog baseline, ModThemeCatalog overrides)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(overrides);

        return new ModThemeCatalog(
            Math.Max(baseline.Version, overrides.Version),
            Merge(baseline.Colors, overrides.Colors),
            Merge(baseline.Fonts, overrides.Fonts),
            Merge(baseline.FontSizes, overrides.FontSizes),
            Merge(baseline.Spacing, overrides.Spacing),
            Merge(baseline.Radii, overrides.Radii),
            Merge(baseline.Metrics, overrides.Metrics),
            MergeStyles(baseline.Components, overrides.Components),
            MergeScreens(baseline.Screens, overrides.Screens));
    }

    private static ReadOnlyDictionary<string, T> Copy<T>(IReadOnlyDictionary<string, T>? source) =>
        new(new Dictionary<string, T>(source ?? new Dictionary<string, T>(), StringComparer.Ordinal));

    private static IReadOnlyDictionary<string, T> Merge<T>(IReadOnlyDictionary<string, T> baseline, IReadOnlyDictionary<string, T> overrides)
    {
        var merged = new Dictionary<string, T>(baseline, StringComparer.Ordinal);
        foreach (var (key, value) in overrides) merged[key] = value;
        return merged;
    }

    private static IReadOnlyDictionary<string, ModThemeStyle> MergeStyles(
        IReadOnlyDictionary<string, ModThemeStyle> baseline,
        IReadOnlyDictionary<string, ModThemeStyle> overrides)
    {
        var merged = new Dictionary<string, ModThemeStyle>(baseline, StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
        {
            merged[key] = merged.TryGetValue(key, out var inherited)
                ? MergeStyle(inherited, value)
                : value;
        }
        return merged;
    }

    private static IReadOnlyDictionary<string, ModThemeScreenStyle> MergeScreens(
        IReadOnlyDictionary<string, ModThemeScreenStyle> baseline,
        IReadOnlyDictionary<string, ModThemeScreenStyle> overrides)
    {
        var merged = new Dictionary<string, ModThemeScreenStyle>(baseline, StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
        {
            merged[key] = merged.TryGetValue(key, out var inherited)
                ? new ModThemeScreenStyle(value.BackgroundColor ?? inherited.BackgroundColor, value.BackgroundAsset ?? inherited.BackgroundAsset)
                : value;
        }
        return merged;
    }

    private static ModThemeStyle MergeStyle(ModThemeStyle baseline, ModThemeStyle overrides)
    {
        var states = new Dictionary<string, ModThemeStyle>(baseline.States, StringComparer.Ordinal);
        foreach (var (state, style) in overrides.States)
        {
            states[state] = states.TryGetValue(state, out var inherited)
                ? MergeStyle(inherited, style)
                : style;
        }

        return new ModThemeStyle(
            overrides.Font ?? baseline.Font,
            overrides.FontSize ?? baseline.FontSize,
            overrides.TextColor ?? baseline.TextColor,
            overrides.BackgroundColor ?? baseline.BackgroundColor,
            overrides.BorderColor ?? baseline.BorderColor,
            overrides.BorderWidth ?? baseline.BorderWidth,
            overrides.Radius ?? baseline.Radius,
            overrides.Padding ?? baseline.Padding,
            overrides.BackgroundAsset ?? baseline.BackgroundAsset,
            overrides.Slice ?? baseline.Slice,
            overrides.Opacity ?? baseline.Opacity,
            states);
    }
}

internal sealed class ModThemeFileModel
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("colors")]
    public Dictionary<string, string>? Colors { get; set; }

    [JsonPropertyName("typography")]
    public ModThemeTypographyModel? Typography { get; set; }

    [JsonPropertyName("spacing")]
    public Dictionary<string, int>? Spacing { get; set; }

    [JsonPropertyName("shape")]
    public Dictionary<string, int>? Shape { get; set; }

    [JsonPropertyName("metrics")]
    public Dictionary<string, double>? Metrics { get; set; }

    [JsonPropertyName("components")]
    public Dictionary<string, ModThemeStyleModel>? Components { get; set; }

    [JsonPropertyName("screens")]
    public Dictionary<string, ModThemeScreenStyleModel>? Screens { get; set; }
}

internal sealed class ModThemeTypographyModel
{
    [JsonPropertyName("fonts")]
    public Dictionary<string, ModThemeFontModel>? Fonts { get; set; }

    [JsonPropertyName("sizes")]
    public Dictionary<string, int>? Sizes { get; set; }
}

internal sealed class ModThemeFontModel
{
    [JsonPropertyName("asset")]
    public string? Asset { get; set; }
}

internal sealed class ModThemeStyleModel
{
    [JsonPropertyName("font")]
    public string? Font { get; set; }

    [JsonPropertyName("fontSize")]
    public string? FontSize { get; set; }

    [JsonPropertyName("textColor")]
    public string? TextColor { get; set; }

    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }

    [JsonPropertyName("borderColor")]
    public string? BorderColor { get; set; }

    [JsonPropertyName("borderWidth")]
    public int? BorderWidth { get; set; }

    [JsonPropertyName("radius")]
    public string? Radius { get; set; }

    [JsonPropertyName("padding")]
    public ModThemePaddingModel? Padding { get; set; }

    [JsonPropertyName("backgroundAsset")]
    public string? BackgroundAsset { get; set; }

    [JsonPropertyName("slice")]
    public ModThemeSliceModel? Slice { get; set; }

    [JsonPropertyName("opacity")]
    public double? Opacity { get; set; }

    [JsonPropertyName("states")]
    public Dictionary<string, ModThemeStyleModel>? States { get; set; }
}

internal sealed class ModThemePaddingModel
{
    [JsonPropertyName("horizontal")]
    public string? Horizontal { get; set; }

    [JsonPropertyName("vertical")]
    public string? Vertical { get; set; }
}

internal sealed class ModThemeSliceModel
{
    [JsonPropertyName("left")]
    public int Left { get; set; }

    [JsonPropertyName("top")]
    public int Top { get; set; }

    [JsonPropertyName("right")]
    public int Right { get; set; }

    [JsonPropertyName("bottom")]
    public int Bottom { get; set; }
}

internal sealed class ModThemeScreenStyleModel
{
    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }

    [JsonPropertyName("backgroundAsset")]
    public string? BackgroundAsset { get; set; }
}
