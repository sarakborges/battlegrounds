namespace Battlegrounds.Content.Tests;

public sealed class SpecializedSchemaOwnershipValidationTests
{
    [Fact]
    public void Validate_AcceptsSpecializedPreparationAndLeaderSchemaWithoutCompatibilityBridge()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");

        var report = new ModValidator().Validate(path);

        Assert.DoesNotContain(report.Issues, issue =>
            issue.Code == "UNKNOWN_KEY" &&
            issue.File == "rules/preparation.json" &&
            issue.Path == "$.actionOfferSizesByTier");
        Assert.DoesNotContain(report.Issues, issue =>
            issue.Code == "UNKNOWN_KEY" &&
            issue.File.StartsWith("content/leaders/", StringComparison.Ordinal) &&
            issue.Path == "$.initialPowerId");
        Assert.True(
            report.IsValid,
            string.Join(Environment.NewLine, report.Issues.Select(issue => $"{issue.Code} {issue.File} {issue.Path}: {issue.Message}")));
    }

    [Fact]
    public void Validate_SpecializedValidatorsStillRejectInvalidPreparationAndLeaderValues()
    {
        var path = CreateTempMod();
        try
        {
            var preparationPath = Path.Combine(path, "rules", "preparation.json");
            File.WriteAllText(
                preparationPath,
                File.ReadAllText(preparationPath).Replace(
                    "[0, 0, 0, 0, 0, 0]",
                    "[4, 0, 0, 0, 0, 0]",
                    StringComparison.Ordinal));

            var leaderPath = Path.Combine(path, "content", "leaders", "steady.json");
            File.WriteAllText(
                leaderPath,
                File.ReadAllText(leaderPath).Replace(
                    "steady-pulse",
                    "missing-power",
                    StringComparison.Ordinal));

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_VALUE" &&
                issue.File == "rules/preparation.json" &&
                issue.Path == "$.actionOfferSizesByTier[0]");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_REFERENCE" &&
                issue.File == "content/leaders/steady.json" &&
                issue.Path == "$.initialPowerId");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-specialized-schema", Guid.NewGuid().ToString("N"));
        CopyDirectory(source, target);
        return target;
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(source, target, StringComparison.Ordinal));
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(source, target, StringComparison.Ordinal));
    }
}
