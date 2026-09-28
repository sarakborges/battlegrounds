using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class PersistentUnitMutationModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> Kinds =
    [
        "transformUnit",
        "copyUnitToReserve",
        "applyUnitModifier",
        "removeUnitModifier",
    ];

    private static readonly HashSet<string> Selections =
    [
        "all", "random", "lowestAttack", "highestAttack", "lowestHealth", "highestHealth",
        "leftmost", "rightmost", "adjacent", "leftAdjacent", "rightAdjacent",
    ];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var unitIds = ReadEntityIds(modDirectory, "content/units");
        var typeIds = ReadEntityIds(modDirectory, "content/types");
        var tagIds = ReadEntityIds(modDirectory, "content/tags");

        foreach (var file in ReadEntityDirectory(modDirectory, "content/actions"))
        {
            if (file.Root.TryGetProperty("effects", out var effects))
                ValidateEffects(file.Path, effects, "$.effects", contextAllowed: true, unitIds, typeIds, tagIds, issues);
        }

        foreach (var file in ReadEntityDirectory(modDirectory, "content/units"))
            ValidateTriggers(file, powerMode: false, unitIds, typeIds, tagIds, issues);
        foreach (var file in ReadEntityDirectory(modDirectory, "content/powers"))
            ValidateTriggers(file, powerMode: true, unitIds, typeIds, tagIds, issues);

        return issues;
    }

    private static void ValidateTriggers(
        EntityFile file,
        bool powerMode,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (!file.Root.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array) return;

        var triggerIndex = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            if (trigger.ValueKind != JsonValueKind.Object)
            {
                triggerIndex++;
                continue;
            }

            var eventName = trigger.TryGetProperty("event", out var eventValue) && eventValue.ValueKind == JsonValueKind.String
                ? eventValue.GetString()
                : null;
            var contextAllowed = powerMode
                ? eventName is "onActivate" or "onMatchStart" or "onTurnStart" or "onTurnEnd"
                : eventName is "onAcquire" or "onPlay" or "onTurnStart" or "onTurnEnd";

            if (trigger.TryGetProperty("effects", out var effects))
            {
                ValidateEffects(
                    file.Path,
                    effects,
                    $"$.triggers[{triggerIndex}].effects",
                    contextAllowed,
                    unitIds,
                    typeIds,
                    tagIds,
                    issues);
            }
            triggerIndex++;
        }
    }

    private static void ValidateEffects(
        string file,
        JsonElement effects,
        string path,
        bool contextAllowed,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (effects.ValueKind != JsonValueKind.Array) return;

        var index = 0;
        foreach (var effect in effects.EnumerateArray())
        {
            var effectPath = $"{path}[{index}]";
            if (effect.ValueKind != JsonValueKind.Object ||
                !effect.TryGetProperty("kind", out var kindValue) ||
                kindValue.ValueKind != JsonValueKind.String)
            {
                index++;
                continue;
            }

            var kind = kindValue.GetString();
            if (kind is null || !Kinds.Contains(kind))
            {
                if (effect.TryGetProperty("modifierKey", out _))
                    issues.Add(new("UNKNOWN_KEY", file, effectPath + ".modifierKey", "modifierKey is only valid for persistent unit modifier effects."));
                index++;
                continue;
            }

            if (!contextAllowed)
                issues.Add(new("INVALID_EFFECT_CONTEXT", file, effectPath + ".kind", "Persistent unit mutations are only valid in preparation-only effect contexts."));

            switch (kind)
            {
                case "transformUnit":
                    ValidateKeys(effect, file, effectPath, ["kind", "target", "unitId"], ["kind", "target", "unitId"], issues);
                    ValidateReference(effect, "unitId", file, effectPath, unitIds, "unit", issues);
                    break;
                case "copyUnitToReserve":
                    ValidateKeys(effect, file, effectPath, ["kind", "target"], ["kind", "target"], issues);
                    break;
                case "applyUnitModifier":
                    ValidateKeys(effect, file, effectPath, ["kind", "target", "modifierKey", "attack", "health"], ["kind", "target", "modifierKey"], issues);
                    ValidateModifierKey(effect, file, effectPath, issues);
                    if (!effect.TryGetProperty("attack", out _) && !effect.TryGetProperty("health", out _))
                        issues.Add(new("MISSING_REQUIRED_PARAMETER", file, effectPath, "applyUnitModifier requires attack and/or health."));
                    ValidateOptionalValueShape(effect, "attack", file, effectPath, issues);
                    ValidateOptionalValueShape(effect, "health", file, effectPath, issues);
                    break;
                case "removeUnitModifier":
                    ValidateKeys(effect, file, effectPath, ["kind", "target", "modifierKey"], ["kind", "target", "modifierKey"], issues);
                    ValidateModifierKey(effect, file, effectPath, issues);
                    break;
            }

            ValidateTarget(effect, file, effectPath, typeIds, tagIds, issues);
            index++;
        }
    }

    private static void ValidateTarget(
        JsonElement effect,
        string file,
        string effectPath,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (!effect.TryGetProperty("target", out var target)) return;
        var path = effectPath + ".target";
        if (target.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
            return;
        }

        ValidateKeys(
            target,
            file,
            path,
            ["scope", "excludeSource", "typeId", "tagId", "selection", "limit", "relativeTo"],
            ["scope"],
            issues);

        string? scope = null;
        if (TryString(target, "scope", file, path + ".scope", issues, out scope) &&
            scope is not ("self" or "selected" or "friendly" or "enemy"))
            issues.Add(new("INVALID_VALUE", file, path + ".scope", $"Unsupported target scope '{scope}'."));

        if (target.TryGetProperty("excludeSource", out var excludeSource) &&
            excludeSource.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            issues.Add(new("INVALID_TYPE", file, path + ".excludeSource", "Expected a boolean."));

        if (target.TryGetProperty("selection", out var selectionValue))
        {
            if (selectionValue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(selectionValue.GetString()))
                issues.Add(new("INVALID_TYPE", file, path + ".selection", "Expected a non-empty string."));
            else if (!Selections.Contains(selectionValue.GetString()!))
                issues.Add(new("INVALID_VALUE", file, path + ".selection", $"Unsupported target selection '{selectionValue.GetString()}'."));
            else if (scope is "self" or "selected")
                issues.Add(new("INVALID_VALUE", file, path + ".selection", "Self and selected targets cannot use selection."));
        }

        if (target.TryGetProperty("limit", out var limit))
        {
            if (limit.ValueKind != JsonValueKind.Number || !limit.TryGetInt32(out var parsed) || parsed <= 0)
                issues.Add(new("INVALID_VALUE", file, path + ".limit", "Target limit must be a positive integer."));
            if (scope is "self" or "selected")
                issues.Add(new("INVALID_VALUE", file, path + ".limit", "Self and selected targets cannot use limit."));
        }

        if (target.TryGetProperty("typeId", out _))
            ValidateReference(target, "typeId", file, path, typeIds, "unit type", issues);
        if (target.TryGetProperty("tagId", out _))
            ValidateReference(target, "tagId", file, path, tagIds, "tag", issues);
    }

    private static void ValidateModifierKey(JsonElement effect, string file, string path, List<ModValidationIssue> issues) =>
        TryString(effect, "modifierKey", file, path + ".modifierKey", issues, out _);

    private static void ValidateOptionalValueShape(
        JsonElement effect,
        string property,
        string file,
        string path,
        List<ModValidationIssue> issues)
    {
        if (!effect.TryGetProperty(property, out var value)) return;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _)) return;
        if (value.ValueKind == JsonValueKind.Object &&
            value.TryGetProperty("kind", out var kind) &&
            kind.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(kind.GetString())) return;
        issues.Add(new("INVALID_TYPE", file, path + "." + property, "Expected an integer or effect value expression object."));
    }

    private static void ValidateReference(
        JsonElement parent,
        string property,
        string file,
        string path,
        IReadOnlySet<string> known,
        string label,
        List<ModValidationIssue> issues)
    {
        if (TryString(parent, property, file, path + "." + property, issues, out var id) && !known.Contains(id!))
            issues.Add(new("UNKNOWN_REFERENCE", file, path + "." + property, $"Unknown {label} '{id}'."));
    }

    private static void ValidateKeys(
        JsonElement element,
        string file,
        string path,
        IEnumerable<string> allowed,
        IEnumerable<string> required,
        List<ModValidationIssue> issues)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        var present = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var property in present.Where(name => !allowedSet.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("UNKNOWN_KEY", file, path + "." + property, $"Unknown key '{property}'."));
        foreach (var property in required.Where(name => !present.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Required parameter '{property}' is missing."));
    }

    private static bool TryString(
        JsonElement parent,
        string property,
        string file,
        string path,
        List<ModValidationIssue> issues,
        out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(property, out var element)) return false;
        if (element.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(element.GetString()))
        {
            value = element.GetString();
            return true;
        }
        issues.Add(new("INVALID_TYPE", file, path, "Expected a non-empty string."));
        return false;
    }

    private static HashSet<string> ReadEntityIds(string root, string relativeDirectory) =>
        ReadEntityDirectory(root, relativeDirectory)
            .Select(file => file.Root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyList<EntityFile> ReadEntityDirectory(string root, string relativeDirectory)
    {
        var directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory)) return [];

        var result = new List<EntityFile>();
        foreach (var fullPath in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                    result.Add(new EntityFile(relativeDirectory + "/" + Path.GetFileName(fullPath), document.RootElement.Clone()));
            }
            catch (JsonException)
            {
            }
        }
        return result;
    }

    private sealed record EntityFile(string Path, JsonElement Root);
}
