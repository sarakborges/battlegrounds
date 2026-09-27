using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class UnitCombineContentTests
{
    [Fact]
    public void Load_ExposesIndependentUnitCombineCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var mod = new ModLoader().Load(path);

        var combine = mod.UnitCombines.GetRequired(new UnitCombineId("scout-upgrade"));

        Assert.Equal(new UnitId("scout"), combine.SourceUnitId);
        Assert.Equal(3, combine.RequiredCopies);
        Assert.Equal(new UnitId("scout-prime"), combine.ResultUnitId);
    }

    [Fact]
    public void Validate_RejectsUnknownCombineResultAndFilenameMismatch()
    {
        var path = CreateTempMod();
        try
        {
            var combineDirectory = Path.Combine(path, "content", "combines");
            Directory.CreateDirectory(combineDirectory);
            File.WriteAllText(
                Path.Combine(combineDirectory, "wrong-name.json"),
                """
                {
                  "id": "broken-combine",
                  "name": "Broken Combine",
                  "sourceUnitId": "scout",
                  "requiredCopies": 3,
                  "resultUnitId": "missing-unit"
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue => issue.Code == "ID_FILENAME_MISMATCH" && issue.File.EndsWith("wrong-name.json", StringComparison.Ordinal));
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_REFERENCE" && issue.Path == "$.resultUnitId");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-unit-combine", Guid.NewGuid().ToString("N"));
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
