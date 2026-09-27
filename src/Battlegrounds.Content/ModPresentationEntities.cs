using System.Text.Json;

namespace Battlegrounds.Content;

public enum ModPresentationEntityKind
{
    Leader,
    Power,
    Unit,
    Action,
    Behavior,
    UnitType,
    Tag,
    Combine,
}

public static class ModPresentationEntityKeys
{
    private static readonly IReadOnlyList<ModPresentationEntityDescriptor> EntityDescriptors = Array.AsReadOnly(new[]
    {
        new ModPresentationEntityDescriptor(ModPresentationEntityKind.Leader, "leader", "leaders"),
        new ModPresentationEntityDescriptor(ModPresentationEntityKind.Power, "power", "powers"),
        new ModPresentationEntityDescriptor(ModPresentationEntityKind.Unit, "unit", "units"),
        new ModPresentationEntityDescriptor(ModPresentationEntityKind.Action, "action", "actions"),
        new ModPresentationEntityDescriptor(ModPresentationEntityKind.Behavior, "behavior", "behaviors"),
        new ModPresentationEntityDescriptor(ModPresentationEntityKind.UnitType, "type", "types"),
        new ModPresentationEntityDescriptor(ModPresentationEntityKind.Tag, "tag", "tags"),
        new ModPresentationEntityDescriptor(ModPresentationEntityKind.Combine, "combine", "combines"),
    });

    internal static IReadOnlyList<ModPresentationEntityDescriptor> Descriptors => EntityDescriptors;

    public static string Name(ModPresentationEntityKind kind, string id) => Build(kind, id, "name");

    public static string Description(ModPresentationEntityKind kind, string id) => Build(kind, id, "description");

    public static bool TryParse(
        string key,
        out ModPresentationEntityKind kind,
        out string id,
        out string field)
    {
        kind = default;
        id = string.Empty;
        field = string.Empty;
        if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("entity.", StringComparison.Ordinal)) return false;

        var parts = key.Split('.', StringSplitOptions.None);
        if (parts.Length != 4 || string.IsNullOrWhiteSpace(parts[2])) return false;
        var descriptor = EntityDescriptors.FirstOrDefault(value => string.Equals(value.KeySegment, parts[1], StringComparison.Ordinal));
        if (descriptor is null) return false;
        if (parts[3] is not ("name" or "description")) return false;

        kind = descriptor.Kind;
        id = parts[2];
        field = parts[3];
        return true;
    }

    internal static ModPresentationEntityDescriptor GetDescriptor(ModPresentationEntityKind kind) =>
        EntityDescriptors.FirstOrDefault(value => value.Kind == kind)
        ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported presentation entity kind.");

    private static string Build(ModPresentationEntityKind kind, string id, string field)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Entity id cannot be empty.", nameof(id));
        var descriptor = GetDescriptor(kind);
        return $"entity.{descriptor.KeySegment}.{id}.{field}";
    }
}

internal sealed record ModPresentationEntityDescriptor(
    ModPresentationEntityKind Kind,
    string KeySegment,
    string ContentDirectory);

internal static class ModPresentationEntityFallbackLoader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static IReadOnlyDictionary<string, string> Load(string modDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);
        var fallbacks = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var descriptor in ModPresentationEntityKeys.Descriptors)
        {
            var directory = Path.Combine(modDirectory, "content", descriptor.ContentDirectory);
            foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                         .OrderBy(Path.GetFileName, StringComparer.Ordinal))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
                var root = document.RootElement;
                var id = root.GetProperty("id").GetString()
                    ?? throw new InvalidDataException($"Validated entity '{path}' is missing id.");
                var name = root.GetProperty("name").GetString()
                    ?? throw new InvalidDataException($"Validated entity '{path}' is missing name.");
                fallbacks.Add(ModPresentationEntityKeys.Name(descriptor.Kind, id), name);
            }
        }

        return fallbacks;
    }
}
