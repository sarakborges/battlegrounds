using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class ModValidatorTests
{
    [Fact]
    public void Validate_ExampleMod_IsValid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");

        var report = new ModValidator().Validate(path);

        Assert.True(report.IsValid);
        Assert.Empty(report.Issues);
    }

    [Fact]
    public void Validate_CollectsSchemaReferenceAndSemanticErrors()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "content", "behaviors.json"),
                "[{\"id\":\"ward\",\"name\":\"Ward\"}]");
            File.WriteAllText(Path.Combine(path, "content", "units.json"),
                "[{\"id\":\"scout\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":2,\"behaviors\":[\"missing\"]}]");
            File.WriteAllText(Path.Combine(path, "content", "pool.json"),
                "[{\"unitId\":\"ghost\",\"copies\":0,\"unexpected\":true}]");

            var report = new ModValidator().Validate(path);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue => issue.Code == "MISSING_REQUIRED_KEY" && issue.File == "content/behaviors.json" && issue.Path == "$[0].handler");
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_REFERENCE" && issue.File == "content/units.json");
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_REFERENCE" && issue.File == "content/pool.json");
            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_VALUE" && issue.Path == "$[0].copies");
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_KEY" && issue.Path == "$[0].unexpected");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_TriggerAndEffectMissingParameters_AreReportedPrecisely()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "content", "units.json"),
                "[" +
                "{\"id\":\"scout\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":2,\"triggers\":[" +
                "{\"event\":\"onPlay\"}," +
                "{\"event\":\"onAttack\",\"effects\":[{\"kind\":\"dealDamage\",\"target\":{\"scope\":\"randomEnemy\"}}]}" +
                "]}]" );

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_PARAMETER" &&
                issue.Path == "$[0].triggers[0].effects");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_PARAMETER" &&
                issue.Path == "$[0].triggers[1].effects[0].amount");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_UnknownTaxonomyReferences_AreReported()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "content", "units.json"),
                "[{\"id\":\"scout\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":2,\"types\":[\"missing-type\"],\"tags\":[\"missing-tag\"]}]");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_REFERENCE" && issue.Path == "$[0].types[0]");
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_REFERENCE" && issue.Path == "$[0].tags[0]");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Load_InvalidMod_ThrowsReportAndDoesNotMaterializePackage()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "rules", "combat.json"), "{}");

            var exception = Assert.Throws<ModValidationException>(() => new ModLoader().Load(path));

            Assert.False(exception.Report.IsValid);
            var issue = Assert.Single(exception.Report.Issues, issue => issue.Code == "MISSING_REQUIRED_KEY");
            Assert.Equal("rules/combat.json", issue.File);
            Assert.Equal("$.startingSidePolicy", issue.Path);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_MissingRequiredFile_IsReportedInsteadOfThrown()
    {
        var path = CreateTempMod();
        try
        {
            File.Delete(Path.Combine(path, "content", "types.json"));

            var report = new ModValidator().Validate(path);

            var issue = Assert.Single(report.Issues, issue => issue.Code == "MISSING_REQUIRED_FILE");
            Assert.Equal("content/types.json", issue.File);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-mod-validation", Guid.NewGuid().ToString("N"));
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
