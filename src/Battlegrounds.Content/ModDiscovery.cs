using System.Text.Json;

namespace Battlegrounds.Content;

public sealed record ModPackageSummary(int? SchemaVersion, string? Id, string? Name);

public sealed record ModDiscoveryEntry(
    string DirectoryName,
    string DirectoryPath,
    ModPackageSummary Summary,
    ModValidationReport Validation)
{
    public bool IsValid => Validation.IsValid;
    public string DisplayName => string.IsNullOrWhiteSpace(Summary.Name) ? DirectoryName : Summary.Name;
}

public sealed class ModDiscovery
{
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private readonly ModValidator _validator;

    public ModDiscovery(ModValidator? validator = null) => _validator = validator ?? new ModValidator();

    public IReadOnlyList<ModDiscoveryEntry> Discover(string modsRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modsRoot);

        var root = Path.GetFullPath(modsRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Mods root '{root}' does not exist.");

        return Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .Select(Inspect)
            .ToArray();
    }

    private ModDiscoveryEntry Inspect(string directory)
    {
        var normalizedDirectory = Path.GetFullPath(directory);
        var directoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(normalizedDirectory));
        var summary = ReadSummary(normalizedDirectory);
        var validation = ValidateSafely(normalizedDirectory);
        return new ModDiscoveryEntry(directoryName, normalizedDirectory, summary, validation);
    }

    private ModValidationReport ValidateSafely(string directory)
    {
        try
        {
            return _validator.Validate(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return new ModValidationReport([
                new ModValidationIssue(
                    "DISCOVERY_VALIDATION_FAILURE",
                    ".",
                    "$",
                    $"Validation could not complete while discovering this package: {exception.Message}")
            ]);
        }
    }

    private static ModPackageSummary ReadSummary(string directory)
    {
        var manifestPath = Path.Combine(directory, "mod.json");
        if (!File.Exists(manifestPath)) return new ModPackageSummary(null, null, null);

        try
        {
            using var stream = File.OpenRead(manifestPath);
            using var document = JsonDocument.Parse(stream, JsonOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new ModPackageSummary(null, null, null);

            var root = document.RootElement;
            int? schemaVersion = root.TryGetProperty("schemaVersion", out var schemaElement) &&
                                 schemaElement.ValueKind == JsonValueKind.Number &&
                                 schemaElement.TryGetInt32(out var schema)
                ? schema
                : null;
            var id = ReadOptionalString(root, "id");
            var name = ReadOptionalString(root, "name");
            return new ModPackageSummary(schemaVersion, id, name);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new ModPackageSummary(null, null, null);
        }
    }

    private static string? ReadOptionalString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
