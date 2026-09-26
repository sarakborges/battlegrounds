using System.Collections.ObjectModel;

namespace Battlegrounds.Content;

public enum ModValidationSeverity
{
    Error,
}

public sealed record ModValidationIssue(
    string Code,
    string File,
    string Path,
    string Message,
    ModValidationSeverity Severity = ModValidationSeverity.Error);

public sealed class ModValidationReport
{
    private readonly ReadOnlyCollection<ModValidationIssue> _issues;

    public IReadOnlyList<ModValidationIssue> Issues => _issues;
    public bool IsValid => _issues.Count == 0;
    public int ErrorCount => _issues.Count(issue => issue.Severity == ModValidationSeverity.Error);

    internal ModValidationReport(IEnumerable<ModValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        _issues = Array.AsReadOnly(issues
            .OrderBy(issue => issue.File, StringComparer.Ordinal)
            .ThenBy(issue => issue.Path, StringComparer.Ordinal)
            .ThenBy(issue => issue.Code, StringComparer.Ordinal)
            .ToArray());
    }
}

public sealed class ModValidationException : Exception
{
    public ModValidationReport Report { get; }

    public ModValidationException(ModValidationReport report)
        : base($"Mod validation failed with {report?.ErrorCount ?? 0} error(s).")
    {
        Report = report ?? throw new ArgumentNullException(nameof(report));
    }
}
