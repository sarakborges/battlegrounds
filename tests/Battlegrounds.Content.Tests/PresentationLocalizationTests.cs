using Battlegrounds.Content;

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
    public void Resolve_PartialLocaleFallsBackPerStringToDefaultLocale()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "localization", "es.json"),
                "{\"term.leader\":\"Líder\",\"ui.refresh\":\"Actualizar\"}");

            var mod = new ModLoader().Load(path);
            var text = mod.Presentation.Resolve("es-MX");

            Assert.Equal("es", text.Locale);
            Assert.Equal("Líder", text.Term("leader"));
            Assert.Equal("Actualizar", text.Get("ui.refresh"));
            Assert.Equal("Confirm", text.Get("ui.confirm"));
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
