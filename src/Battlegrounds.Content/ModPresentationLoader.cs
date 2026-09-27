using System.Text.Json;

namespace Battlegrounds.Content;

internal static class ModPresentationLoader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static ModPresentationCatalog Load(
        string modDirectory,
        IReadOnlyDictionary<string, string> terminology)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);
        ArgumentNullException.ThrowIfNull(terminology);

        var localizationDirectory = Path.Combine(modDirectory, "localization");
        var presentationPath = Path.Combine(localizationDirectory, "presentation.json");
        using var presentationDocument = JsonDocument.Parse(File.ReadAllText(presentationPath), DocumentOptions);
        var defaultLocale = presentationDocument.RootElement.GetProperty("defaultLocale").GetString()
            ?? throw new InvalidDataException("Validated localization/presentation.json is missing defaultLocale.");

        var locales = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.GetFiles(localizationDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .Where(path => !string.Equals(Path.GetFileName(path), "presentation.json", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            var values = document.RootElement.EnumerateObject()
                .ToDictionary(
                    pair => pair.Name,
                    pair => pair.Value.GetString() ?? throw new InvalidDataException($"Validated locale '{path}' contains a non-string value."),
                    StringComparer.Ordinal);
            locales.Add(Path.GetFileNameWithoutExtension(path), values);
        }

        var entityFallbacks = ModPresentationEntityFallbackLoader.Load(modDirectory);
        return new ModPresentationCatalog(defaultLocale, terminology, entityFallbacks, locales);
    }
}
