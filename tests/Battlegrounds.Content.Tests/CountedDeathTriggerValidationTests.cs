using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class CountedDeathTriggerValidationTests
{
    [Fact]
    public void Validate_AfterFriendlyDeathsRequiresPositiveCount()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "units", "scout.json"),
                "{\"id\":\"scout\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":1," +
                "\"triggers\":[{\"event\":\"afterFriendlyDeaths\",\"effects\":[{\"kind\":\"addResource\",\"amount\":1}]}]}");
            File.WriteAllText(
                Path.Combine(path, "content", "units", "guard.json"),
                "{\"id\":\"guard\",\"name\":\"Guard\",\"tier\":1,\"attack\":1,\"health\":1," +
                "\"triggers\":[{\"event\":\"afterFriendlyDeaths\",\"count\":0,\"effects\":[{\"kind\":\"addResource\",\"amount\":1}]}]}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_PARAMETER" &&
                issue.File == "content/units/scout.json" &&
                issue.Path == "$.triggers[0].count");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_VALUE" &&
                issue.File == "content/units/guard.json" &&
                issue.Path == "$.triggers[0].count");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_CountOnOtherTriggerIsRejected()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "units", "scout.json"),
                "{\"id\":\"scout\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":1," +
                "\"triggers\":[{\"event\":\"onPlay\",\"count\":2,\"effects\":[{\"kind\":\"addResource\",\"amount\":1}]}]}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_PARAMETER" &&
                issue.File == "content/units/scout.json" &&
                issue.Path == "$.triggers[0].count");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Load_AfterFriendlyDeathsMaterializesCountedTrigger()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "units", "scout.json"),
                "{\"id\":\"scout\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":1," +
                "\"triggers\":[{\"event\":\"afterFriendlyDeaths\",\"count\":3,\"effects\":[{\"kind\":\"addResource\",\"amount\":1}]}]}");

            var package = new ModLoader().Load(path);
            var definition = package.Units.GetRequired(new UnitId("scout"));
            var trigger = Assert.Single(definition.Triggers);

            Assert.Equal(NativeTriggerKeys.AfterFriendlyDeaths, trigger.Event);
            Assert.Equal(3, trigger.Count);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-counted-trigger", Guid.NewGuid().ToString("N"));
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
