namespace Battlegrounds.Content;

public sealed class ModValidator
{
    public ModValidationReport Validate(string modDirectory)
    {
        var baseReport = new DirectoryModValidator().Validate(modDirectory);
        var issues = baseReport.Issues
            .Where(issue => !(
                issue.Code == "UNKNOWN_KEY" &&
                issue.File.StartsWith("content/leaders/", StringComparison.Ordinal) &&
                issue.Path == "$.initialPowerId"))
            .Concat(new PowerLifecycleModValidator().Validate(modDirectory))
            .ToArray();

        var preliminaryReport = new ModValidationReport(issues);
        return new ModValidationReport(
            issues.Concat(new LeaderSelectionModValidator().Validate(modDirectory, preliminaryReport)));
    }
}
