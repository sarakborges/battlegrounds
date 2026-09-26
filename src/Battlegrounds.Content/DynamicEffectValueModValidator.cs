using System.Text.Json;

namespace Battlegrounds.Content;

internal sealed class DynamicEffectValueModValidator
{
    private const int MaximumExpressionDepth = 32;

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly HashSet<string> Scopes = ["self", "selected", "friendly", "enemy"];
    private static readonly HashSet<string> Stats = ["attack", "health"];
    private static readonly HashSet<string> Operations = ["add", "multiply", "min", "max"];

    public IReadOnlyList<ModValidationIssue> Validate(string modDirectory)
    {
        var issues = new List<ModValidationIssue>();
        if (string.IsNullOrWhiteSpace(modDirectory) || !Directory.Exists(modDirectory)) return issues;

        var typeIds = ReadEntityIds(modDirectory, "content/types");
        var tagIds = ReadEntityIds(modDirectory, "content/tags");

        foreach (var file in ReadEntityDirectory(modDirectory, "content/units"))
            ValidateTriggers(file, typeIds, tagIds, powerMode: false, issues);
        foreach (var file in ReadEntityDirectory(modDirectory, "content/powers"))
            ValidateTriggers(file, typeIds, tagIds, powerMode: true, issues);

        return issues;
    }

    private static void ValidateTriggers(
        EntityFile file,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        bool powerMode,
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

            var triggerPath = $"$.triggers[{triggerIndex}]";
            var eventName = trigger.TryGetProperty("event", out var eventElement) && eventElement.ValueKind == JsonValueKind.String
                ? eventElement.GetString()
                : null;
            var allowSelected = powerMode && eventName == "onActivate";

            if (trigger.TryGetProperty("effects", out var effects) && effects.ValueKind == JsonValueKind.Array)
            {
                var effectIndex = 0;
                foreach (var effect in effects.EnumerateArray())
                {
                    if (effect.ValueKind == JsonValueKind.Object)
                    {
                        ValidateEffectValues(
                            file.Path,
                            effect,
                            $"{triggerPath}.effects[{effectIndex}]",
                            allowSelected,
                            typeIds,
                            tagIds,
                            issues);
                    }
                    effectIndex++;
                }
            }

            triggerIndex++;
        }
    }

    private static void ValidateEffectValues(
        string file,
        JsonElement effect,
        string path,
        bool allowSelected,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (!effect.TryGetProperty("kind", out var kindElement) || kindElement.ValueKind != JsonValueKind.String) return;
        var kind = kindElement.GetString();

        switch (kind)
        {
            case "modifyStats":
            {
                var hasAttack = effect.TryGetProperty("attack", out var attack);
                var hasHealth = effect.TryGetProperty("health", out var health);
                if (!hasAttack && !hasHealth)
                {
                    return;
                }

                if (hasAttack)
                    ValidateValue(file, attack, path + ".attack", allowTargetStat: true, allowSelected, typeIds, tagIds, 0, issues);
                if (hasHealth)
                    ValidateValue(file, health, path + ".health", allowTargetStat: true, allowSelected, typeIds, tagIds, 0, issues);

                if (IsLiteral(attack, hasAttack, out var attackValue) &&
                    IsLiteral(health, hasHealth, out var healthValue) &&
                    attackValue == 0 && healthValue == 0)
                {
                    issues.Add(new("INVALID_VALUE", file, path, "modifyStats requires a non-zero attack or health delta."));
                }
                break;
            }

            case "dealDamage":
                if (effect.TryGetProperty("amount", out var damage))
                {
                    ValidateValue(file, damage, path + ".amount", allowTargetStat: true, allowSelected, typeIds, tagIds, 0, issues);
                    if (TryLiteral(damage, out var value) && value <= 0)
                        issues.Add(new("INVALID_VALUE", file, path + ".amount", "amount must be positive when it is a literal."));
                }
                break;

            case "summonUnit":
                if (effect.TryGetProperty("count", out var count))
                {
                    ValidateValue(file, count, path + ".count", allowTargetStat: false, allowSelected, typeIds, tagIds, 0, issues);
                    if (TryLiteral(count, out var value) && value <= 0)
                        issues.Add(new("INVALID_VALUE", file, path + ".count", "count must be positive when it is a literal."));
                }
                break;

            case "addResource":
                if (effect.TryGetProperty("amount", out var resource))
                {
                    ValidateValue(file, resource, path + ".amount", allowTargetStat: false, allowSelected, typeIds, tagIds, 0, issues);
                    if (TryLiteral(resource, out var value) && value == 0)
                        issues.Add(new("INVALID_VALUE", file, path + ".amount", "amount cannot be zero when it is a literal."));
                }
                break;
        }
    }

    private static void ValidateValue(
        string file,
        JsonElement value,
        string path,
        bool allowTargetStat,
        bool allowSelected,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        int depth,
        List<ModValidationIssue> issues)
    {
        if (depth > MaximumExpressionDepth)
        {
            issues.Add(new("VALUE_EXPRESSION_TOO_DEEP", file, path, $"Effect value expressions cannot exceed depth {MaximumExpressionDepth}."));
            return;
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            if (!value.TryGetInt32(out _))
                issues.Add(new("INVALID_TYPE", file, path, "Expected a 32-bit integer."));
            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an integer or a value-expression object."));
            return;
        }

        if (!TryRequiredString(value, "kind", file, path + ".kind", issues, out var kind)) return;

        switch (kind)
        {
            case "sourceStat":
                ValidateKeys(value, file, path, ["kind", "stat"], ["kind", "stat"], issues);
                ValidateStat(value, file, path, issues);
                break;

            case "targetStat":
                ValidateKeys(value, file, path, ["kind", "stat"], ["kind", "stat"], issues);
                ValidateStat(value, file, path, issues);
                if (!allowTargetStat)
                {
                    issues.Add(new(
                        "INVALID_VALUE_EXPRESSION_CONTEXT",
                        file,
                        path + ".kind",
                        "targetStat is only valid for effects that resolve unit targets."));
                }
                break;

            case "unitCount":
                ValidateKeys(value, file, path, ["kind", "query"], ["kind", "query"], issues);
                if (value.TryGetProperty("query", out var query))
                    ValidateQuery(file, query, path + ".query", allowSelected, typeIds, tagIds, issues);
                break;

            default:
                if (kind is not null && Operations.Contains(kind))
                {
                    ValidateKeys(value, file, path, ["kind", "values"], ["kind", "values"], issues);
                    if (!value.TryGetProperty("values", out var values)) break;
                    if (values.ValueKind != JsonValueKind.Array)
                    {
                        issues.Add(new("INVALID_TYPE", file, path + ".values", "Expected an array."));
                        break;
                    }
                    if (values.GetArrayLength() < 2)
                    {
                        issues.Add(new("INVALID_VALUE", file, path + ".values", "Composite value expressions require at least two operands."));
                    }

                    var index = 0;
                    foreach (var operand in values.EnumerateArray())
                    {
                        ValidateValue(
                            file,
                            operand,
                            $"{path}.values[{index}]",
                            allowTargetStat,
                            allowSelected,
                            typeIds,
                            tagIds,
                            depth + 1,
                            issues);
                        index++;
                    }
                }
                else
                {
                    issues.Add(new("UNSUPPORTED_VALUE_EXPRESSION", file, path + ".kind", $"Value expression kind '{kind}' is not supported."));
                }
                break;
        }
    }

    private static void ValidateStat(
        JsonElement expression,
        string file,
        string path,
        List<ModValidationIssue> issues)
    {
        if (TryRequiredString(expression, "stat", file, path + ".stat", issues, out var stat) && !Stats.Contains(stat!))
            issues.Add(new("INVALID_VALUE", file, path + ".stat", $"Unknown stat '{stat}'."));
    }

    private static void ValidateQuery(
        string file,
        JsonElement query,
        string path,
        bool allowSelected,
        IReadOnlySet<string> typeIds,
        IReadOnlySet<string> tagIds,
        List<ModValidationIssue> issues)
    {
        if (query.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new("INVALID_TYPE", file, path, "Expected an object."));
            return;
        }

        ValidateKeys(query, file, path, ["scope", "excludeSource", "typeId", "tagId"], ["scope"], issues);
        string? scope = null;
        if (TryRequiredString(query, "scope", file, path + ".scope", issues, out var parsedScope))
        {
            scope = parsedScope;
            if (!Scopes.Contains(scope!))
                issues.Add(new("INVALID_VALUE", file, path + ".scope", $"Unknown target scope '{scope}'."));
            else if (scope == "selected" && !allowSelected)
                issues.Add(new("INVALID_VALUE", file, path + ".scope", "selected is only valid during an activatable power's onActivate trigger."));
        }

        if (query.TryGetProperty("excludeSource", out var exclude))
        {
            if (exclude.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                issues.Add(new("INVALID_TYPE", file, path + ".excludeSource", "Expected a boolean."));
            else if (exclude.GetBoolean() && scope != "friendly")
                issues.Add(new("INVALID_PARAMETER", file, path + ".excludeSource", "excludeSource is only valid for friendly queries."));
        }

        if (query.TryGetProperty("typeId", out _) &&
            TryRequiredString(query, "typeId", file, path + ".typeId", issues, out var typeId) &&
            !typeIds.Contains(typeId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, path + ".typeId", $"Unknown unit type '{typeId}'."));
        }
        if (query.TryGetProperty("tagId", out _) &&
            TryRequiredString(query, "tagId", file, path + ".tagId", issues, out var tagId) &&
            !tagIds.Contains(tagId!))
        {
            issues.Add(new("UNKNOWN_REFERENCE", file, path + ".tagId", $"Unknown tag '{tagId}'."));
        }
    }

    private static bool IsLiteral(JsonElement value, bool isPresent, out int literal)
    {
        if (!isPresent)
        {
            literal = 0;
            return true;
        }
        return TryLiteral(value, out literal);
    }

    private static bool TryLiteral(JsonElement value, out int literal)
    {
        literal = default;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out literal);
    }

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
                {
                    result.Add(new EntityFile(
                        relativeDirectory + "/" + Path.GetFileName(fullPath),
                        document.RootElement.Clone()));
                }
            }
            catch (JsonException)
            {
            }
        }
        return result;
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
            issues.Add(new("MISSING_REQUIRED_PARAMETER", file, path + "." + property, $"Required value-expression parameter '{property}' is missing."));
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

    private sealed record EntityFile(string Path, JsonElement Root);
}
