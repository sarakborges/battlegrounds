using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class UnitAuraModValidator
{
    private static readonly JsonDocumentOptions Options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
    private static readonly HashSet<string> Selections = ["all", "adjacent", "leftAdjacent", "rightAdjacent"];
    private static readonly HashSet<string> ContinuousBehaviorHandlers = ["targetPriority", "extraAttack"];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;
        var typeIds = ReadIds(Path.Combine(modDirectory, "content", "types"));
        var tagIds = ReadIds(Path.Combine(modDirectory, "content", "tags"));
        var behaviorHandlers = ReadBehaviorHandlers(Path.Combine(modDirectory, "content", "behaviors"));
        var units = Path.Combine(modDirectory, "content", "units");
        if (!Directory.Exists(units)) return issues;

        foreach (var path in Directory.GetFiles(units, "*.json", SearchOption.TopDirectoryOnly))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), Options);
            if (!doc.RootElement.TryGetProperty("auras", out var auras)) continue;
            var file = Path.GetRelativePath(modDirectory, path).Replace('\\', '/');
            if (auras.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new("INVALID_TYPE", file, "$.auras", "auras must be an array."));
                continue;
            }

            var index = 0;
            foreach (var aura in auras.EnumerateArray())
            {
                var basePath = $"$.auras[{index}]";
                if (aura.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new("INVALID_TYPE", file, basePath, "Aura must be an object."));
                    index++;
                    continue;
                }
                var allowed = new HashSet<string>(["target", "attack", "health", "behaviorIds"], StringComparer.Ordinal);
                foreach (var property in aura.EnumerateObject())
                    if (!allowed.Contains(property.Name)) issues.Add(new("UNKNOWN_PROPERTY", file, basePath + "." + property.Name, $"Unknown aura property '{property.Name}'."));

                var attack = ReadNonNegativeInt(aura, "attack", file, basePath, issues);
                var health = ReadNonNegativeInt(aura, "health", file, basePath, issues);
                var behaviorCount = ValidateBehaviorIds(aura, behaviorHandlers, file, basePath, issues);
                if (attack == 0 && health == 0 && behaviorCount == 0)
                    issues.Add(new("INVALID_VALUE", file, basePath, "Aura requires a positive stat bonus or at least one behaviorId."));

                if (!aura.TryGetProperty("target", out var target) || target.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file, basePath + ".target", "Aura requires a target object."));
                    index++;
                    continue;
                }
                ValidateTarget(target, file, basePath + ".target", typeIds, tagIds, issues);
                index++;
            }
        }
        return issues;
    }

    private static int ValidateBehaviorIds(
        JsonElement aura,
        IReadOnlyDictionary<string, string> behaviorHandlers,
        string file,
        string path,
        List<ModValidationIssue> issues)
    {
        if (!aura.TryGetProperty("behaviorIds", out var behaviors)) return 0;
        if (behaviors.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("INVALID_TYPE", file, path + ".behaviorIds", "behaviorIds must be an array."));
            return 0;
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenHandlers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var behavior in behaviors.EnumerateArray())
        {
            var itemPath = $"{path}.behaviorIds[{index}]";
            if (behavior.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(behavior.GetString()))
            {
                issues.Add(new("INVALID_TYPE", file, itemPath, "Behavior aura reference must be a non-empty string."));
                index++;
                continue;
            }

            var id = behavior.GetString()!;
            if (!seenIds.Add(id))
                issues.Add(new("DUPLICATE_REFERENCE", file, itemPath, $"Behavior '{id}' is referenced more than once by this aura."));
            if (!behaviorHandlers.TryGetValue(id, out var handler))
            {
                issues.Add(new("UNKNOWN_REFERENCE", file, itemPath, $"Unknown behavior '{id}'."));
            }
            else
            {
                if (!seenHandlers.Add(handler))
                    issues.Add(new("DUPLICATE_NATIVE_BEHAVIOR", file, itemPath, $"Native behavior handler '{handler}' is granted more than once by this aura."));
                if (!ContinuousBehaviorHandlers.Contains(handler))
                    issues.Add(new("INVALID_VALUE", file, itemPath, $"Behavior aura cannot grant consuming handler '{handler}'. Only targetPriority and extraAttack are currently supported."));
            }
            index++;
        }
        return behaviors.GetArrayLength();
    }

    private static void ValidateTarget(JsonElement target, string file, string path, IReadOnlySet<string> typeIds, IReadOnlySet<string> tagIds, List<ModValidationIssue> issues)
    {
        var allowed = new HashSet<string>(["scope", "selection", "excludeSource", "typeId", "tagId", "relativeTo", "limit"], StringComparer.Ordinal);
        foreach (var property in target.EnumerateObject())
            if (!allowed.Contains(property.Name)) issues.Add(new("UNKNOWN_PROPERTY", file, path + "." + property.Name, $"Unknown aura target property '{property.Name}'."));

        if (!target.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.String || scope.GetString() != "friendly")
            issues.Add(new("INVALID_VALUE", file, path + ".scope", "Aura target scope must be 'friendly'."));
        if (target.TryGetProperty("selection", out var selection) && (selection.ValueKind != JsonValueKind.String || !Selections.Contains(selection.GetString()!)))
            issues.Add(new("INVALID_VALUE", file, path + ".selection", "Aura selection must be all or source-relative adjacent."));
        if (target.TryGetProperty("relativeTo", out var relativeTo) && (relativeTo.ValueKind != JsonValueKind.String || relativeTo.GetString() != "source"))
            issues.Add(new("INVALID_VALUE", file, path + ".relativeTo", "Aura targets are source-relative."));
        if (target.TryGetProperty("limit", out _))
            issues.Add(new("INVALID_VALUE", file, path + ".limit", "Aura targets do not support limit."));
        if (target.TryGetProperty("excludeSource", out var exclude) && exclude.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            issues.Add(new("INVALID_TYPE", file, path + ".excludeSource", "excludeSource must be boolean."));
        ValidateReference(target, "typeId", typeIds, "unit type", file, path, issues);
        ValidateReference(target, "tagId", tagIds, "tag", file, path, issues);
    }

    private static int ReadNonNegativeInt(JsonElement aura, string name, string file, string path, List<ModValidationIssue> issues)
    {
        if (!aura.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            issues.Add(new("INVALID_TYPE", file, path + "." + name, $"{name} must be an integer."));
            return 0;
        }
        if (result < 0) issues.Add(new("INVALID_VALUE", file, path + "." + name, $"{name} cannot be negative in the current aura surface."));
        return result;
    }

    private static void ValidateReference(JsonElement target, string name, IReadOnlySet<string> known, string label, string file, string path, List<ModValidationIssue> issues)
    {
        if (!target.TryGetProperty(name, out var value)) return;
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            issues.Add(new("INVALID_TYPE", file, path + "." + name, $"{name} must be a non-empty string."));
            return;
        }
        if (!known.Contains(value.GetString()!)) issues.Add(new("UNKNOWN_REFERENCE", file, path + "." + name, $"Unknown {label} '{value.GetString()}'."));
    }

    private static HashSet<string> ReadIds(string directory)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(directory)) return ids;
        foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), Options);
            if (doc.RootElement.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString())) ids.Add(id.GetString()!);
        }
        return ids;
    }

    private static Dictionary<string, string> ReadBehaviorHandlers(string directory)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Directory.Exists(directory)) return result;
        foreach (var path in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path), Options);
            if (!doc.RootElement.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString())) continue;
            if (!doc.RootElement.TryGetProperty("handler", out var handler) || handler.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(handler.GetString())) continue;
            result[id.GetString()!] = handler.GetString()!;
        }
        return result;
    }
}
