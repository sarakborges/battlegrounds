using Battlegrounds.Core.Domain.Effects;

namespace Battlegrounds.Content.Tests;

public sealed class EffectSchemaValidationTests
{
    [Fact]
    public void Validate_DestroyAndTriggeredEventReportMissingParameters()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "content", "units.json"),
                "[{\"id\":\"unit\",\"name\":\"Unit\",\"tier\":1,\"attack\":1,\"health\":1," +
                "\"triggers\":[{\"event\":\"onPlay\",\"effects\":[" +
                "{\"kind\":\"destroyUnit\"}," +
                "{\"kind\":\"triggerEvent\",\"target\":{\"scope\":\"self\"}}" +
                "]}]}]");
            File.WriteAllText(Path.Combine(path, "content", "pool.json"),
                "[{\"unitId\":\"unit\",\"copies\":1}]");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_PARAMETER" &&
                issue.Path == "$[0].triggers[0].effects[0].target");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_PARAMETER" &&
                issue.Path == "$[0].triggers[0].effects[1].event");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Load_DestroyAndTriggerEventEffects_AreMaterialized()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "content", "units.json"),
                "[{\"id\":\"unit\",\"name\":\"Unit\",\"tier\":1,\"attack\":1,\"health\":1," +
                "\"triggers\":[{\"event\":\"onPlay\",\"effects\":[" +
                "{\"kind\":\"destroyUnit\",\"target\":{\"scope\":\"self\"}}," +
                "{\"kind\":\"triggerEvent\",\"target\":{\"scope\":\"self\"},\"event\":\"onDeath\"}" +
                "]}]}]");
            File.WriteAllText(Path.Combine(path, "content", "pool.json"),
                "[{\"unitId\":\"unit\",\"copies\":1}]");

            var package = new ModLoader().Load(path);
            var unit = package.Units.GetRequired(new Battlegrounds.Core.Domain.Ids.UnitId("unit"));
            var effects = Assert.Single(unit.Triggers).Effects;

            Assert.IsType<DestroyUnitEffectDefinition>(effects[0]);
            var trigger = Assert.IsType<TriggerEventEffectDefinition>(effects[1]);
            Assert.Equal(NativeTriggerKeys.OnDeath, trigger.Event);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-effect-schema", Guid.NewGuid().ToString("N"));
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
