using System.Text.Json;
using System.Text.Json.Nodes;
using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class ModThemeTests
{
    [Fact]
    public void Load_ExampleThemeExposesDesignTokensAndComponentStates()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");

        var theme = new ModThemeLoader().Load(path);

        Assert.False(theme.IsEmpty);
        Assert.Equal(1, theme.Version);
        Assert.Equal("#E7A93D", theme.Colors["primary"]);
        Assert.Equal(30, theme.FontSizes["title"]);
        Assert.Equal(12, theme.Spacing["md"]);
        Assert.Equal(9, theme.Radii["medium"]);

        var primary = theme.Components[ModThemeComponentRoles.ButtonPrimary];
        Assert.Equal("primary", primary.BackgroundColor);
        Assert.Equal("assets/ui/button-primary.svg", primary.BackgroundAsset);
        Assert.Equal(14, primary.Slice?.Left);
        Assert.Equal("primaryHover", primary.States["hover"].BackgroundColor);
        Assert.Equal("background", theme.Screens[ModThemeScreenRoles.Preparation].BackgroundColor);

        var dragPreview = theme.Components["drag.preview"];
        Assert.Equal(0.12, dragPreview.Opacity);

        var validDropTarget = theme.Components["dropTarget.valid"];
        Assert.Equal("success", validDropTarget.BorderColor);
        Assert.Equal(2, validDropTarget.BorderWidth);
        Assert.Equal("sm", validDropTarget.Padding?.Horizontal);

        var activeValidDropTarget = theme.Components["dropTarget.valid.active"];
        Assert.Equal("successActive", activeValidDropTarget.BorderColor);
        Assert.Equal(4, activeValidDropTarget.BorderWidth);

        var invalidDropTarget = theme.Components["dropTarget.invalid"];
        Assert.Equal("danger", invalidDropTarget.BorderColor);
        Assert.Equal(2, invalidDropTarget.BorderWidth);
        Assert.Equal("sm", invalidDropTarget.Padding?.Horizontal);

        var activeInvalidDropTarget = theme.Components["dropTarget.invalid.active"];
        Assert.Equal("dangerActive", activeInvalidDropTarget.BorderColor);
        Assert.Equal(4, activeInvalidDropTarget.BorderWidth);
    }

    [Fact]
    public void Load_MissingThemeProducesEmptyCatalog()
    {
        var path = CreateTempMod();
        try
        {
            File.Delete(Path.Combine(path, "presentation", "theme.json"));

            var report = new ModValidator().Validate(path);
            var theme = new ModThemeLoader().Load(path);

            Assert.True(report.IsValid);
            Assert.True(theme.IsEmpty);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_UnknownThemeTokenIsReported()
    {
        var path = CreateTempMod();
        try
        {
            UpdateTheme(path, root =>
            {
                root["components"]!["button"]!["textColor"] = "missingColor";
            });

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_TOKEN" &&
                issue.File == "presentation/theme.json" &&
                issue.Path == "$.components.button.textColor");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_ThemeAssetPathCannotEscapeModDirectory()
    {
        var path = CreateTempMod();
        try
        {
            UpdateTheme(path, root =>
            {
                root["screens"]!["preparation"]!["backgroundAsset"] = "../outside.png";
            });

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "ASSET_PATH_ESCAPE" &&
                issue.Path == "$.screens.preparation.backgroundAsset");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_UnknownThemePropertyIsReported()
    {
        var path = CreateTempMod();
        try
        {
            UpdateTheme(path, root => root["magic"] = true);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_KEY" && issue.Path == "$.magic");
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
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-theme", Guid.NewGuid().ToString("N"));
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
