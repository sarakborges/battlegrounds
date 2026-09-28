using System.Text.Json;
using Battlegrounds.Core.Domain.Behaviors;
using Battlegrounds.Core.Domain.Effects;

namespace Battlegrounds.Content;

internal sealed class DirectoryModValidator
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public ModValidationReport Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory))
        {
            issues.Add(new("MOD_DIRECTORY_INVALID", "", "$", "Mod directory cannot be empty."));
            return new ModValidationReport(issues);
        }

        if (!Directory.Exists(modDirectory))
        {
            issues.Add(new("MOD_DIRECTORY_MISSING", "", "$", $"Mod directory '{modDirectory}' does not exist."));
            return new ModValidationReport(issues);
        }

        var manifest = ReadRequiredObject(modDirectory, "mod.json", issues);
        var match = ReadRequiredObject(modDirectory, "rules/match.json", issues);
        var preparation = ReadRequiredObject(modDirectory, "rules/preparation.json", issues);
        var combat = ReadRequiredObject(modDirectory, "rules/combat.json", issues);
        var pool = ReadRequiredArray(modDirectory, "content/pool.json", issues);

        ValidateManifest(manifest, issues);
        var startingHealth = ValidateMatch(match, issues);
        var maximumTier = ValidatePreparation(preparation, issues);
        ValidateCombat(combat, issues);

        var behaviorFiles = ReadEntityDirectory(modDirectory, "content/behaviors", issues);
        var leaderFiles = ReadEntityDirectory(modDirectory, "content/leaders", issues);
        var typeFiles = ReadEntityDirectory(modDirectory, "content/types", issues);
        var tagFiles = ReadEntityDirectory(modDirectory, "content/tags", issues);
        var unitFiles = ReadEntityDirectory(modDirectory, "content/units", issues);

        var behaviorHandlers = ValidateBehaviors(behaviorFiles, issues);
        ValidateLeaders(leaderFiles, startingHealth, issues);
        var typeIds = ValidateNamedEntities(typeFiles, "unit type", requireAtLeastOne: false, issues);
        var tagIds = ValidateNamedEntities(tagFiles, "tag", requireAtLeastOne: false, issues);
        var unitIds = ValidateUnitHeaders(unitFiles, maximumTier, behaviorHandlers, typeIds, tagIds, issues);
        ValidateUnitTriggers(unitFiles, unitIds, behaviorHandlers.Keys.ToHashSet(StringComparer.Ordinal), typeIds, tagIds, issues);
        ValidatePool(pool, unitIds, issues);

        return new ModValidationReport(issues);
    }

    private static JsonElement? ReadRequiredObject(
        string root,
        string relativePath,
        List<ModValidationIssue> issues)
    {
        var element = ReadRequiredJson(root, relativePath, issues);
        if (element is null) return null;
        if (element.Value.ValueKind == JsonValueKind.Object) return element;
        issues.Add(new("INVALID_TYPE", relativePath, "$", "Expected an object."));
        return null;
    }

    private static JsonElement? ReadRequiredArray(
        string root,
        string relativePath,
        List<ModValidationIssue> issues)
    {
        var element = ReadRequiredJson(root, relativePath, issues);
        if (element is null) return null;
        if (element.Value.ValueKind == JsonValueKind.Array) return element;
        issues.Add(new("INVALID_TYPE", relativePath, "$", "Expected an array."));
        return null;
    }

    private static JsonElement? ReadRequiredJson(
        string root,
        string relativePath,
        List<ModValidationIssue> issues)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            issues.Add(new("MISSING_REQUIRED_FILE", relativePath, "$", $"Required file '{relativePath}' is missing."));
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath), DocumentOptions);
            return document.RootElement.Clone();
        }
        catch (JsonException exception)
        {
            issues.Add(new("INVALID_JSON", relativePath, exception.Path ?? "$", "File contains invalid JSON."));
            return null;
        }
    }

    private static IReadOnlyList<EntityFile> ReadEntityDirectory(
        string root,
        string relativeDirectory,
        List<ModValidationIssue> issues)
    {
        var fullDirectory = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(fullDirectory))
        {
            issues.Add(new(
                "MISSING_REQUIRED_DIRECTORY",
                relativeDirectory,
                "$",
                $"Required directory '{relativeDirectory}' is missing."));
            return [];
        }

        var result = new List<EntityFile>();
        foreach (var fullPath in Directory.GetFiles(fullDirectory, "*.json", SearchOption.TopDirectoryOnly)
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

                result.Add(new EntityFile(
                    relativePath,
                    Path.GetFileNameWithoutExtension(fileName),
                    document.RootElement.Clone()));
            }
            catch (JsonException exception)
            {
                issues.Add(new("INVALID_JSON", relativePath, exception.Path ?? "$", "File contains invalid JSON."));
            }
        }

        return result;
    }

    private static void ValidateManifest(JsonElement? root, List<ModValidationIssue> issues)
    {
        const string file = "mod.json";
        if (root is null) return;

        ValidateKeys(
            root.Value,
            file,
            "$",
            ["schemaVersion", "id", "name", "terminology"],
            ["schemaVersion", "id", "name", "terminology"],
            issues);

        if (TryInt(root.Value, "schemaVersion", file, "$.schemaVersion", issues, out var version) && version != 1)
        {
            issues.Add(new("UNSUPPORTED_SCHEMA_VERSION", file, "$.schemaVersion", $"Unsupported schema version {version}."));
        }

        RequireNonEmptyString(root.Value, "id", file, "$.id", issues, out _);
        RequireNonEmptyString(root.Value, "name", file, "$.name", issues, out _);

        if (!root.Value.TryGetProperty("terminology", out var terminology)) return;
        if (terminology.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, "$.terminology", "Expected an object."));
            return;
        }

        if (!terminology.EnumerateObject().Any())
        {
            issues.Add(new("INVALID_VALUE", file, "$.terminology", "Terminology cannot be empty."));
        }

        foreach (var pair in terminology.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(pair.Name) ||
                pair.Value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(pair.Value.GetString()))
            {
                issues.Add(new(
                    "INVALID_VALUE",
                    file,
                    $"$.terminology.{pair.Name}",
                    "Terminology keys and values must be non-empty strings."));
            }
        }
    }

    private static int? ValidateMatch(JsonElement? root, List<ModValidationIssue> issues)
    {
        const string file = "rules/match.json";
        if (root is null) return null;

        ValidateKeys(
            root.Value,
            file,
            "$",
            ["minimumPlayers", "maximumPlayers", "startingHealth"],
            ["minimumPlayers", "maximumPlayers", "startingHealth"],
            issues);

        var hasMinimum = TryInt(root.Value, "minimumPlayers", file, "$.minimumPlayers", issues, out var minimum);
        var hasMaximum = TryInt(root.Value, "maximumPlayers", file, "$.maximumPlayers", issues, out var maximum);
        var hasHealth = TryInt(root.Value, "startingHealth", file, "$.startingHealth", issues, out var startingHealth);

        if (hasMinimum && minimum <= 0)
            issues.Add(new("INVALID_VALUE", file, "$.minimumPlayers", "minimumPlayers must be positive."));
        if (hasMaximum && maximum <= 0)
            issues.Add(new("INVALID_VALUE", file, "$.maximumPlayers", "maximumPlayers must be positive."));
        if (hasMinimum && hasMaximum && maximum < minimum)
            issues.Add(new("INVALID_VALUE", file, "$.maximumPlayers", "maximumPlayers cannot be lower than minimumPlayers."));
        if (hasHealth && startingHealth <= 0)
            issues.Add(new("INVALID_VALUE", file, "$.startingHealth", "startingHealth must be positive."));

        return hasHealth ? startingHealth : null;
    }

    private static int? ValidatePreparation(JsonElement? root, List<ModValidationIssue> issues)
    {
        const string file = "rules/preparation.json";
        if (root is null) return null;

        string[] requiredKeys =
        [
            "startingResource", "resourcePerRound", "maximumResource", "acquireCost", "releaseValue",
            "refreshCost", "fieldCapacity", "reserveCapacity", "maximumTier", "offerSizesByTier",
            "initialUpgradeCostsByTier",
        ];
        var allowedKeys = requiredKeys.Append("actionOfferSizesByTier");
        ValidateKeys(root.Value, file, "$", allowedKeys, requiredKeys, issues);

        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var key in requiredKeys.Take(9))
        {
            if (TryInt(root.Value, key, file, "$." + key, issues, out var value))
                values[key] = value;
        }

        foreach (var key in new[] { "startingResource", "resourcePerRound", "maximumResource", "acquireCost", "releaseValue", "refreshCost" })
        {
            if (values.TryGetValue(key, out var value) && value < 0)
                issues.Add(new("INVALID_VALUE", file, "$." + key, $"{key} cannot be negative."));
        }

        if (values.TryGetValue("fieldCapacity", out var fieldCapacity) && fieldCapacity <= 0)
            issues.Add(new("INVALID_VALUE", file, "$.fieldCapacity", "fieldCapacity must be positive."));
        if (values.TryGetValue("reserveCapacity", out var reserveCapacity) && reserveCapacity <= 0)
            issues.Add(new("INVALID_VALUE", file, "$.reserveCapacity", "reserveCapacity must be positive."));
        if (values.TryGetValue("maximumTier", out var maximumTier) && maximumTier <= 1)
            issues.Add(new("INVALID_VALUE", file, "$.maximumTier", "maximumTier must be greater than 1."));

        if (values.TryGetValue("startingResource", out var startingResource) &&
            values.TryGetValue("maximumResource", out var maximumResource) &&
            maximumResource < startingResource)
        {
            issues.Add(new("INVALID_VALUE", file, "$.maximumResource", "maximumResource cannot be lower than startingResource."));
        }

        var offerSizes = ValidateIntArray(root.Value, "offerSizesByTier", file, issues, positiveOnly: true);
        var upgradeCosts = ValidateIntArray(root.Value, "initialUpgradeCostsByTier", file, issues, positiveOnly: false);

        if (values.TryGetValue("maximumTier", out maximumTier))
        {
            if (offerSizes is not null && offerSizes.Count != maximumTier)
                issues.Add(new("INVALID_LENGTH", file, "$.offerSizesByTier", "Offer size must be defined for every tier."));
            if (upgradeCosts is not null && upgradeCosts.Count != maximumTier - 1)
                issues.Add(new("INVALID_LENGTH", file, "$.initialUpgradeCostsByTier", "Upgrade cost must be defined for every non-maximum tier."));
        }

        if (offerSizes is not null)
        {
            for (var index = 1; index < offerSizes.Count; index++)
            {
                if (offerSizes[index] < offerSizes[index - 1])
                    issues.Add(new("INVALID_VALUE", file, $"$.offerSizesByTier[{index}]", "Offer sizes cannot decrease at higher tiers."));
            }
        }

        return values.TryGetValue("maximumTier", out maximumTier) ? maximumTier : null;
    }

    private static void ValidateCombat(JsonElement? root, List<ModValidationIssue> issues)
    {
        const string file = "rules/combat.json";
        if (root is null) return;

        ValidateKeys(
            root.Value,
            file,
            "$",
            ["startingSidePolicy", "postCombatDamagePolicy"],
            ["startingSidePolicy", "postCombatDamagePolicy"],
            issues);

        if (RequireNonEmptyString(root.Value, "startingSidePolicy", file, "$.startingSidePolicy", issues, out var sidePolicy) &&
            sidePolicy is not ("random" or "largerFieldThenRandom"))
        {
            issues.Add(new("INVALID_VALUE", file, "$.startingSidePolicy", $"Unknown startingSidePolicy '{sidePolicy}'."));
        }

        if (RequireNonEmptyString(root.Value, "postCombatDamagePolicy", file, "$.postCombatDamagePolicy", issues, out var damagePolicy) &&
            damagePolicy != "winnerTierPlusSurvivorTiers")
        {
            issues.Add(new("INVALID_VALUE", file, "$.postCombatDamagePolicy", $"Unknown postCombatDamagePolicy '{damagePolicy}'."));
        }
    }

    private static Dictionary<string, string> ValidateBehaviors(
        IReadOnlyList<EntityFile> files,
        List<ModValidationIssue> issues)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            ValidateKeys(file.Root, file.RelativePath, "$", ["id", "name", "handler"], ["id", "name", "handler"], issues);
            var hasId = RequireNonEmptyString(file.Root, "id", file.RelativePath, "$.id", issues, out var id);
            RequireNonEmptyString(file.Root, "name", file.RelativePath, "$.name", issues, out _);
            var hasHandler = RequireNonEmptyString(file.Root, "handler", file.RelativePath, "$.handler", issues, out var handler);

            if (hasId)
            {
                ValidateFileStem(file, id!, issues);
                if (!result.TryAdd(id!, handler ?? string.Empty))
                    issues.Add(new("DUPLICATE_ID", file.RelativePath, "$.id", $"Duplicate behavior id '{id}'."));
            }

            if (hasHandler && !NativeBehaviorKeys.IsSupported(new NativeBehaviorKey(handler!)))
            {
                issues.Add(new("UNSUPPORTED_HANDLER", file.RelativePath, "$.handler", $"Native behavior handler '{handler}' is not supported."));
            }
        }

        return result;
    }

    private static void ValidateLeaders(
        IReadOnlyList<EntityFile> files,
        int? startingHealth,
        List<ModValidationIssue> issues)
    {
        if (files.Count == 0)
        {
            issues.Add(new("INVALID_VALUE", "content/leaders", "$", "A mod must define at least one leader."));
            return;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            ValidateKeys(
                file.Root,
                file.RelativePath,
                "$",
                ["id", "name", "healthModifier", "armor", "initialPowerId"],
                ["id", "name", "healthModifier", "armor"],
                issues);

            if (RequireNonEmptyString(file.Root, "id", file.RelativePath, "$.id", issues, out var id))
            {
                ValidateFileStem(file, id!, issues);
                if (!ids.Add(id!))
                    issues.Add(new("DUPLICATE_ID", file.RelativePath, "$.id", $"Duplicate leader id '{id}'."));
            }

            RequireNonEmptyString(file.Root, "name", file.RelativePath, "$.name", issues, out _);
            var hasModifier = TryInt(file.Root, "healthModifier", file.RelativePath, "$.healthModifier", issues, out var modifier);
            var hasArmor = TryInt(file.Root, "armor", file.RelativePath, "$.armor", issues, out var armor);

            if (hasArmor && armor < 0)
                issues.Add(new("INVALID_VALUE", file.RelativePath, "$.armor", "armor cannot be negative."));
            if (hasModifier && startingHealth is not null && startingHealth.Value + modifier <= 0)
            {
                issues.Add(new(
                    "INVALID_VALUE",
                    file.RelativePath,
                    "$.healthModifier",
                    "healthModifier must leave the player with positive starting Health."));
            }
        }
    }

    private static HashSet<string> ValidateNamedEntities(
        IReadOnlyList<EntityFile> files,
        string label,
        bool requireAtLeastOne,
        List<ModValidationIssue> issues)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (requireAtLeastOne && files.Count == 0)
        {
            issues.Add(new("INVALID_VALUE", files.Count == 0 ? "content" : files[0].RelativePath, "$", $"A mod must define at least one {label}."));
        }

        foreach (var file in files)
        {
            ValidateKeys(file.Root, file.RelativePath, "$", ["id", "name"], ["id", "name"], issues);
            if (RequireNonEmptyString(file.Root, "id", file.RelativePath, "$.id", issues, out var id))
            {
                ValidateFileStem(file, id!, issues);
                if (!ids.Add(id!))
                    issues.Add(new("DUPLICATE_ID", file.RelativePath, "$.id", $"Duplicate {label} id '{id}'."));
            }
            RequireNonEmptyString(file.Root, "name", file.RelativePath, "$.name", issues, out _);
        }

        return ids;
    }

    private static HashSet<string> ValidateUnitHeaders(
        IReadOnlyList<EntityFile> files,
        int? maximumTier,
        IReadOnlyDictionary<string, string> behaviorHandlers,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        var unitIds = new HashSet<string>(StringComparer.Ordinal);
        if (files.Count == 0)
        {
            issues.Add(new("INVALID_VALUE", "content/units", "$", "A mod must define at least one unit."));
            return unitIds;
        }

        foreach (var file in files)
        {
            ValidateKeys(
                file.Root,
                file.RelativePath,
                "$",
                ["id", "name", "tier", "attack", "health", "behaviors", "types", "tags", "triggers"],
                ["id", "name", "tier", "attack", "health"],
                issues);

            if (RequireNonEmptyString(file.Root, "id", file.RelativePath, "$.id", issues, out var id))
            {
                ValidateFileStem(file, id!, issues);
                if (!unitIds.Add(id!))
                    issues.Add(new("DUPLICATE_ID", file.RelativePath, "$.id", $"Duplicate unit id '{id}'."));
            }

            RequireNonEmptyString(file.Root, "name", file.RelativePath, "$.name", issues, out _);
            if (TryInt(file.Root, "tier", file.RelativePath, "$.tier", issues, out var tier))
            {
                if (tier <= 0)
                    issues.Add(new("INVALID_VALUE", file.RelativePath, "$.tier", "tier must be positive."));
                if (maximumTier is not null && tier > maximumTier)
                    issues.Add(new("INVALID_VALUE", file.RelativePath, "$.tier", $"tier {tier} exceeds maximumTier {maximumTier}."));
            }
            if (TryInt(file.Root, "attack", file.RelativePath, "$.attack", issues, out var attack) && attack < 0)
                issues.Add(new("INVALID_VALUE", file.RelativePath, "$.attack", "attack cannot be negative."));
            if (TryInt(file.Root, "health", file.RelativePath, "$.health", issues, out var health) && health <= 0)
                issues.Add(new("INVALID_VALUE", file.RelativePath, "$.health", "health must be positive."));

            ValidateBehaviorReferences(file, behaviorHandlers, issues);
            ValidateReferenceArray(file, "types", typeIds, "unit type", issues);
            ValidateReferenceArray(file, "tags", tagIds, "tag", issues);
        }

        return unitIds;
    }

    private static void ValidateBehaviorReferences(
        EntityFile file,
        IReadOnlyDictionary<string, string> behaviorHandlers,
        List<ModValidationIssue> issues)
    {
        if (!file.Root.TryGetProperty("behaviors", out var behaviors)) return;
        if (behaviors.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("INVALID_TYPE", file.RelativePath, "$.behaviors", "Expected an array."));
            return;
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var seenHandlers = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var behavior in behaviors.EnumerateArray())
        {
            var path = $"$.behaviors[{index}]";
            if (behavior.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(behavior.GetString()))
            {
                issues.Add(new("INVALID_TYPE", file.RelativePath, path, "Behavior reference must be a non-empty string."));
            }
            else
            {
                var id = behavior.GetString()!;
                if (!seenIds.Add(id))
                    issues.Add(new("DUPLICATE_REFERENCE", file.RelativePath, path, $"Behavior '{id}' is referenced more than once."));
                if (!behaviorHandlers.TryGetValue(id, out var handler))
                    issues.Add(new("UNKNOWN_REFERENCE", file.RelativePath, path, $"Unknown behavior '{id}'."));
                else if (!seenHandlers.Add(handler))
                    issues.Add(new("DUPLICATE_NATIVE_BEHAVIOR", file.RelativePath, path, $"Native behavior handler '{handler}' is attached more than once."));
            }
            index++;
        }
    }

    private static void ValidateReferenceArray(
        EntityFile file,
        string property,
        IReadOnlySet<string> known,
        string label,
        List<ModValidationIssue> issues)
    {
        if (!file.Root.TryGetProperty(property, out var values)) return;
        if (values.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("INVALID_TYPE", file.RelativePath, "$." + property, "Expected an array."));
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var value in values.EnumerateArray())
        {
            var path = $"$.{property}[{index}]";
            if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            {
                issues.Add(new("INVALID_TYPE", file.RelativePath, path, $"{label} reference must be a non-empty string."));
            }
            else
            {
                var id = value.GetString()!;
                if (!seen.Add(id))
                    issues.Add(new("DUPLICATE_REFERENCE", file.RelativePath, path, $"{label} '{id}' is referenced more than once."));
                if (!known.Contains(id))
                    issues.Add(new("UNKNOWN_REFERENCE", file.RelativePath, path, $"Unknown {label} '{id}'."));
            }
            index++;
        }
    }

    private static void ValidateUnitTriggers(
        IReadOnlyList<EntityFile> files,
        IReadOnlySet<string> unitIds,
        IReadOnlySet<string> behaviorIds,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        foreach (var file in files)
        {
            if (!file.Root.TryGetProperty("triggers", out var triggers)) continue;
            if (triggers.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new("INVALID_TYPE", file.RelativePath, "$.triggers", "Expected an array."));
                continue;
            }

            var triggerIndex = 0;
            foreach (var trigger in triggers.EnumerateArray())
            {
                var path = $"$.triggers[{triggerIndex}]";
                if (trigger.ValueKind != JsonValueKind.Object)
                {
                    issues.Add(new("INVALID_TYPE", file.RelativePath, path, "Expected an object."));
                    triggerIndex++;
                    continue;
                }

                ValidateKeys(trigger, file.RelativePath, path, ["event", "effects", "count"], ["event"], issues);
                var hasEvent = RequireNonEmptyString(trigger, "event", file.RelativePath, path + ".event", issues, out var eventName);
                var eventKey = hasEvent ? new NativeTriggerKey(eventName!) : default;
                if (hasEvent && !NativeTriggerKeys.IsSupported(eventKey))
                    issues.Add(new("UNSUPPORTED_TRIGGER", file.RelativePath, path + ".event", $"Trigger event '{eventName}' is not supported."));

                if (hasEvent && eventKey == NativeTriggerKeys.AfterFriendlyDeaths)
                {
                    if (!trigger.TryGetProperty("count", out _))
                    {
                        issues.Add(new("MISSING_REQUIRED_PARAMETER", file.RelativePath, path + ".count", "afterFriendlyDeaths requires 'count'."));
                    }
                    else if (TryInt(trigger, "count", file.RelativePath, path + ".count", issues, out var count) && count <= 0)
                    {
                        issues.Add(new("INVALID_VALUE", file.RelativePath, path + ".count", "count must be positive."));
                    }
                }
                else if (trigger.TryGetProperty("count", out _) &&
                         TryInt(trigger, "count", file.RelativePath, path + ".count", issues, out _))
                {
                    issues.Add(new("INVALID_PARAMETER", file.RelativePath, path + ".count", "count is only valid for afterFriendlyDeaths."));
                }

                if (!trigger.TryGetProperty("effects", out var effects))
                {
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file.RelativePath, path + ".effects", "Trigger requires 'effects'."));
                    triggerIndex++;
                    continue;
                }

                if (effects.ValueKind != JsonValueKind.Array)
                {
                    issues.Add(new("INVALID_TYPE", file.RelativePath, path + ".effects", "Expected an array."));
                    triggerIndex++;
                    continue;
                }

                if (effects.GetArrayLength() == 0)
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file.RelativePath, path + ".effects", "Trigger requires at least one effect."));

                var effectIndex = 0;
                foreach (var effect in effects.EnumerateArray())
                {
                    ValidateEffect(
                        file.RelativePath,
                        effect,
                        $"{path}.effects[{effectIndex}]",
                        unitIds,
                        behaviorIds,
                        typeIds,
                        tagIds,
                        issues);
                    effectIndex++;
                }

                triggerIndex++;
            }
        }
    }

    private static void ValidateEffect(
        string file,
        JsonElement effect,
        string path,
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

        if (!RequireNonEmptyString(effect, "kind", file, path + ".kind", issues, out var kind))
        {
            ValidateKeys(effect, file, path, ["kind"], ["kind"], issues);
            return;
        }

        switch (kind)
        {
            case "modifyStats":
            {
                ValidateKeys(effect, file, path, ["kind", "target", "attack", "health"], ["kind"], issues);
                ValidateRequiredTarget(file, effect, path, typeIds, tagIds, issues);
                if (!effect.TryGetProperty("attack", out _) && !effect.TryGetProperty("health", out _))
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path, "modifyStats requires 'attack' and/or 'health'."));
                break;
            }
            case "dealDamage":
                ValidateKeys(effect, file, path, ["kind", "target", "amount"], ["kind"], issues);
                ValidateRequiredTarget(file, effect, path, typeIds, tagIds, issues);
                if (!effect.TryGetProperty("amount", out _))
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + ".amount", "Effect requires 'amount'."));
                break;
            case "destroyUnit":
                ValidateKeys(effect, file, path, ["kind", "target"], ["kind"], issues);
                ValidateRequiredTarget(file, effect, path, typeIds, tagIds, issues);
                break;
            case "triggerEvent":
                ValidateKeys(effect, file, path, ["kind", "target", "event"], ["kind"], issues);
                ValidateRequiredTarget(file, effect, path, typeIds, tagIds, issues);
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
                break;
            case "addBehavior":
            case "removeBehavior":
                ValidateKeys(effect, file, path, ["kind", "target", "behaviorId"], ["kind"], issues);
                ValidateRequiredTarget(file, effect, path, typeIds, tagIds, issues);
                if (RequireParameterString(file, effect, "behaviorId", path, issues, out var behaviorId) && !behaviorIds.Contains(behaviorId!))
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".behaviorId", $"Unknown behavior '{behaviorId}'."));
                break;
            case "addResource":
                ValidateKeys(effect, file, path, ["kind", "amount"], ["kind"], issues);
                if (!effect.TryGetProperty("amount", out _))
                    issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + ".amount", "Effect requires 'amount'."));
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
                ValidateKeys(effect, file, path, ["kind"], ["kind"], issues);
                issues.Add(new("UNSUPPORTED_EFFECT", file, path + ".kind", $"Effect kind '{kind}' is not supported."));
                break;
        }
    }

    private static void ValidateRequiredTarget(
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
        if (RequireNonEmptyString(target, "scope", file, targetPath + ".scope", issues, out var scope) &&
            scope is not ("self" or "randomFriendly" or "randomEnemy" or "allFriendly" or "allEnemy"))
        {
            issues.Add(new("INVALID_VALUE", file, targetPath + ".scope", $"Unknown target scope '{scope}'."));
        }

        if (target.TryGetProperty("typeId", out _) &&
            RequireNonEmptyString(target, "typeId", file, targetPath + ".typeId", issues, out var typeId) &&
            !typeIds.Contains(typeId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, targetPath + ".typeId", $"Unknown unit type '{typeId}'."));
        }

        if (target.TryGetProperty("tagId", out _) &&
            RequireNonEmptyString(target, "tagId", file, targetPath + ".tagId", issues, out var tagId) &&
            !tagIds.Contains(tagId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, targetPath + ".tagId", $"Unknown tag '{tagId}'."));
        }
    }

    private static void ValidatePool(
        JsonElement? root,
        IReadOnlySet<string> unitIds,
        List<ModValidationIssue> issues)
    {
        const string file = "content/pool.json";
        if (root is null) return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var item in root.Value.EnumerateArray())
        {
            var path = $"$[{index}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
                index++;
                continue;
            }

            ValidateKeys(item, file, path, ["unitId", "copies"], ["unitId", "copies"], issues);
            if (RequireNonEmptyString(item, "unitId", file, path + ".unitId", issues, out var unitId))
            {
                if (!seen.Add(unitId!))
                    issues.Add(new("DUPLICATE_ID", file, path + ".unitId", $"Duplicate pool entry for '{unitId}'."));
                if (!unitIds.Contains(unitId!))
                    issues.Add(new("UNKNOWN_REFERENCE", file, path + ".unitId", $"Unknown unit '{unitId}'."));
            }

            if (TryInt(item, "copies", file, path + ".copies", issues, out var copies) && copies <= 0)
                issues.Add(new("INVALID_VALUE", file, path + ".copies", "copies must be positive."));
            index++;
        }
    }

    private static void ValidateFileStem(EntityFile file, string id, List<ModValidationIssue> issues)
    {
        if (!string.Equals(file.FileStem, id, StringComparison.Ordinal))
        {
            issues.Add(new(
                "ID_FILENAME_MISMATCH",
                file.RelativePath,
                "$.id",
                $"Entity id '{id}' must match file name '{file.FileStem}.json'."));
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

    private static bool TryInt(
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
        return TryInt(parent, property, file, path, issues, out value);
    }

    private static bool RequireNonEmptyString(
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
        return RequireNonEmptyString(effect, property, file, path + "." + property, issues, out value);
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
        return TryInt(effect, property, file, path + "." + property, issues, out value);
    }

    private static void ValidateRequiredPositiveInt(
        string file,
        JsonElement effect,
        string property,
        string path,
        List<ModValidationIssue> issues)
    {
        if (RequireParameterInt(file, effect, property, path, issues, out var value) && value <= 0)
            issues.Add(new("INVALID_VALUE", file, path + "." + property, $"{property} must be positive."));
    }

    private static List<int>? ValidateIntArray(
        JsonElement parent,
        string property,
        string file,
        List<ModValidationIssue> issues,
        bool positiveOnly)
    {
        if (!parent.TryGetProperty(property, out var element)) return null;
        var path = "$." + property;
        if (element.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an array."));
            return null;
        }

        var result = new List<int>();
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var value))
            {
                issues.Add(new("INVALID_TYPE", file, $"{path}[{index}]", "Expected an integer."));
            }
            else
            {
                result.Add(value);
                if (positiveOnly ? value <= 0 : value < 0)
                {
                    issues.Add(new(
                        "INVALID_VALUE",
                        file,
                        $"{path}[{index}]",
                        positiveOnly ? "Value must be positive." : "Value cannot be negative."));
                }
            }
            index++;
        }

        return result;
    }

    private sealed record EntityFile(string RelativePath, string FileStem, JsonElement Root);
}
