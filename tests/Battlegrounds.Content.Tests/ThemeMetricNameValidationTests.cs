using System.Text.Json;
using System.Text.Json.Nodes;
using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class ThemeMetricNameValidationTests
{
    [Theory]
    [InlineData("launcher.marginHorizontal")]
    [InlineData("layout.interaction.anchorLeft")]
    [InlineData("layout.combat.boardsGap")]
    [InlineData("row.offer.preferredCardWidth")]
    [InlineData("card.board.artHeight")]
    [InlineData("hud.opponent.portraitSize")]
    [InlineData("drag.preview.rotationDegrees")]
    public void MetricRegistry_RecognizesSupportedNames(string name)
    {
        Assert.True(ModThemeMetricNames.IsSupported(name));
    }

    [Theory]
    [InlineData("layout.combat.boardsGpa")]
    [InlineData("row.offer.preferredWidth")]
    [InlineData("card.board.imageHeight")]
    [InlineData("hud.opponent.portratSize")]
    [InlineData("custom.future.metric")]
    public void MetricRegistry_RejectsUnknownNames(string name)
    {
        Assert.False(ModThemeMetricNames.IsSupported(name));
    }

    [Fact]
    public void Validate_UnknownMetricNameIsReported()
    {
        var path = CreateTempMod();
        try
        {
            var themePath = Path.Combine(path, "presentation", "theme.json");
            var root = JsonNode.Parse(File.ReadAllText(themePath))?.AsObject()
                ?? throw new InvalidDataException("Test theme must contain a JSON object.");
            root["metrics"] = new JsonObject
            {
                ["layout.combat.boardsGpa"] = 24,
            };
            File.WriteAllText(themePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_METRIC" &&
                issue.File == "presentation/theme.json" &&
                issue.Path == "$.metrics.layout.combat.boardsGpa");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-theme-metric-names", Guid.NewGuid().ToString("N"));
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
