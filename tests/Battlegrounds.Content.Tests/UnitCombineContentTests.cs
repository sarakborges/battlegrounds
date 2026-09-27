using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class UnitCombineContentTests
{
    [Fact]
    public void Load_ExposesPerFileUnitCombineCatalog()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");

        var mod = new ModLoader().Load(path);
        var combine = mod.Combines.GetRequired(new UnitCombineId("scout-merge"));

        Assert.Equal(new UnitId("scout"), combine.SourceUnitId);
        Assert.Equal(3, combine.RequiredCopies);
        Assert.Equal(new UnitId("scout-merged"), combine.ResultUnitId);
        Assert.Equal("Merged Scout", mod.Units.GetRequired(combine.ResultUnitId).Name);
    }

    [Fact]
    public void Validate_RejectsCombineReferenceToUnknownUnit()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "combines", "broken.json"),
                """
                {
                  "id": "broken",
                  "name": "Broken",
                  "sourceUnitId": "missing",
                  "requiredCopies": 3,
                  "resultUnitId": "scout-merged"
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_REFERENCE" &&
                issue.File == "content/combines/broken.json" &&
                issue.Path == "$.sourceUnitId");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-combines", Guid.NewGuid().ToString("N"));
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
