using System.Text.Json;
using Battlegrounds.Core.Domain.Effects;

namespace Battlegrounds.Content;

internal sealed class PowerLifecycleModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> PowerEvents =
    [
        "onActivate",
        "onMatchStart",
        "onTurnStart",
        "onTurnEnd",
        "onCombatStart",
        "onCombatEnd",
        "afterEventCount",
    ];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var powers = ReadEntityDirectory(modDirectory, "content/powers", issues);
        if (powers.Count == 0)
            issues.Add(new("INVALID_VALUE", "content/powers", "$", "A mod must define at least one power."));

        var powerIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in powers)
        {
            if (!TryRequiredString(file.Root, "id", file.Path, "$.id", issues, out var id)) continue;
            if (!string.Equals(file.Stem, id, StringComparison.Ordinal))
                issues.Add(new("ID_FILENAME_MISMATCH", file.Path, "$.id", $"Entity id '{id}' must match file name '{file.Stem}.json'."));
            if (!powerIds.Add(id!))
                issues.Add(new("DUPLICATE_ID", file.Path, "$.id", $"Duplicate power id '{id}'."));
        }

        var unitIds = ReadEntityIds(modDirectory, "content/units");
        var behaviorIds = ReadEntityIds(modDirectory, "content/behaviors");
        var typeIds = ReadEntityIds(modDirectory, "content/types");
        var tagIds = ReadEntityIds(modDirectory, "content/tags");

        foreach (var power in powers)
            ValidatePower(power, powerIds, unitIds, behaviorIds, typeIds, tagIds, issues);

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
        ValidateKeys(file.Root, file.Path, "$", ["id", "name", "activation", "triggers"], ["id", "name", "triggers"], issues);
        TryRequiredString(file.Root, "id", file.Path, "$.id", issues, out _);
        TryRequiredString(file.Root, "name", file.Path, "$.name", issues, out _);

        var hasActivation = file.Root.TryGetProperty("activation", out var activation);
        if (hasActivation)
            ValidateActivation(file.Path, activation, issues);

        if (!file.Root.TryGetProperty("triggers", out var triggers)) return;
        if (triggers.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("INVALID_TYPE", file.Path, "$.triggers", "Expected an array."));
            return;
        }
        if (triggers.GetArrayLength() == 0)
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file.Path, "$.triggers", "Power requires at least one trigger."));
            return;
        }

        var activationTriggerCount = 0;
        var index = 0;
        foreach (var trigger in triggers.EnumerateArray())
        {
            var path = $"$.triggers[{index}]";
            var eventName = ValidateTrigger(
                file.Path,
                trigger,
                path,
                powerIds,
                unitIds,
                behaviorIds,
                typeIds,
                tagIds,
                issues);
            if (eventName == "onActivate") activationTriggerCount++;
            index++;
        }

        if (hasActivation && activationTriggerCount != 1)
        {
            issues.Add(new(
                "INVALID_POWER_ACTIVATION",
                file.Path,
                "$.triggers",
                "An activatable power requires exactly one onActivate trigger."));
        }
        else if (!hasActivation && activationTriggerCount != 0)
        {
            issues.Add(new(
                "INVALID_POWER_ACTIVATION",
                file.Path,
                "$.triggers",
                "A passive-only power cannot define onActivate without an activation object."));
        }
    }

    private static void ValidateActivation(string file, JsonElement activation, List<ModValidationIssue> issues)
    {
        if (activation.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, "$.activation", "Expected an object."));
            return;
        }

        ValidateKeys(
            activation,
            file,
            "$.activation",
            ["cost", "maxUsesPerTurn", "maxUsesPerMatch"],
            ["cost", "maxUsesPerTurn"],
            issues);

        if (TryRequiredInt(activation, "cost", file, "$.activation.cost", issues, out var cost) && cost < 0)
            issues.Add(new("INVALID_VALUE", file, "$.activation.cost", "cost cannot be negative."));
        if (TryRequiredInt(activation, "maxUsesPerTurn", file, "$.activation.maxUsesPerTurn", issues, out var perTurn) && perTurn <= 0)
            issues.Add(new("INVALID_VALUE", file, "$.activation.maxUsesPerTurn", "maxUsesPerTurn must be positive."));
        if (activation.TryGetProperty("maxUsesPerMatch", out _) &&
            TryRequiredInt(activation, "maxUsesPerMatch", file, "$.activation.maxUsesPerMatch", issues, out var perMatch) &&
            perMatch <= 0)
        {
            issues.Add(new("INVALID_VALUE", file, "$.activation.maxUsesPerMatch", "maxUsesPerMatch must be positive."));
        }
    }

    private static string? ValidateTrigger(
        string file,
        JsonElement trigger,
        string path,
        IReadOnlySet<string> powerIds,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> behaviorIds,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (trigger.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
            return null;
        }

        ValidateKeys(trigger, file, path, ["event", "effects", "count", "counter", "conditions", "activationLimit"], ["event", "effects"], issues);
        if (!TryRequiredString(trigger, "event", file, path + ".event", issues, out var eventName)) return null;
        if (!PowerEvents.Contains(eventName!))
            issues.Add(new("UNSUPPORTED_TRIGGER", file, path + ".event", $"Power trigger '{eventName}' is not supported."));

        if (!trigger.TryGetProperty("effects", out var effects)) return eventName;
        if (effects.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("INVALID_TYPE", file, path + ".effects", "Expected an array."));
            return eventName;
        }
        if (effects.GetArrayLength() == 0)
        {
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + ".effects", "Trigger requires at least one effect."));
            return eventName;
        }

        var effectIndex = 0;
        foreach (var effect in effects.EnumerateArray())
        {
            ValidateEffect(
                file,
                effect,
                $"{path}.effects[{effectIndex}]",
                eventName!,
                powerIds,
                unitIds,
                behaviorIds,
                typeIds,
                tagIds,
                issues);
            effectIndex++;
        }
        return eventName;
    }

    private static void ValidateEffect(
        string file,
        JsonElement effect,
        string path,
        string triggerEvent,
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
        if (!TryRequiredString(effect, "kind", file, path + ".kind", issues, out var kind)) return;

        switch (kind)
        {
            case "modifyStats":
                ValidateKeys(effect, file, path, ["kind", "target", "attack", "health"], ["kind", "target"], issues);
                ValidateTarget(file, effect, path, triggerEvent, typeIds, tagIds, issues);
                if (!effect.TryGetProperty("attack", out _) && !effect.TryGetProperty("health", out _))
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path, "modifyStats requires attack and/or health."));
                break;

            case "dealDamage":
                ValidateKeys(effect, file, path, ["kind", "target", "amount"], ["kind", "target", "amount"], issues);
                ValidateTarget(file, effect, path, triggerEvent, typeIds, tagIds, issues);
                break;

            case "destroyUnit":
                ValidateKeys(effect, file, path, ["kind", "target"], ["kind", "target"], issues);
                ValidateTarget(file, effect, path, triggerEvent, typeIds, tagIds, issues);
                break;

            case "triggerEvent":
                ValidateKeys(effect, file, path, ["kind", "target", "event"], ["kind", "target", "event"], issues);
                ValidateTarget(file, effect, path, triggerEvent, typeIds, tagIds, issues);
                if (TryRequiredString(effect, "event", file, path + ".event", issues, out var eventName) &&
                    !NativeTriggerKeys.IsSupported(new NativeTriggerKey(eventName!)))
                {
                    issues.Add(new("UNSUPPORTED_TRIGGER", file, path + ".event", $"Trigger event '{eventName}' is not supported."));
                }
                break;

            case "summonUnit":
                ValidateKeys(effect, file, path, ["kind", "unitId", "count"], ["kind", "unitId"], issues);
                if (TryRequiredString(effect, "unitId", file, path + ".unitId", issues, out var unitId) && !unitIds.Contains(unitId!))
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".unitId", $"Unknown unit '{unitId}'."));
                break;

            case "addBehavior":
            case "removeBehavior":
                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId"], ["kind", "target", "behaviorId"], issues);
                ValidateTarget(file, effect, path, triggerEvent, typeIds, tagIds, issues);
                if (TryRequiredString(effect, "behaviorId", file, path + ".behaviorId", issues, out var behaviorId) &&
                    !behaviorIds.Contains(behaviorId!))
                {
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".behaviorId", $"Unknown behavior '{behaviorId}'."));
                }
                break;

            case "addResource":
            case "adjustUpgradeCost":
            case "addAcquireDiscount":
                ValidateKeys(effect, file, path, ["kind", "amount"], ["kind", "amount"], issues);
                break;

            case "refreshOffer":
                ValidateKeys(effect, file, path, ["kind"], ["kind"], issues);
                break;

            case "setPower":
                ValidateKeys(effect, file, path, ["kind", "powerId"], ["kind", "powerId"], issues);
                if (TryRequiredString(effect, "powerId", file, path + ".powerId", issues, out var powerId) &&
                    !powerIds.Contains(powerId!))
                {
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".powerId", $"Unknown power '{powerId}'."));
                }
                break;

            case "transformUnit":
            case "copyUnitToReserve":
            case "applyUnitModifier":
            case "removeUnitModifier":
                // Persistent mutation schemas are owned by PersistentUnitMutationModValidator.
                break;
            case "generateUnitToReserve":
            case "generateUnitChoice":
            case "generateActionToReserve":
            case "generateActionChoice":
                // Generation schemas are owned by GenerationChoiceModValidator and ActionModValidator.
                break;
            default:
                issues.Add(new("UNSUPPORTED_EFFECT", file, path + ".kind", $"Effect kind '{kind}' is not supported."));
                break;
        }
    }

    private static void ValidateTarget(
        string file,
        JsonElement effect,
        string path,
        string triggerEvent,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (!effect.TryGetProperty("target", out var target)) return;
        if (target.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path + ".target", "Expected an object."));
            return;
        }

        var targetPath = path + ".target";
        ValidateKeys(
            target,
            file,
            targetPath,
            ["scope", "selection", "excludeSource", "limit", "typeId", "tagId", "relativeTo"],
            ["scope"],
            issues);
        TryRequiredString(target, "scope", file, targetPath + ".scope", issues, out _);

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

    private static void ValidateLeaderInitialPowers(
        string modDirectory,
        IReadOnlySet<string> powerIds,
        List<ModValidationIssue> issues)
    {
        foreach (var leader in ReadEntityDirectory(modDirectory, "content/leaders", issues, reportMissing: false))
        {
            if (!leader.Root.TryGetProperty("initialPowerId", out _))
            {
                issues.Add(new("MISSING_REQUIRED_KEY", leader.Path, "$.initialPowerId", "Required key 'initialPowerId' is missing."));
                continue;
            }
            if (TryRequiredString(leader.Root, "initialPowerId", leader.Path, "$.initialPowerId", issues, out var powerId) &&
                !powerIds.Contains(powerId!))
            {
                issues.Add(new("UNKNOWN_REFERENCE", leader.Path, "$.initialPowerId", $"Unknown power '{powerId}'."));
            }
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
                issues.Add(new("MISSING_REQUIRED_DIRECTORY", relativeDirectory, "$", $"Required directory '{relativeDirectory}' is missing."));
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
        value = 0;
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
        value = 0;
        if (!parent.TryGetProperty(property, out var element)) return false;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value)) return true;
        issues.Add(new("INVALID_TYPE", file, path, "Expected an integer."));
        return false;
    }

    private sealed record EntityFile(string Path, string Stem, JsonElement Root);
}
