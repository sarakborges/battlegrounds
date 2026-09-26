using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class PowerValidationTests
{
    [Fact]
    public void Validate_LeaderInitialPowerMustExist()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "leaders", "steady.json"),
                "{\"id\":\"steady\",\"name\":\"Steady\",\"healthModifier\":0,\"armor\":0,\"initialPowerId\":\"missing\"}");

            var report = new ModValidator().Validate(path);

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

    [Fact]
    public void Validate_PowerRequiredParametersAndSetPowerReferenceAreReported()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "powers", "steady-pulse.json"),
                "{\"id\":\"steady-pulse\",\"name\":\"Broken\",\"activation\":{\"cost\":0,\"maxUsesPerTurn\":1},\"triggers\":[" +
                "{\"event\":\"onActivate\",\"effects\":[" +
                "{\"kind\":\"dealDamage\",\"target\":{\"scope\":\"selected\"}}," +
                "{\"kind\":\"setPower\",\"powerId\":\"missing\"}" +
                "]}]}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_KEY" &&
                issue.File == "content/powers/steady-pulse.json" &&
                issue.Path == "$.triggers[0].effects[0].amount");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_REFERENCE" &&
                issue.File == "content/powers/steady-pulse.json" &&
                issue.Path == "$.triggers[0].effects[1].powerId");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_ActivationAndOnActivateMustMatch()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "powers", "steady-pulse.json"),
                "{\"id\":\"steady-pulse\",\"name\":\"Broken\",\"activation\":{\"cost\":0,\"maxUsesPerTurn\":1},\"triggers\":[" +
                "{\"event\":\"onTurnStart\",\"effects\":[{\"kind\":\"addResource\",\"amount\":1}]}]}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_POWER_ACTIVATION" &&
                issue.File == "content/powers/steady-pulse.json");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_SelectedTargetIsOnlyValidForActivation()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "powers", "steady-pulse.json"),
                "{\"id\":\"steady-pulse\",\"name\":\"Broken\",\"triggers\":[" +
                "{\"event\":\"onTurnStart\",\"effects\":[" +
                "{\"kind\":\"modifyStats\",\"target\":{\"scope\":\"selected\"},\"attack\":1}" +
                "]}]}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_VALUE" &&
                issue.File == "content/powers/steady-pulse.json" &&
                issue.Path == "$.triggers[0].effects[0].target.scope");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_PowerIdMustMatchFilename()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "powers", "steady-pulse.json"),
                "{\"id\":\"other\",\"name\":\"Other\",\"triggers\":[{\"event\":\"onTurnStart\",\"effects\":[{\"kind\":\"addResource\",\"amount\":1}]}]}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "ID_FILENAME_MISMATCH" &&
                issue.File == "content/powers/steady-pulse.json" &&
                issue.Path == "$.id");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-power-validation", Guid.NewGuid().ToString("N"));
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
