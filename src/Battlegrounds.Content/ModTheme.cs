using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Battlegrounds.Content;

public static class ModThemeComponentRoles
{
    public const string Button = "button";
    public const string ButtonPrimary = "button.primary";
    public const string Card = "card";
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

public sealed class ModThemeCatalog
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

    public bool IsEmpty =>
        _colors.Count == 0 &&
        _fonts.Count == 0 &&
        _fontSizes.Count == 0 &&
        _spacing.Count == 0 &&
        _radii.Count == 0 &&
        _metrics.Count == 0 &&
        _components.Count == 0 &&
        _screens.Count == 0;

    internal ModThemeCatalog(
        int version,
        IReadOnlyDictionary<string, string> colors,
        IReadOnlyDictionary<string, ModThemeFont> fonts,
        IReadOnlyDictionary<string, int> fontSizes,
        IReadOnlyDictionary<string, int> spacing,
        IReadOnlyDictionary<string, int> radii,
        IReadOnlyDictionary<string, double> metrics,
        IReadOnlyDictionary<string, ModThemeStyle> components,
        IReadOnlyDictionary<string, ModThemeScreenStyle> screens)
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

    internal static ModThemeCatalog Empty { get; } = new(
        1,
        new Dictionary<string, string>(),
        new Dictionary<string, ModThemeFont>(),
        new Dictionary<string, int>(),
        new Dictionary<string, int>(),
        new Dictionary<string, int>(),
        new Dictionary<string, double>(),
        new Dictionary<string, ModThemeStyle>(),
        new Dictionary<string, ModThemeScreenStyle>());

    public bool TryResolveColor(string? value, out string color)
    {
        color = string.Empty;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.StartsWith('#'))
        {
            color = value;
            return true;
        }
        return _colors.TryGetValue(value, out color!);
    }

    private static ReadOnlyDictionary<string, T> Copy<T>(IReadOnlyDictionary<string, T> source) =>
        new(new Dictionary<string, T>(source, StringComparer.Ordinal));
}

public sealed class ModThemeLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public ModThemeCatalog Load(string modDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);
        var issues = new ModThemeValidator().Validate(modDirectory)
            .Concat(new ModThemeMetricsValidator().Validate(modDirectory))
            .ToArray();
        if (issues.Length > 0) throw new ModValidationException(new ModValidationReport(issues));
        return LoadValidated(modDirectory);
    }

    internal static ModThemeCatalog LoadValidated(string modDirectory)
    {
        var path = Path.Combine(modDirectory, "presentation", "theme.json");
        if (!File.Exists(path)) return ModThemeCatalog.Empty;

        var data = JsonSerializer.Deserialize<ThemeData>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Validated theme contained no data.");

        var fonts = (data.Typography?.Fonts ?? new Dictionary<string, ThemeFontData>())
            .ToDictionary(pair => pair.Key, pair => new ModThemeFont(pair.Value.Asset), StringComparer.Ordinal);
        var components = (data.Components ?? new Dictionary<string, ThemeStyleData>())
            .ToDictionary(pair => pair.Key, pair => BuildStyle(pair.Value, includeStates: true), StringComparer.Ordinal);
        var screens = (data.Screens ?? new Dictionary<string, ThemeScreenData>())
            .ToDictionary(
                pair => pair.Key,
                pair => new ModThemeScreenStyle(pair.Value.BackgroundColor, pair.Value.BackgroundAsset),
                StringComparer.Ordinal);

        return new ModThemeCatalog(
            data.Version,
            data.Colors ?? new Dictionary<string, string>(),
            fonts,
            data.Typography?.Sizes ?? new Dictionary<string, int>(),
            data.Spacing ?? new Dictionary<string, int>(),
            data.Shape ?? new Dictionary<string, int>(),
            data.Metrics ?? new Dictionary<string, double>(),
            components,
            screens);
    }

    private static ModThemeStyle BuildStyle(ThemeStyleData data, bool includeStates)
    {
        var states = includeStates
            ? (data.States ?? new Dictionary<string, ThemeStyleData>())
                .ToDictionary(pair => pair.Key, pair => BuildStyle(pair.Value, includeStates: false), StringComparer.Ordinal)
            : new Dictionary<string, ModThemeStyle>();

        return new ModThemeStyle(
            data.Font,
            data.FontSize,
            data.TextColor,
            data.BackgroundColor,
            data.BorderColor,
            data.BorderWidth,
            data.Radius,
            data.Padding is null ? null : new ModThemePadding(data.Padding.Horizontal, data.Padding.Vertical),
            data.BackgroundAsset,
            data.Slice is null ? null : new ModThemeSlice(data.Slice.Left, data.Slice.Top, data.Slice.Right, data.Slice.Bottom),
            data.Opacity,
            states);
    }

    private sealed class ThemeData
    {
        public int Version { get; set; }
        public Dictionary<string, string>? Colors { get; set; }
        public ThemeTypographyData? Typography { get; set; }
        public Dictionary<string, int>? Spacing { get; set; }
        public Dictionary<string, int>? Shape { get; set; }
        public Dictionary<string, double>? Metrics { get; set; }
        public Dictionary<string, ThemeStyleData>? Components { get; set; }
        public Dictionary<string, ThemeScreenData>? Screens { get; set; }
    }

    private sealed class ThemeTypographyData
    {
        public Dictionary<string, ThemeFontData>? Fonts { get; set; }
        public Dictionary<string, int>? Sizes { get; set; }
    }

    private sealed class ThemeFontData
    {
        public string Asset { get; set; } = string.Empty;
    }

    private sealed class ThemePaddingData
    {
        public string Horizontal { get; set; } = string.Empty;
        public string Vertical { get; set; } = string.Empty;
    }

    private sealed class ThemeSliceData
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }
    }

    private sealed class ThemeStyleData
    {
        public string? Font { get; set; }
        public string? FontSize { get; set; }
        public string? TextColor { get; set; }
        public string? BackgroundColor { get; set; }
        public string? BorderColor { get; set; }
        public int? BorderWidth { get; set; }
        public string? Radius { get; set; }
        public ThemePaddingData? Padding { get; set; }
        public string? BackgroundAsset { get; set; }
        public ThemeSliceData? Slice { get; set; }
        public double? Opacity { get; set; }
        public Dictionary<string, ThemeStyleData>? States { get; set; }
    }

    private sealed class ThemeScreenData
    {
        public string? BackgroundColor { get; set; }
        public string? BackgroundAsset { get; set; }
    }
}
