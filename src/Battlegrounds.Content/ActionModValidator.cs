using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class ActionModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> SupportedEffects =
    [
        "modifyStats", "dealDamage", "destroyUnit", "triggerEvent", "summonUnit",
        "generateUnitToReserve", "generateUnitChoice", "generateActionToReserve", "generateActionChoice",
        "transformUnit", "copyUnitToReserve", "applyUnitModifier", "removeUnitModifier",
        "addBehavior", "removeBehavior", "addResource", "adjustUpgradeCost", "addAcquireDiscount", "refreshOffer", "setPower",
    ];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var actionFiles = ReadEntityDirectory(modDirectory, "content/actions");
        if (actionFiles.Count == 0)
        {
            issues.Add(new("INVALID_VALUE", "content/actions", "$", "A mod must define at least one action."));
            return issues;
        }

        var unitIds = ReadEntityIds(modDirectory, "content/units");
        var behaviorIds = ReadEntityIds(modDirectory, "content/behaviors");
        var powerIds = ReadEntityIds(modDirectory, "content/powers");
        var actionIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in actionFiles)
        {
            ValidateKeys(file.Root, file.Path, "$", ["id", "name", "tier", "cost", "effects"], ["id", "name", "tier", "cost", "effects"], issues);
            if (TryString(file.Root, "id", file.Path, "$.id", issues, out var id))
            {
                if (!string.Equals(Path.GetFileNameWithoutExtension(file.Path), id, StringComparison.Ordinal))
                    issues.Add(new("ID_FILENAME_MISMATCH", file.Path, "$.id", $"Entity id '{id}' must match file name '{Path.GetFileNameWithoutExtension(file.Path)}.json'."));
                if (!actionIds.Add(id!)) issues.Add(new("DUPLICATE_ID", file.Path, "$.id", $"Duplicate action id '{id}'."));
            }
            TryString(file.Root, "name", file.Path, "$.name", issues, out _);
            if (TryInt(file.Root, "tier", file.Path, "$.tier", issues, out var tier) && tier <= 0)
                issues.Add(new("INVALID_VALUE", file.Path, "$.tier", "tier must be positive."));
            if (TryInt(file.Root, "cost", file.Path, "$.cost", issues, out var cost) && cost < 0)
                issues.Add(new("INVALID_VALUE", file.Path, "$.cost", "cost cannot be negative."));
        }

        foreach (var file in actionFiles)
        {
            if (!file.Root.TryGetProperty("effects", out var effects)) continue;
            ValidateEffects(file.Path, effects, "$.effects", unitIds, actionIds, behaviorIds, powerIds, issues);
        }

        ValidateActionGenerationInTriggers(modDirectory, actionIds, issues);
        ValidatePreparationRules(modDirectory, actionFiles.Count, issues);
        return issues;
    }

    private static void ValidateActionGenerationInTriggers(
        string root,
        IReadOnlySet<string> actionIds,
        List<ModValidationIssue> issues)
    {
        foreach (var pair in new[] { (Dir: "content/units", Power: false), (Dir: "content/powers", Power: true) })
        {
            foreach (var file in ReadEntityDirectory(root, pair.Dir))
            {
                if (!file.Root.TryGetProperty("triggers", out var triggers) || triggers.ValueKind != JsonValueKind.Array) continue;
                var triggerIndex = 0;
                foreach (var trigger in triggers.EnumerateArray())
                {
                    var eventName = trigger.ValueKind == JsonValueKind.Object && trigger.TryGetProperty("event", out var eventValue) && eventValue.ValueKind == JsonValueKind.String
                        ? eventValue.GetString()
                        : null;
                    if (trigger.ValueKind == JsonValueKind.Object && trigger.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Array)
                    {
                        var effectIndex = 0;
                        foreach (var effect in effects.EnumerateArray())
                        {
                            if (effect.ValueKind == JsonValueKind.Object && effect.TryGetProperty("kind", out var kindValue) && kindValue.ValueKind == JsonValueKind.String)
                            {
                                var kind = kindValue.GetString();
                                if (kind is "generateActionToReserve" or "generateActionChoice")
                                {
                                    var allowed = pair.Power
                                        ? eventName is "onActivate" or "onMatchStart" or "onTurnStart" or "onTurnEnd"
                                        : eventName is "onAcquire" or "onPlay" or "onTurnStart" or "onTurnEnd";
                                    var path = $"$.triggers[{triggerIndex}].effects[{effectIndex}]";
                                    if (kind == "generateActionChoice" && eventName == "onTurnEnd")
                                        allowed = false;
                                    if (!allowed)
                                    {
                                        var message = kind == "generateActionChoice" && eventName == "onTurnEnd"
                                            ? "Pending Action choices are not valid in onTurnEnd because preparation must transition cleanly to combat."
                                            : "Action generation is only valid in preparation-only triggers.";
                                        issues.Add(new("INVALID_EFFECT_CONTEXT", file.Path, path + ".kind", message));
                                    }
                                    ValidateActionGenerationEffect(file.Path, effect, path, actionIds, issues);
                                }
                            }
                            effectIndex++;
                        }
                    }
                    triggerIndex++;
                }
            }
        }
    }

    private static void ValidateEffects(
        string file,
        JsonElement effects,
        string path,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> actionIds,
        IReadOnlySet<string> behaviorIds,
        IReadOnlySet<string> powerIds,
        List<ModValidationIssue> issues)
    {
        if (effects.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an array."));
            return;
        }
        if (effects.GetArrayLength() == 0)
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path, "Action requires at least one effect."));

        var index = 0;
        foreach (var effect in effects.EnumerateArray())
        {
            var effectPath = $"{path}[{index}]";
            if (effect.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", file, effectPath, "Expected an object."));
                index++;
                continue;
            }
            if (!TryString(effect, "kind", file, effectPath + ".kind", issues, out var kind))
            {
                index++;
                continue;
            }
            if (!SupportedEffects.Contains(kind!))
                issues.Add(new("UNSUPPORTED_EFFECT", file, effectPath + ".kind", $"Effect kind '{kind}' is not supported."));

            switch (kind)
            {
                case "summonUnit":
                case "generateUnitToReserve":
                    ValidateReference(effect, "unitId", file, effectPath, unitIds, "unit", issues);
                    break;
                case "generateActionToReserve":
                case "generateActionChoice":
                    ValidateActionGenerationEffect(file, effect, effectPath, actionIds, issues);
                    break;
                case "addBehavior":
                case "removeBehavior":
                    ValidateReference(effect, "behaviorId", file, effectPath, behaviorIds, "behavior", issues);
                    break;
                case "setPower":
                    ValidateReference(effect, "powerId", file, effectPath, powerIds, "power", issues);
                    break;
            }

            if (kind is "modifyStats" or "dealDamage" or "destroyUnit" or "triggerEvent" or "addBehavior" or "removeBehavior")
            {
                if (!effect.TryGetProperty("target", out var target) || target.ValueKind != JsonValueKind.Object)
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file, effectPath + ".target", "Effect requires a target object."));
            }
            index++;
        }
    }

    private static void ValidateActionGenerationEffect(
        string file,
        JsonElement effect,
        string path,
        IReadOnlySet<string> actionIds,
        List<ModValidationIssue> issues)
    {
        var kind = effect.TryGetProperty("kind", out var kindValue) ? kindValue.GetString() : null;
        if (kind == "generateActionToReserve")
        {
            ValidateKeys(effect, file, path, ["kind", "actionId", "count"], ["kind", "actionId"], issues);
            ValidateReference(effect, "actionId", file, path, actionIds, "action", issues);
            return;
        }
        if (kind != "generateActionChoice") return;

        ValidateKeys(effect, file, path, ["kind", "actionQuery", "optionCount"], ["kind", "actionQuery"], issues);
        if (effect.TryGetProperty("optionCount", out var optionCount) &&
            (optionCount.ValueKind != JsonValueKind.Number || !optionCount.TryGetInt32(out var optionValue) || optionValue <= 0))
            issues.Add(new("INVALID_VALUE", file, path + ".optionCount", "optionCount must be a positive integer."));
        if (!effect.TryGetProperty("actionQuery", out var query)) return;
        if (query.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path + ".actionQuery", "Expected an object."));
            return;
        }
        ValidateKeys(query, file, path + ".actionQuery", ["minimumTier", "maximumTier", "excludeActionId"], [], issues);
        var minimum = OptionalPositiveInt(query, "minimumTier", file, path + ".actionQuery", issues);
        var maximum = OptionalPositiveInt(query, "maximumTier", file, path + ".actionQuery", issues);
        if (minimum is not null && maximum is not null && minimum > maximum)
            issues.Add(new("INVALID_VALUE", file, path + ".actionQuery", "minimumTier cannot exceed maximumTier."));
        if (query.TryGetProperty("excludeActionId", out _))
            ValidateReference(query, "excludeActionId", file, path + ".actionQuery", actionIds, "action", issues);
    }

    private static void ValidatePreparationRules(string root, int actionCount, List<ModValidationIssue> issues)
    {
        var path = Path.Combine(root, "rules", "preparation.json");
        if (!File.Exists(path)) return;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), DocumentOptions);
            var json = document.RootElement;
            if (!json.TryGetProperty("actionOfferSizesByTier", out var actionSizes)) return;
            if (actionSizes.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new("INVALID_TYPE", "rules/preparation.json", "$.actionOfferSizesByTier", "Expected an array."));
                return;
            }
            if (!json.TryGetProperty("offerSizesByTier", out var totalSizes) || totalSizes.ValueKind != JsonValueKind.Array) return;
            var total = totalSizes.EnumerateArray().Select(value => value.TryGetInt32(out var parsed) ? parsed : -1).ToArray();
            var actions = actionSizes.EnumerateArray().Select(value => value.TryGetInt32(out var parsed) ? parsed : -1).ToArray();
            if (actions.Length != total.Length)
                issues.Add(new("INVALID_VALUE", "rules/preparation.json", "$.actionOfferSizesByTier", "actionOfferSizesByTier must have one entry per tier."));
            for (var index = 0; index < Math.Min(actions.Length, total.Length); index++)
            {
                if (actions[index] < 0 || actions[index] > total[index])
                    issues.Add(new("INVALID_VALUE", "rules/preparation.json", $"$.actionOfferSizesByTier[{index}]", "Action offer size must be between zero and total offer size."));
                if (actions[index] > actionCount)
                    issues.Add(new("INVALID_VALUE", "rules/preparation.json", $"$.actionOfferSizesByTier[{index}]", "Action offer size exceeds the number of distinct action definitions."));
            }
        }
        catch (JsonException) { }
    }

    private static void ValidateReference(JsonElement parent, string property, string file, string path, IReadOnlySet<string> known, string label, List<ModValidationIssue> issues)
    {
        if (TryString(parent, property, file, path + "." + property, issues, out var id) && !known.Contains(id!))
            issues.Add(new("UNKNOWN_REFERENCE", file, path + "." + property, $"Unknown {label} '{id}'."));
    }

    private static int? OptionalPositiveInt(JsonElement parent, string property, string file, string path, List<ModValidationIssue> issues)
    {
        if (!parent.TryGetProperty(property, out var value)) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed) || parsed <= 0)
        {
            issues.Add(new("INVALID_VALUE", file, path + "." + property, property + " must be a positive integer."));
            return null;
        }
        return parsed;
    }

    private static HashSet<string> ReadEntityIds(string root, string relativeDirectory) =>
        ReadEntityDirectory(root, relativeDirectory)
            .Select(file => file.Root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id)).Cast<string>().ToHashSet(StringComparer.Ordinal);

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
            catch (JsonException) { }
        }
        return result;
    }

    private static void ValidateKeys(JsonElement element, string file, string path, IEnumerable<string> allowed, IEnumerable<string> required, List<ModValidationIssue> issues)
    {
        var allowedSet = allowed.ToHashSet(StringComparer.Ordinal);
        var present = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var property in present.Where(name => !allowedSet.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("UNKNOWN_KEY", file, path + "." + property, $"Unknown key '{property}'."));
        foreach (var property in required.Where(name => !present.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Required parameter '{property}' is missing."));
    }

    private static bool TryString(JsonElement parent, string property, string file, string path, List<ModValidationIssue> issues, out string? value)
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

    private static bool TryInt(JsonElement parent, string property, string file, string path, List<ModValidationIssue> issues, out int value)
    {
        value = default;
        if (!parent.TryGetProperty(property, out var element)) return false;
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value)) return true;
        issues.Add(new("INVALID_TYPE", file, path, "Expected an integer."));
        return false;
    }

    private sealed record EntityFile(string Path, JsonElement Root);
}
