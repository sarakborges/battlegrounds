using System.Text.Json;
using System.Text.Json.Nodes;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class PresentationLocalizationTests
{
    [Fact]
    public void Resolve_UnknownLocaleFallsBackToDefaultLocale()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var mod = new ModLoader().Load(path);

        var text = mod.Presentation.Resolve("fr-CA");

        Assert.Equal("en", text.Locale);
        Assert.Equal("Leader", text.Term("leader"));
        Assert.Equal("Refresh", text.Get("ui.refresh"));
    }

    [Fact]
    public void Resolve_AuthoredEntityNameFallsBackWithoutLocaleOverride()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var mod = new ModLoader().Load(path);

        var text = mod.Presentation.Resolve("en");
        var authoredName = mod.Units.GetRequired(new UnitId("scout")).Name;

        Assert.Equal(authoredName, text.EntityName(ModPresentationEntityKind.Unit, "scout"));
        Assert.False(text.TryEntityDescription(ModPresentationEntityKind.Unit, "scout", out _));
    }

    [Fact]
    public void Resolve_EntityOverridesAndDescriptionsUseSelectedLocale()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var mod = new ModLoader().Load(path);

        var text = mod.Presentation.Resolve("pt-BR");

        Assert.Equal("Estável", text.EntityName(ModPresentationEntityKind.Leader, "steady"));
        Assert.Equal("Aura Silenciosa", text.EntityName(ModPresentationEntityKind.Power, "quiet-aura"));
        Assert.Equal("Batedor", text.EntityName(ModPresentationEntityKind.Unit, "scout"));
        Assert.Equal("Treinamento", text.EntityName(ModPresentationEntityKind.Action, "training"));
        Assert.Equal("Golpe Duplo", text.EntityName(ModPresentationEntityKind.Behavior, "double-strike"));
        Assert.Equal("Construto", text.EntityName(ModPresentationEntityKind.UnitType, "construct"));
        Assert.Equal("Inicial", text.EntityName(ModPresentationEntityKind.Tag, "starter"));
        Assert.Equal("Fusão de Batedores", text.EntityName(ModPresentationEntityKind.Combine, "scout-merge"));
        Assert.True(text.TryEntityDescription(ModPresentationEntityKind.Unit, "scout", out var description));
        Assert.Equal("Uma unidade leve que devolve Energia ao entrar em campo.", description);
    }

    [Fact]
    public void Resolve_PartialLocaleFallsBackPerStringToDefaultLocale()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "localization", "es.json"),
                "{\"term.leader\":\"Líder\",\"ui.refresh\":\"Actualizar\",\"entity.unit.scout.name\":\"Explorador\"}");

            var mod = new ModLoader().Load(path);
            var text = mod.Presentation.Resolve("es-MX");

            Assert.Equal("es", text.Locale);
            Assert.Equal("Líder", text.Term("leader"));
            Assert.Equal("Actualizar", text.Get("ui.refresh"));
            Assert.Equal("Confirm", text.Get("ui.confirm"));
            Assert.Equal("Explorador", text.EntityName(ModPresentationEntityKind.Unit, "scout"));
            Assert.Equal(
                mod.Units.GetRequired(new UnitId("guard")).Name,
                text.EntityName(ModPresentationEntityKind.Unit, "guard"));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_MissingLocalizationDirectoryIsReported()
    {
        var path = CreateTempMod();
        try
        {
            Directory.Delete(Path.Combine(path, "localization"), recursive: true);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_DIRECTORY" &&
                issue.File == "localization");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_DefaultLocaleMustContainRequiredPresentationStrings()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "localization", "en.json"), "{\"ui.loading\":\"Loading...\"}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_PRESENTATION_STRING" &&
                issue.File == "localization/en.json" &&
                issue.Path == "$.ui.chooseLeader");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_RequiredTerminologyMustExistInManifest()
    {
        var path = CreateTempMod();
        try
        {
            var manifest = File.ReadAllText(Path.Combine(path, "mod.json"));
            manifest = manifest.Replace("    \"power\": \"Power\",\n", string.Empty, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(path, "mod.json"), manifest);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_PRESENTATION_TERM" &&
                issue.File == "mod.json" &&
                issue.Path == "$.terminology.power");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_MalformedEntityLocalizationKeyIsReported()
    {
        var path = CreateTempMod();
        try
        {
            AddLocalizationEntry(path, "pt-BR", "entity.units.scout.name", "Batedor");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_ENTITY_LOCALIZATION_KEY" &&
                issue.File == "localization/pt-BR.json" &&
                issue.Path == "$.entity.units.scout.name");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_UnknownEntityLocalizationReferenceIsReported()
    {
        var path = CreateTempMod();
        try
        {
            AddLocalizationEntry(path, "pt-BR", "entity.unit.missing.name", "Ausente");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_ENTITY_LOCALIZATION_REFERENCE" &&
                issue.File == "localization/pt-BR.json" &&
                issue.Path == "$.entity.unit.missing.name");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void AddLocalizationEntry(string modPath, string locale, string key, string value)
    {
        var localizationPath = Path.Combine(modPath, "localization", locale + ".json");
        var root = JsonNode.Parse(File.ReadAllText(localizationPath))?.AsObject()
            ?? throw new InvalidDataException("Test localization file must contain a JSON object.");
        root[key] = value;
        File.WriteAllText(
            localizationPath,
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-presentation-validation", Guid.NewGuid().ToString("N"));
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
