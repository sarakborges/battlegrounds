using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class UnitAuraValidationTests
{
    [Fact]
    public void WarbandsContinuousAuraContentValidates()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "warbands");
        var report = new ModValidator().Validate(path);
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues.Select(issue => $"{issue.Code} {issue.File} {issue.Path}: {issue.Message}")));
    }
}
