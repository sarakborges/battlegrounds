namespace Battlegrounds.Content;

public sealed class ModValidator
{
    public ModValidationReport Validate(string modDirectory)
    {
        var baseIssues = new DirectoryModValidator().Validate(modDirectory).Issues
            .Where(issue => !IsSupersededEffectSchemaIssue(issue))
            .Where(issue => !IsSupersededStaticEffectValueIssue(issue))
            .Where(issue => !(
                issue.Code == "UNKNOWN_KEY" &&
                issue.File.StartsWith("content/leaders/", StringComparison.Ordinal) &&
                issue.Path == "$.initialPowerId"));

        var powerIssues = new PowerLifecycleModValidator().Validate(modDirectory)
            .Where(issue => !IsSupersededEffectSchemaIssue(issue))
            .Where(issue => !IsSupersededStaticEffectValueIssue(issue));

        var issues = baseIssues
            .Concat(powerIssues)
            .Concat(new AdvancedEffectModValidator().Validate(modDirectory))
            .Concat(new DynamicEffectValueModValidator().Validate(modDirectory))
            .ToArray();

        var preliminaryReport = new ModValidationReport(issues);
        return new ModValidationReport(
            issues.Concat(new LeaderSelectionModValidator().Validate(modDirectory, preliminaryReport)));
    }

    private static bool IsSupersededEffectSchemaIssue(ModValidationIssue issue)
    {
        if (issue.Code == "UNKNOWN_KEY" &&
            (issue.Path.EndsWith(".conditions", StringComparison.Ordinal) ||
             issue.Path.EndsWith(".target.selection", StringComparison.Ordinal) ||
             issue.Path.EndsWith(".target.excludeSource", StringComparison.Ordinal) ||
             issue.Path.EndsWith(".target.limit", StringComparison.Ordinal)))
        {
            return true;
        }

        return issue.Code == "INVALID_VALUE" &&
               issue.Path.EndsWith(".target.scope", StringComparison.Ordinal);
    }

    private static bool IsSupersededStaticEffectValueIssue(ModValidationIssue issue)
    {
        if (!issue.Path.Contains(".effects[", StringComparison.Ordinal) ||
            !(issue.File.StartsWith("content/units/", StringComparison.Ordinal) ||
              issue.File.StartsWith("content/powers/", StringComparison.Ordinal)))
        {
            return false;
        }

        var numericPath = issue.Path.EndsWith(".attack", StringComparison.Ordinal) ||
                          issue.Path.EndsWith(".health", StringComparison.Ordinal) ||
                          issue.Path.EndsWith(".amount", StringComparison.Ordinal) ||
                          issue.Path.EndsWith(".count", StringComparison.Ordinal);

        if (numericPath && issue.Code is "INVALID_TYPE" or "INVALID_VALUE")
        {
            return true;
        }

        return issue.Code == "INVALID_VALUE" &&
               issue.Message.StartsWith("modifyStats requires", StringComparison.Ordinal);
    }
}
