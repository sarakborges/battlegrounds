using System.Collections.ObjectModel;
using System.Text.Json;

namespace Battlegrounds.Content;

public enum ModPresentationAssetType
{
    Image,
    Audio,
}

public static class ModPresentationAssetSlots
{
    public const string Portrait = "portrait";
    public const string Art = "art";
}

public sealed class ModPresentationAssetReference
{
    public ModPresentationAssetType Type { get; }
    public string RelativePath { get; }

    internal ModPresentationAssetReference(ModPresentationAssetType type, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("Asset path cannot be empty.", nameof(relativePath));
        Type = type;
        RelativePath = relativePath;
    }
}

public sealed class ModPresentationAssetEntry
{
    public ModPresentationEntityKind EntityKind { get; }
    public string EntityId { get; }
    public string Slot { get; }
    public ModPresentationAssetReference Asset { get; }

    internal ModPresentationAssetEntry(
        ModPresentationEntityKind entityKind,
        string entityId,
        string slot,
        ModPresentationAssetReference asset)
    {
        if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("Entity id cannot be empty.", nameof(entityId));
        if (string.IsNullOrWhiteSpace(slot)) throw new ArgumentException("Asset slot cannot be empty.", nameof(slot));
        EntityKind = entityKind;
        EntityId = entityId;
        Slot = slot;
        Asset = asset ?? throw new ArgumentNullException(nameof(asset));
    }
}

public sealed class ModPresentationAssetCatalog
{
    private readonly ReadOnlyCollection<ModPresentationAssetEntry> _all;
    private readonly ReadOnlyDictionary<string, ModPresentationAssetReference> _byKey;

    public IReadOnlyList<ModPresentationAssetEntry> All => _all;

    internal ModPresentationAssetCatalog(IEnumerable<ModPresentationAssetEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var ordered = entries
            .OrderBy(entry => entry.EntityKind)
            .ThenBy(entry => entry.EntityId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Slot, StringComparer.Ordinal)
            .ToArray();
        _all = Array.AsReadOnly(ordered);
        _byKey = new ReadOnlyDictionary<string, ModPresentationAssetReference>(
            ordered.ToDictionary(
                entry => Key(entry.EntityKind, entry.EntityId, entry.Slot),
                entry => entry.Asset,
                StringComparer.Ordinal));
    }

    public bool TryGet(
        ModPresentationEntityKind entityKind,
        string entityId,
        string slot,
        out ModPresentationAssetReference asset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        return _byKey.TryGetValue(Key(entityKind, entityId, slot), out asset!);
    }

    public ModPresentationAssetReference GetRequired(
        ModPresentationEntityKind entityKind,
        string entityId,
        string slot) =>
        TryGet(entityKind, entityId, slot, out var asset)
            ? asset
            : throw new KeyNotFoundException($"Presentation asset '{entityKind}:{entityId}:{slot}' is not defined.");

    private static string Key(ModPresentationEntityKind kind, string id, string slot) =>
        $"{kind}\u001f{id}\u001f{slot}";
}

public sealed class ModPresentationAssetLoader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public ModPresentationAssetCatalog Load(string modDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);
        var issues = new PresentationAssetModValidator().Validate(modDirectory);
        if (issues.Count > 0) throw new ModValidationException(new ModValidationReport(issues));
        return LoadValidated(modDirectory);
    }

    internal static ModPresentationAssetCatalog LoadValidated(string modDirectory)
    {
        var manifestPath = Path.Combine(modDirectory, "assets", "presentation.json");
        if (!File.Exists(manifestPath)) return new ModPresentationAssetCatalog([]);

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath), DocumentOptions);
        var entries = new List<ModPresentationAssetEntry>();
        ReadCategory(document.RootElement, "leaders", ModPresentationEntityKind.Leader, ModPresentationAssetSlots.Portrait, entries);
        ReadCategory(document.RootElement, "units", ModPresentationEntityKind.Unit, ModPresentationAssetSlots.Art, entries);
        ReadCategory(document.RootElement, "actions", ModPresentationEntityKind.Action, ModPresentationAssetSlots.Art, entries);
        return new ModPresentationAssetCatalog(entries);
    }

    private static void ReadCategory(
        JsonElement root,
        string category,
        ModPresentationEntityKind kind,
        string slot,
        ICollection<ModPresentationAssetEntry> entries)
    {
        if (!root.TryGetProperty(category, out var categoryElement)) return;
        foreach (var entity in categoryElement.EnumerateObject())
        {
            if (!entity.Value.TryGetProperty(slot, out var pathElement)) continue;
            var relativePath = pathElement.GetString()
                ?? throw new InvalidDataException($"Validated presentation asset '{category}.{entity.Name}.{slot}' has no path.");
            entries.Add(new ModPresentationAssetEntry(
                kind,
                entity.Name,
                slot,
                new ModPresentationAssetReference(ModPresentationAssetType.Image, relativePath)));
        }
    }
}
