using System.Text.Json;
using System.Text.Json.Nodes;
using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class ThemeRoleNameValidationTests
{
    [Theory]
    [InlineData("button")]
    [InlineData("button.primary")]
    [InlineData("panel.healthBadge")]
    [InlineData("panel.opponent.self")]
    [InlineData("label.health")]
    [InlineData("drag.preview")]
    [InlineData("dropTarget.valid.active")]
    public void ComponentRoleRegistry_RecognizesSupportedNames(string role)
    {
        Assert.True(ModThemeRoleNames.IsSupportedComponent(role));
    }

    [Theory]
    [InlineData("button.primry")]
    [InlineData("panel.healtBadge")]
    [InlineData("label.heath")]
    [InlineData("dropTarget.valid.activ")]
    public void ComponentRoleRegistry_RejectsUnknownNames(string role)
    {
        Assert.False(ModThemeRoleNames.IsSupportedComponent(role));
    }

    [Theory]
    [InlineData("launcher")]
    [InlineData("preparation")]
    [InlineData("combat")]
    public void ScreenRoleRegistry_RecognizesSupportedNames(string role)
    {
        Assert.True(ModThemeRoleNames.IsSupportedScreen(role));
    }

    [Fact]
    public void Validate_UnknownComponentRoleIsReported()
    {
        var path = CreateTempMod();
        try
        {
            UpdateTheme(path, root =>
            {
                root["components"]!["button.primry"] = new JsonObject();
            });

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_COMPONENT_ROLE" &&
                issue.File == "presentation/theme.json" &&
                issue.Path == "$.components.button.primry");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_UnknownScreenRoleIsReported()
    {
        var path = CreateTempMod();
        try
        {
            UpdateTheme(path, root =>
            {
                root["screens"]!["preperation"] = new JsonObject();
            });

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_SCREEN_ROLE" &&
                issue.File == "presentation/theme.json" &&
                issue.Path == "$.screens.preperation");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void UpdateTheme(string modPath, Action<JsonObject> update)
    {
        var themePath = Path.Combine(modPath, "presentation", "theme.json");
        var root = JsonNode.Parse(File.ReadAllText(themePath))?.AsObject()
            ?? throw new InvalidDataException("Test theme must contain a JSON object.");
        update(root);
        File.WriteAllText(themePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-theme-role-names", Guid.NewGuid().ToString("N"));
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
