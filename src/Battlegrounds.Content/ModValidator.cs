namespace Battlegrounds.Content;

public sealed class ModValidator
{
    public ModValidationReport Validate(string modDirectory)
    {
        var baseIssues = new DirectoryModValidator().Validate(modDirectory).Issues
            .Where(issue => !IsSupersededBaseIssue(issue));

        var powerIssues = new PowerLifecycleModValidator().Validate(modDirectory)
            .Where(issue => !IsSupersededEffectSchemaIssue(issue));

        var advancedIssues = new AdvancedEffectModValidator().Validate(modDirectory);
        var dynamicIssues = new DynamicEffectValueModValidator().Validate(modDirectory);
        var statefulIssues = new StatefulEffectModValidator().Validate(modDirectory);
        var generationIssues = new GenerationChoiceModValidator().Validate(modDirectory);
        var actionIssues = new ActionModValidator().Validate(modDirectory);
        var themeIssues = new ModThemeValidator().Validate(modDirectory)
            .Concat(new ModThemeMetricsValidator().Validate(modDirectory))
            .Concat(new ModThemeRoleValidator().Validate(modDirectory));

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
            .Concat(new ModInteractionSettingsValidator().Validate(modDirectory))
            .Concat(themeIssues)
            .ToArray();

        var preliminaryReport = new ModValidationReport(issues);
        return new ModValidationReport(issues.Concat(new LeaderSelectionModValidator().Validate(modDirectory, preliminaryReport)));
    }

    private static bool IsSupersededBaseIssue(ModValidationIssue issue) =>
        IsSupersededEffectSchemaIssue(issue) ||
        IsSpecializedValidatorOwnedSchemaIssue(issue);

    private static bool IsSpecializedValidatorOwnedSchemaIssue(ModValidationIssue issue)
    {
        if (issue.Code != "UNKNOWN_KEY") return false;

        if (issue.File == "rules/preparation.json" && issue.Path == "$.actionOfferSizesByTier")
            return true;

        return issue.File.StartsWith("content/leaders/", StringComparison.Ordinal) &&
               issue.Path == "$.initialPowerId";
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

}
