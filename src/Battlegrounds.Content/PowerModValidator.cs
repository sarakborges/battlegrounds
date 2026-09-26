using System.Text.Json;
using Battlegrounds.Core.Domain.Effects;

namespace Battlegrounds.Content;

internal sealed class PowerModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory))
            return issues;

        var powerFiles = ReadEntityDirectory(modDirectory, "content/powers", issues);
        if (powerFiles.Count == 0)
        {
            issues.Add(new("INVALID_VALUE", "content/powers", "$", "A mod must define at least one power."));
        }

        var powerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in powerFiles)
        {
            if (TryRequiredString(file.Root, "id", file.Path, "$.id", issues, out var id))
            {
                ValidateFileStem(file, id!, issues);
                if (!powerIds.Add(id!))
                    issues.Add(new("DUPLICATE_ID", file.Path, "$.id", $"Duplicate power id '{id}'."));
            }
        }

        var unitIds = ReadEntityIds(modDirectory, "content/units");
        var behaviorIds = ReadEntityIds(modDirectory, "content/behaviors");
        var typeIds = ReadEntityIds(modDirectory, "content/types");
        var tagIds = ReadEntityIds(modDirectory, "content/tags");

        foreach (var file in powerFiles)
        {
            ValidatePower(file, powerIds, unitIds, behaviorIds, typeIds, tagIds, issues);
        }

        ValidateLeaderInitialPowers(modDirectory, powerIds, issues);
        return issues;
    }

    private static void ValidatePower(
        EntityFile file,
        IReadOnlySet<string> powerIds,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> behaviorIds,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        ValidateKeys(
            file.Root,
            file.Path,
            "$",
            ["id", "name", "cost", "maxUsesPerTurn", "maxUsesPerMatch", "effects"],
            ["id", "name", "cost", "maxUsesPerTurn", "effects"],
            issues);

        TryRequiredString(file.Root, "id", file.Path, "$.id", issues, out _);
        TryRequiredString(file.Root, "name", file.Path, "$.name", issues, out _);

        if (TryRequiredInt(file.Root, "cost", file.Path, "$.cost", issues, out var cost) && cost < 0)
            issues.Add(new("INVALID_VALUE", file.Path, "$.cost", "cost cannot be negative."));
        if (TryRequiredInt(file.Root, "maxUsesPerTurn", file.Path, "$.maxUsesPerTurn", issues, out var perTurn) && perTurn <= 0)
            issues.Add(new("INVALID_VALUE", file.Path, "$.maxUsesPerTurn", "maxUsesPerTurn must be positive."));
        if (file.Root.TryGetProperty("maxUsesPerMatch", out _) &&
            TryRequiredInt(file.Root, "maxUsesPerMatch", file.Path, "$.maxUsesPerMatch", issues, out var perMatch) && perMatch <= 0)
        {
            issues.Add(new("INVALID_VALUE", file.Path, "$.maxUsesPerMatch", "maxUsesPerMatch must be positive."));
        }

        if (!file.Root.TryGetProperty("effects", out var effects)) return;
        if (effects.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("INVALID_TYPE", file.Path, "$.effects", "Expected an array."));
            return;
        }
        if (effects.GetArrayLength() == 0)
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file.Path, "$.effects", "Power requires at least one effect."));
            return;
        }

        var index = 0;
        foreach (var effect in effects.EnumerateArray())
        {
            ValidateEffect(
                file.Path,
                effect,
                $"$.effects[{index}]",
                powerIds,
                unitIds,
                behaviorIds,
                typeIds,
                tagIds,
                issues);
            index++;
        }
    }

    private static void ValidateLeaderInitialPowers(
        string modDirectory,
        IReadOnlySet<string> powerIds,
        List<ModValidationIssue> issues)
    {
        var leaders = ReadEntityDirectory(modDirectory, "content/leaders", issues, reportMissing: false);
        foreach (var leader in leaders)
        {
            if (!leader.Root.TryGetProperty("initialPowerId", out _))
            {
                issues.Add(new(
                    "MISSING_REQUIRED_KEY",
                    leader.Path,
                    "$.initialPowerId",
                    "Required key 'initialPowerId' is missing."));
                continue;
            }

            if (TryRequiredString(leader.Root, "initialPowerId", leader.Path, "$.initialPowerId", issues, out var powerId) &&
                !powerIds.Contains(powerId!))
            {
                issues.Add(new(
                    "UNKNOWN_REFERENCE",
                    leader.Path,
                    "$.initialPowerId",
                    $"Unknown power '{powerId}'."));
            }
        }
    }

    private static void ValidateEffect(
        string file,
        JsonElement effect,
        string path,
        IReadOnlySet<string> powerIds,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> behaviorIds,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (effect.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
            return;
        }

        if (!TryRequiredString(effect, "kind", file, path + ".kind", issues, out var kind))
        {
            ValidateKeys(effect, file, path, ["kind"], ["kind"], issues);
            return;
        }

        switch (kind)
        {
            case "modifyStats":
                ValidateKeys(effect, file, path, ["kind", "target", "attack", "health"], ["kind"], issues);
                ValidateTarget(file, effect, path, typeIds, tagIds, issues);
                var hasAttack = TryOptionalInt(effect, "attack", file, path + ".attack", issues, out var attack);
                var hasHealth = TryOptionalInt(effect, "health", file, path + ".health", issues, out var health);
                if (!effect.TryGetProperty("attack", out _) && !effect.TryGetProperty("health", out _))
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path, "modifyStats requires 'attack' and/or 'health'."));
                else if ((!hasAttack || attack == 0) && (!hasHealth || health == 0))
                    issues.Add(new("INVALID_VALUE", file, path, "modifyStats requires a non-zero attack or health delta."));
                break;

            case "dealDamage":
                ValidateKeys(effect, file, path, ["kind", "target", "amount"], ["kind"], issues);
                ValidateTarget(file, effect, path, typeIds, tagIds, issues);
                if (RequireParameterInt(file, effect, "amount", path, issues, out var damage) && damage <= 0)
                    issues.Add(new("INVALID_VALUE", file, path + ".amount", "amount must be positive."));
                break;

            case "destroyUnit":
                ValidateKeys(effect, file, path, ["kind", "target"], ["kind"], issues);
                ValidateTarget(file, effect, path, typeIds, tagIds, issues);
                break;

            case "triggerEvent":
                ValidateKeys(effect, file, path, ["kind", "target", "event"], ["kind"], issues);
                ValidateTarget(file, effect, path, typeIds, tagIds, issues);
                if (RequireParameterString(file, effect, "event", path, issues, out var eventName) &&
                    !NativeTriggerKeys.IsSupported(new NativeTriggerKey(eventName!)))
                {
                    issues.Add(new("UNSUPPORTED_TRIGGER", file, path + ".event", $"Trigger event '{eventName}' is not supported."));
                }
                break;

            case "summonUnit":
                ValidateKeys(effect, file, path, ["kind", "unitId", "count"], ["kind"], issues);
                if (RequireParameterString(file, effect, "unitId", path, issues, out var unitId) && !unitIds.Contains(unitId!))
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".unitId", $"Unknown unit '{unitId}'."));
                if (TryOptionalInt(effect, "count", file, path + ".count", issues, out var count) && count <= 0)
                    issues.Add(new("INVALID_VALUE", file, path + ".count", "count must be positive."));
                break;

            case "addBehavior":
            case "removeBehavior":
                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId"], ["kind"], issues);
                ValidateTarget(file, effect, path, typeIds, tagIds, issues);
                if (RequireParameterString(file, effect, "behaviorId", path, issues, out var behaviorId) &&
                    !behaviorIds.Contains(behaviorId!))
                {
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".behaviorId", $"Unknown behavior '{behaviorId}'."));
                }
                break;

            case "addResource":
                ValidateKeys(effect, file, path, ["kind", "amount"], ["kind"], issues);
                if (RequireParameterInt(file, effect, "amount", path, issues, out var amount) && amount == 0)
                    issues.Add(new("INVALID_VALUE", file, path + ".amount", "amount cannot be zero."));
                break;

            case "setPower":
                ValidateKeys(effect, file, path, ["kind", "powerId"], ["kind"], issues);
                if (RequireParameterString(file, effect, "powerId", path, issues, out var nextPowerId) &&
                    !powerIds.Contains(nextPowerId!))
                {
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".powerId", $"Unknown power '{nextPowerId}'."));
                }
                break;

            default:
                ValidateKeys(effect, file, path, ["kind"], ["kind"], issues);
                issues.Add(new("UNSUPPORTED_EFFECT", file, path + ".kind", $"Effect kind '{kind}' is not supported."));
                break;
        }
    }

    private static void ValidateTarget(
        string file,
        JsonElement effect,
        string path,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (!effect.TryGetProperty("target", out var target))
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + ".target", "Effect requires 'target'."));
            return;
        }
        if (target.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path + ".target", "Expected an object."));
            return;
        }

        var targetPath = path + ".target";
        ValidateKeys(target, file, targetPath, ["scope", "typeId", "tagId"], ["scope"], issues);
        if (TryRequiredString(target, "scope", file, targetPath + ".scope", issues, out var scope) &&
            scope is not ("selected" or "randomFriendly" or "randomEnemy" or "allFriendly" or "allEnemy"))
        {
            issues.Add(new(
                "INVALID_VALUE",
                file,
                targetPath + ".scope",
                $"Power target scope '{scope}' is not supported. Use selected/randomFriendly/randomEnemy/allFriendly/allEnemy."));
        }

        if (target.TryGetProperty("typeId", out _) &&
            TryRequiredString(target, "typeId", file, targetPath + ".typeId", issues, out var typeId) &&
            !typeIds.Contains(typeId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, targetPath + ".typeId", $"Unknown unit type '{typeId}'."));
        }
        if (target.TryGetProperty("tagId", out _) &&
            TryRequiredString(target, "tagId", file, targetPath + ".tagId", issues, out var tagId) &&
            !tagIds.Contains(tagId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, targetPath + ".tagId", $"Unknown tag '{tagId}'."));
        }
    }

    private static HashSet<string> ReadEntityIds(string root, string relativeDirectory)
    {
        var directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory)) return [];
        return Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IReadOnlyList<EntityFile> ReadEntityDirectory(
        string root,
        string relativeDirectory,
        List<ModValidationIssue> issues,
        bool reportMissing = true)
    {
        var directory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(directory))
        {
            if (reportMissing)
            {
                issues.Add(new(
                    "MISSING_REQUIRED_DIRECTORY",
                    relativeDirectory,
                    "$",
                    $"Required directory '{relativeDirectory}' is missing."));
            }
            return [];
        }

        var result = new List<EntityFile>();
        foreach (var fullPath in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(fullPath);
            var relativePath = relativeDirectory + "/" + fileName;
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new("INVALID_TYPE", relativePath, "$", "Entity file must contain one JSON object."));
                    continue;
                }
                result.Add(new EntityFile(relativePath, Path.GetFileNameWithoutExtension(fileName), document.RootElement.Clone()));
            }
            catch (JsonException exception)
            {
                issues.Add(new("INVALID_JSON", relativePath, exception.Path ?? "$", "File contains invalid JSON."));
            }
        }
        return result;
    }

    private static void ValidateFileStem(EntityFile file, string id, List<ModValidationIssue> issues)
    {
        if (!string.Equals(file.Stem, id, StringComparison.Ordinal))
        {
            issues.Add(new(
                "ID_FILENAME_MISMATCH",
                file.Path,
                "$.id",
                $"Entity id '{id}' must match file name '{file.Stem}.json'."));
        }
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
            issues.Add(new("MISSING_REQUIRED_KEY", file, path + "." + property, $"Required key '{property}' is missing."));
    }

    private static bool TryRequiredString(
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

    private static bool TryRequiredInt(
        JsonElement parent,
        string property,
        string file,
        string path,
        List<ModValidationIssue> issues,
        out int value)
    {
        value = default;
        if (!parent.TryGetProperty(property, out var element)) return false;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value)) return true;
        issues.Add(new("INVALID_TYPE", file, path, "Expected an integer."));
        return false;
    }

    private static bool TryOptionalInt(
        JsonElement parent,
        string property,
        string file,
        string path,
        List<ModValidationIssue> issues,
        out int value)
    {
        value = default;
        if (!parent.TryGetProperty(property, out _)) return false;
        return TryRequiredInt(parent, property, file, path, issues, out value);
    }

    private static bool RequireParameterString(
        string file,
        JsonElement effect,
        string property,
        string path,
        List<ModValidationIssue> issues,
        out string? value)
    {
        value = null;
        if (!effect.TryGetProperty(property, out _))
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Effect requires '{property}'."));
            return false;
        }
        return TryRequiredString(effect, property, file, path + "." + property, issues, out value);
    }

    private static bool RequireParameterInt(
        string file,
        JsonElement effect,
        string property,
        string path,
        List<ModValidationIssue> issues,
        out int value)
    {
        value = default;
        if (!effect.TryGetProperty(property, out _))
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Effect requires '{property}'."));
            return false;
        }
        return TryRequiredInt(effect, property, file, path + "." + property, issues, out value);
    }

    private sealed record EntityFile(string Path, string Stem, JsonElement Root);
}
