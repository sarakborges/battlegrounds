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
    public void Validate_CollectsSchemaReferenceAndSemanticErrorsWithRealFilePaths()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "content", "behaviors", "protector.json"),
                "{\"id\":\"protector\",\"name\":\"Protector\"}");
            File.WriteAllText(Path.Combine(path, "content", "units", "scout.json"),
                "{\"id\":\"scout\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":2,\"behaviors\":[\"missing\"]}");
            File.WriteAllText(Path.Combine(path, "content", "pool.json"),
                "[{\"unitId\":\"ghost\",\"copies\":0,\"unexpected\":true}]");

            var report = new ModValidator().Validate(path);

            Assert.False(report.IsValid);
            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_KEY" &&
                issue.File == "content/behaviors/protector.json" &&
                issue.Path == "$.handler");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_REFERENCE" &&
                issue.File == "content/units/scout.json" &&
                issue.Path == "$.behaviors[0]");
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
            File.WriteAllText(Path.Combine(path, "content", "units", "scout.json"),
                "{\"id\":\"scout\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":2,\"triggers\":[" +
                "{\"event\":\"onPlay\"}," +
                "{\"event\":\"onAttack\",\"effects\":[{\"kind\":\"dealDamage\",\"target\":{\"scope\":\"randomEnemy\"}}]}" +
                "]}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_PARAMETER" &&
                issue.File == "content/units/scout.json" &&
                issue.Path == "$.triggers[0].effects");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_PARAMETER" &&
                issue.File == "content/units/scout.json" &&
                issue.Path == "$.triggers[1].effects[0].amount");
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
            File.WriteAllText(Path.Combine(path, "content", "units", "scout.json"),
                "{\"id\":\"scout\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":2,\"types\":[\"missing-type\"],\"tags\":[\"missing-tag\"]}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_REFERENCE" && issue.Path == "$.types[0]");
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_REFERENCE" && issue.Path == "$.tags[0]");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_MatchHealthAndPostCombatDamagePolicyAreRequired()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "rules", "match.json"),
                "{\"minimumPlayers\":2,\"maximumPlayers\":8}");
            File.WriteAllText(Path.Combine(path, "rules", "combat.json"),
                "{\"startingSidePolicy\":\"largerFieldThenRandom\"}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_KEY" &&
                issue.File == "rules/match.json" &&
                issue.Path == "$.startingHealth");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_KEY" &&
                issue.File == "rules/combat.json" &&
                issue.Path == "$.postCombatDamagePolicy");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_EntityIdMustMatchFilename()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "content", "units", "scout.json"),
                "{\"id\":\"different\",\"name\":\"Scout\",\"tier\":1,\"attack\":1,\"health\":2}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "ID_FILENAME_MISMATCH" &&
                issue.File == "content/units/scout.json" &&
                issue.Path == "$.id");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_LeaderKeysAndStartingValuesAreValidated()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "content", "leaders", "steady.json"),
                "{\"id\":\"steady\",\"name\":\"Steady\",\"healthModifier\":-30,\"armor\":-1,\"unexpected\":true}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_VALUE" && issue.Path == "$.healthModifier");
            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_VALUE" && issue.Path == "$.armor");
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_KEY" && issue.Path == "$.unexpected");
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
            Assert.Contains(exception.Report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_KEY" &&
                issue.File == "rules/combat.json" &&
                issue.Path == "$.startingSidePolicy");
            Assert.Contains(exception.Report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_KEY" &&
                issue.File == "rules/combat.json" &&
                issue.Path == "$.postCombatDamagePolicy");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_MissingRequiredContentDirectory_IsReportedInsteadOfThrown()
    {
        var path = CreateTempMod();
        try
        {
            Directory.Delete(Path.Combine(path, "content", "types"), recursive: true);

            var report = new ModValidator().Validate(path);

            var issue = Assert.Single(report.Issues, issue => issue.Code == "MISSING_REQUIRED_DIRECTORY");
            Assert.Equal("content/types", issue.File);
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
