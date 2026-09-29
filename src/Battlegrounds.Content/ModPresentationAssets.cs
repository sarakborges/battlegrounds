using System.Collections.ObjectModel;
using System.Text.Json;

namespace Battlegrounds.Content;

public enum ModPresentationAssetType
{
    Image,
    Audio,
}

public enum ModPresentationAnimation
{
    None,
    Pulse,
    Shake,
    Lunge,
    Fade,
    Pop,
}

public static class ModPresentationAssetSlots
{
    public const string Portrait = "portrait";
    public const string Art = "art";
}

public static class ModPresentationCueRoles
{
    public const string UiSelect = "ui.select";
    public const string UiAcquire = "ui.acquire";
    public const string UiDeploy = "ui.deploy";
    public const string UiPlay = "ui.play";
    public const string UiRelease = "ui.release";

    public const string CombatAttack = "combat.attack";
    public const string CombatTarget = "combat.target";
    public const string CombatSummon = "combat.summon";
    public const string CombatStats = "combat.stats";
    public const string CombatDamage = "combat.damage";
    public const string CombatDestroy = "combat.destroy";
    public const string CombatDeath = "combat.death";
    public const string CombatRevive = "combat.revive";
    public const string CombatTrigger = "combat.trigger";
    public const string CombatBehavior = "combat.behavior";
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

public sealed class ModPresentationCue
{
    public ModPresentationEntityKind EntityKind { get; }
    public string EntityId { get; }
    public string Role { get; }
    public ModPresentationAnimation? Animation { get; }
    public double? DurationSeconds { get; }
    public ModPresentationAssetReference? Audio { get; }

    internal ModPresentationCue(
        ModPresentationEntityKind entityKind,
        string entityId,
        string role,
        ModPresentationAnimation? animation,
        double? durationSeconds,
        ModPresentationAssetReference? audio)
    {
        if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("Entity id cannot be empty.", nameof(entityId));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Cue role cannot be empty.", nameof(role));
        EntityKind = entityKind;
        EntityId = entityId;
        Role = role;
        Animation = animation;
        DurationSeconds = durationSeconds;
        Audio = audio;
    }
}

public sealed class ModPresentationCueCatalog
{
    private readonly ReadOnlyCollection<ModPresentationCue> _all;
    private readonly ReadOnlyDictionary<string, ModPresentationCue> _byKey;

    public IReadOnlyList<ModPresentationCue> All => _all;

    internal ModPresentationCueCatalog(IEnumerable<ModPresentationCue> cues)
    {
        ArgumentNullException.ThrowIfNull(cues);
        var ordered = cues
            .OrderBy(cue => cue.EntityKind)
            .ThenBy(cue => cue.EntityId, StringComparer.Ordinal)
            .ThenBy(cue => cue.Role, StringComparer.Ordinal)
            .ToArray();
        _all = Array.AsReadOnly(ordered);
        _byKey = new ReadOnlyDictionary<string, ModPresentationCue>(ordered.ToDictionary(
            cue => Key(cue.EntityKind, cue.EntityId, cue.Role),
            cue => cue,
            StringComparer.Ordinal));
    }

    public bool TryGet(ModPresentationEntityKind entityKind, string entityId, string role, out ModPresentationCue cue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        return _byKey.TryGetValue(Key(entityKind, entityId, role), out cue!);
    }

    public ModPresentationCue GetRequired(ModPresentationEntityKind entityKind, string entityId, string role) =>
        TryGet(entityKind, entityId, role, out var cue)
            ? cue
            : throw new KeyNotFoundException($"Presentation cue '{entityKind}:{entityId}:{role}' is not defined.");

    private static string Key(ModPresentationEntityKind kind, string id, string role) => $"{kind}\u001f{id}\u001f{role}";
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
        var entries = new List<ModPresentationAssetEntry>();
        var manifestPath = Path.Combine(modDirectory, "assets", "presentation.json");
        if (File.Exists(manifestPath))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath), DocumentOptions);
            ReadAssetCategory(document.RootElement, "leaders", ModPresentationEntityKind.Leader, ModPresentationAssetSlots.Portrait, entries);
        }

        ReadInlineCardArt(modDirectory, "content/units", ModPresentationEntityKind.Unit, entries);
        ReadInlineCardArt(modDirectory, "content/actions", ModPresentationEntityKind.Action, entries);
        return new ModPresentationAssetCatalog(entries);
    }

    private static void ReadInlineCardArt(
        string modDirectory,
        string contentDirectory,
        ModPresentationEntityKind kind,
        ICollection<ModPresentationAssetEntry> entries)
    {
        var directory = Path.Combine(modDirectory, contentDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory)) return;

        foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            var root = document.RootElement;
            if (!root.TryGetProperty(ModPresentationAssetSlots.Art, out var artElement)) continue;

            var entityId = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
            entityId ??= Path.GetFileNameWithoutExtension(path);
            var relativePath = artElement.GetString()
                ?? throw new InvalidDataException($"Validated card art '{contentDirectory}/{entityId}.json' has no path.");
            entries.Add(new ModPresentationAssetEntry(
                kind,
                entityId,
                ModPresentationAssetSlots.Art,
                new ModPresentationAssetReference(ModPresentationAssetType.Image, relativePath)));
        }
    }

    private static void ReadAssetCategory(
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

public sealed class ModPresentationCueLoader
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public ModPresentationCueCatalog Load(string modDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modDirectory);
        var issues = new PresentationAssetModValidator().Validate(modDirectory);
        if (issues.Count > 0) throw new ModValidationException(new ModValidationReport(issues));
        return LoadValidated(modDirectory);
    }

    internal static ModPresentationCueCatalog LoadValidated(string modDirectory)
    {
        var manifestPath = Path.Combine(modDirectory, "assets", "presentation.json");
        if (!File.Exists(manifestPath)) return new ModPresentationCueCatalog([]);

        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath), DocumentOptions);
        var cues = new List<ModPresentationCue>();
        ReadCueCategory(document.RootElement, "leaders", ModPresentationEntityKind.Leader, cues);
        ReadCueCategory(document.RootElement, "units", ModPresentationEntityKind.Unit, cues);
        ReadCueCategory(document.RootElement, "actions", ModPresentationEntityKind.Action, cues);
        return new ModPresentationCueCatalog(cues);
    }

    private static void ReadCueCategory(
        JsonElement root,
        string category,
        ModPresentationEntityKind kind,
        ICollection<ModPresentationCue> cues)
    {
        if (!root.TryGetProperty(category, out var categoryElement)) return;
        foreach (var entity in categoryElement.EnumerateObject())
        {
            if (!entity.Value.TryGetProperty("cues", out var cueElement)) continue;
            foreach (var cueProperty in cueElement.EnumerateObject())
            {
                var cue = cueProperty.Value;
                ModPresentationAnimation? animation = null;
                if (cue.TryGetProperty("animation", out var animationElement))
                {
                    animation = Enum.Parse<ModPresentationAnimation>(animationElement.GetString()!, ignoreCase: true);
                }

                double? durationSeconds = cue.TryGetProperty("durationSeconds", out var durationElement)
                    ? durationElement.GetDouble()
                    : null;
                ModPresentationAssetReference? audio = null;
                if (cue.TryGetProperty("audio", out var audioElement))
                {
                    audio = new ModPresentationAssetReference(ModPresentationAssetType.Audio, audioElement.GetString()!);
                }

                cues.Add(new ModPresentationCue(kind, entity.Name, cueProperty.Name, animation, durationSeconds, audio));
            }
        }
    }
}
