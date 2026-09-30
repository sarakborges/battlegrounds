namespace Battlegrounds.Content;

public sealed partial class ModThemeCatalog
{
    public static ModThemeCatalog Layer(ModThemeCatalog baseTheme, ModThemeCatalog overrides)
    {
        ArgumentNullException.ThrowIfNull(baseTheme);
        ArgumentNullException.ThrowIfNull(overrides);
        if (baseTheme.Version != overrides.Version)
        {
            throw new InvalidOperationException(
                $"Cannot layer theme v{overrides.Version} over theme v{baseTheme.Version}.");
        }

        return new ModThemeCatalog(
            baseTheme.Version,
            MergeMap(baseTheme.Colors, overrides.Colors),
            MergeMap(baseTheme.Fonts, overrides.Fonts),
            MergeMap(baseTheme.FontSizes, overrides.FontSizes),
            MergeMap(baseTheme.Spacing, overrides.Spacing),
            MergeMap(baseTheme.Radii, overrides.Radii),
            MergeMap(baseTheme.Metrics, overrides.Metrics),
            MergeComponents(baseTheme.Components, overrides.Components),
            MergeScreens(baseTheme.Screens, overrides.Screens));
    }

    private static Dictionary<string, T> MergeMap<T>(
        IReadOnlyDictionary<string, T> baseValues,
        IReadOnlyDictionary<string, T> overrides)
    {
        var result = new Dictionary<string, T>(baseValues, StringComparer.Ordinal);
        foreach (var pair in overrides)
            result[pair.Key] = pair.Value;
        return result;
    }

    private static Dictionary<string, ModThemeStyle> MergeComponents(
        IReadOnlyDictionary<string, ModThemeStyle> baseValues,
        IReadOnlyDictionary<string, ModThemeStyle> overrides)
    {
        var result = new Dictionary<string, ModThemeStyle>(baseValues, StringComparer.Ordinal);
        foreach (var pair in overrides)
        {
            result[pair.Key] = result.TryGetValue(pair.Key, out var inherited)
                ? MergeStyle(inherited, pair.Value)
                : pair.Value;
        }
        return result;
    }

    private static ModThemeStyle MergeStyle(ModThemeStyle inherited, ModThemeStyle own)
    {
        var states = new Dictionary<string, ModThemeStyle>(inherited.States, StringComparer.Ordinal);
        foreach (var pair in own.States)
        {
            states[pair.Key] = states.TryGetValue(pair.Key, out var inheritedState)
                ? MergeStyle(inheritedState, pair.Value)
                : pair.Value;
        }

        return new ModThemeStyle(
            own.Font ?? inherited.Font,
            own.FontSize ?? inherited.FontSize,
            own.TextColor ?? inherited.TextColor,
            own.BackgroundColor ?? inherited.BackgroundColor,
            own.BorderColor ?? inherited.BorderColor,
            own.BorderWidth ?? inherited.BorderWidth,
            own.Radius ?? inherited.Radius,
            own.Padding ?? inherited.Padding,
            own.BackgroundAsset ?? inherited.BackgroundAsset,
            own.IconAsset ?? inherited.IconAsset,
            own.Width ?? inherited.Width,
            own.Height ?? inherited.Height,
            own.IconWidth ?? inherited.IconWidth,
            own.IconHeight ?? inherited.IconHeight,
            own.Slice ?? inherited.Slice,
            own.Opacity ?? inherited.Opacity,
            states);
    }

    private static Dictionary<string, ModThemeScreenStyle> MergeScreens(
        IReadOnlyDictionary<string, ModThemeScreenStyle> baseValues,
        IReadOnlyDictionary<string, ModThemeScreenStyle> overrides)
    {
        var result = new Dictionary<string, ModThemeScreenStyle>(baseValues, StringComparer.Ordinal);
        foreach (var pair in overrides)
        {
            result[pair.Key] = result.TryGetValue(pair.Key, out var inherited)
                ? new ModThemeScreenStyle(
                    pair.Value.BackgroundColor ?? inherited.BackgroundColor,
                    pair.Value.BackgroundAsset ?? inherited.BackgroundAsset)
                : pair.Value;
        }
        return result;
    }
}

public sealed partial class ModThemeLoader
{
    private const string EngineDefaultThemeResource = "Battlegrounds.Content.EngineDefaultTheme.json";

    public ModThemeCatalog LoadEngineDefault()
    {
        using var stream = typeof(ModThemeLoader).Assembly.GetManifestResourceStream(EngineDefaultThemeResource)
            ?? throw new InvalidOperationException($"Embedded theme resource '{EngineDefaultThemeResource}' was not found.");
        using var reader = new StreamReader(stream);
        var theme = DeserializeCatalog(reader.ReadToEnd());
        ValidateEngineDefault(theme);
        return theme;
    }

    private static void ValidateEngineDefault(ModThemeCatalog theme)
    {
        if (theme.Version != 1)
            throw new InvalidDataException($"Engine default theme must use schema v1, got v{theme.Version}.");

        var unknownMetric = theme.Metrics.Keys.FirstOrDefault(key => !ModThemeMetricNames.IsSupported(key));
        if (unknownMetric is not null)
            throw new InvalidDataException($"Engine default theme contains unknown metric '{unknownMetric}'.");

        var unknownComponent = theme.Components.Keys.FirstOrDefault(role => !ModThemeRoleNames.IsSupportedComponent(role));
        if (unknownComponent is not null)
            throw new InvalidDataException($"Engine default theme contains unknown component role '{unknownComponent}'.");

        var unknownScreen = theme.Screens.Keys.FirstOrDefault(role => !ModThemeRoleNames.IsSupportedScreen(role));
        if (unknownScreen is not null)
            throw new InvalidDataException($"Engine default theme contains unknown screen role '{unknownScreen}'.");

        if (theme.Fonts.Count > 0 ||
            theme.Components.Values.Any(ContainsAsset) ||
            theme.Screens.Values.Any(screen => !string.IsNullOrWhiteSpace(screen.BackgroundAsset)))
        {
            throw new InvalidDataException(
                "Engine default theme must be asset-free; asset-backed presentation belongs to the selected mod.");
        }
    }

    private static bool ContainsAsset(ModThemeStyle style) =>
        !string.IsNullOrWhiteSpace(style.BackgroundAsset) ||
        !string.IsNullOrWhiteSpace(style.IconAsset) ||
        style.States.Values.Any(ContainsAsset);
}
