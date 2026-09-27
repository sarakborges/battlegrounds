namespace Battlegrounds.Content;

public sealed class ModValidator
{
    public ModValidationReport Validate(string modDirectory)
    {
        var baseIssues = new DirectoryModValidator().Validate(modDirectory).Issues
            .Where(issue => !IsSupersededEffectSchemaIssue(issue))
            .Where(issue => !IsSupersededStaticEffectValueIssue(issue))
            .Where(issue => !IsSupersededGenerationIssue(issue))
            .Where(issue => !IsSupersededPersistentMutationIssue(issue))
            .Where(issue => !IsSupersededCombineIssue(issue))
            .Where(issue => !(issue.Code == "UNKNOWN_KEY" && issue.File == "rules/preparation.json" && issue.Path == "$.actionOfferSizesByTier"))
            .Where(issue => !(issue.Code == "UNKNOWN_KEY" && issue.File.StartsWith("content/leaders/", StringComparison.Ordinal) && issue.Path == "$.initialPowerId"));

        var powerIssues = new PowerLifecycleModValidator().Validate(modDirectory)
            .Where(issue => !IsSupersededEffectSchemaIssue(issue))
            .Where(issue => !IsSupersededStaticEffectValueIssue(issue))
            .Where(issue => !IsSupersededGenerationIssue(issue))
            .Where(issue => !IsSupersededPersistentMutationIssue(issue))
            .Where(issue => !IsSupersededCombineIssue(issue));

        var advancedIssues = new AdvancedEffectModValidator().Validate(modDirectory)
            .Where(issue => !IsSupersededPersistentMutationIssue(issue))
            .Where(issue => !IsSupersededCombineIssue(issue));
        var dynamicIssues = new DynamicEffectValueModValidator().Validate(modDirectory)
            .Where(issue => !IsSupersededPersistentMutationIssue(issue))
            .Where(issue => !IsSupersededCombineIssue(issue));
        var statefulIssues = new StatefulEffectModValidator().Validate(modDirectory)
            .Where(issue => !IsSupersededPersistentMutationIssue(issue))
            .Where(issue => !IsSupersededCombineIssue(issue));
        var generationIssues = new GenerationChoiceModValidator().Validate(modDirectory)
            .Where(issue => !IsSupersededPersistentMutationIssue(issue))
            .Where(issue => !IsSupersededCombineIssue(issue));
        var actionIssues = new ActionModValidator().Validate(modDirectory)
            .Where(issue => !IsSupersededPersistentMutationIssue(issue))
            .Where(issue => !IsSupersededCombineIssue(issue));

        var issues = baseIssues
            .Concat(powerIssues)
            .Concat(advancedIssues)
            .Concat(dynamicIssues)
            .Concat(statefulIssues)
            .Concat(generationIssues)
            .Concat(actionIssues)
            .Concat(new PersistentUnitMutationModValidator().Validate(modDirectory))
            .Concat(new UnitCombineModValidator().Validate(modDirectory))
            .Concat(new PresentationModValidator().Validate(modDirectory))
            .Concat(new PresentationAssetModValidator().Validate(modDirectory))
            .ToArray();

        var preliminaryReport = new ModValidationReport(issues);
        return new ModValidationReport(issues.Concat(new LeaderSelectionModValidator().Validate(modDirectory, preliminaryReport)));
    }

    private static bool IsSupersededEffectSchemaIssue(ModValidationIssue issue)
    {
        if (issue.Code == "UNKNOWN_KEY" &&
            (issue.Path.EndsWith(".conditions", StringComparison.Ordinal) ||
             issue.Path.EndsWith(".activationLimit", StringComparison.Ordinal) ||
             issue.Path.EndsWith(".counter", StringComparison.Ordinal) ||
             issue.Path.EndsWith(".count", StringComparison.Ordinal) ||
             issue.Path.EndsWith(".target.selection", StringComparison.Ordinal) ||
             issue.Path.EndsWith(".target.excludeSource", StringComparison.Ordinal) ||
             issue.Path.EndsWith(".target.limit", StringComparison.Ordinal)))
            return true;
        if (issue.Code == "INVALID_PARAMETER" && issue.Path.EndsWith(".count", StringComparison.Ordinal)) return true;
        if (issue.Code == "UNSUPPORTED_TRIGGER" && issue.Message.Contains("afterEventCount", StringComparison.Ordinal)) return true;
        return issue.Code == "INVALID_VALUE" && issue.Path.EndsWith(".target.scope", StringComparison.Ordinal);
    }

    private static bool IsSupersededStaticEffectValueIssue(ModValidationIssue issue)
    {
        if (!issue.Path.Contains(".effects[", StringComparison.Ordinal) ||
            !(issue.File.StartsWith("content/units/", StringComparison.Ordinal) || issue.File.StartsWith("content/powers/", StringComparison.Ordinal)))
            return false;
        var numericPath = issue.Path.EndsWith(".attack", StringComparison.Ordinal) ||
                          issue.Path.EndsWith(".health", StringComparison.Ordinal) ||
                          issue.Path.EndsWith(".amount", StringComparison.Ordinal) ||
                          issue.Path.EndsWith(".count", StringComparison.Ordinal);
        if (numericPath && issue.Code is "INVALID_TYPE" or "INVALID_VALUE") return true;
        return issue.Code == "INVALID_VALUE" && issue.Message.StartsWith("modifyStats requires", StringComparison.Ordinal);
    }

    private static bool IsSupersededGenerationIssue(ModValidationIssue issue)
    {
        if (!issue.Path.Contains(".effects[", StringComparison.Ordinal)) return false;
        if (issue.Code == "UNSUPPORTED_EFFECT" &&
            (issue.Message.Contains("generateUnitToReserve", StringComparison.Ordinal) ||
             issue.Message.Contains("generateUnitChoice", StringComparison.Ordinal) ||
             issue.Message.Contains("generateActionToReserve", StringComparison.Ordinal) ||
             issue.Message.Contains("generateActionChoice", StringComparison.Ordinal)))
            return true;
        return issue.Code == "UNKNOWN_KEY" &&
               (issue.Path.EndsWith(".unitId", StringComparison.Ordinal) ||
                issue.Path.EndsWith(".actionId", StringComparison.Ordinal) ||
                issue.Path.EndsWith(".count", StringComparison.Ordinal) ||
                issue.Path.EndsWith(".generationQuery", StringComparison.Ordinal) ||
                issue.Path.EndsWith(".actionQuery", StringComparison.Ordinal) ||
                issue.Path.EndsWith(".optionCount", StringComparison.Ordinal));
    }

    private static bool IsSupersededPersistentMutationIssue(ModValidationIssue issue)
    {
        if (!issue.Path.Contains(".effects[", StringComparison.Ordinal)) return false;

        if (issue.Code == "UNSUPPORTED_EFFECT" &&
            (issue.Message.Contains("transformUnit", StringComparison.Ordinal) ||
             issue.Message.Contains("copyUnitToReserve", StringComparison.Ordinal) ||
             issue.Message.Contains("applyUnitModifier", StringComparison.Ordinal) ||
             issue.Message.Contains("removeUnitModifier", StringComparison.Ordinal)))
            return true;

        return issue.Code == "UNKNOWN_KEY" && issue.Path.EndsWith(".modifierKey", StringComparison.Ordinal);
    }

    private static bool IsSupersededCombineIssue(ModValidationIssue issue) =>
        issue.Code == "UNSUPPORTED_TRIGGER" && issue.Message.Contains("onCombine", StringComparison.Ordinal);
}
