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

        Assert.True(theme.Components.ContainsKey(ModThemeRoleNames.UnitToken));
        Assert.True(theme.Components.ContainsKey(ModThemeRoleNames.ActionToken));
        Assert.True(theme.Components.ContainsKey(ModThemeRoleNames.UnitCardPreview));
        Assert.True(theme.Components.ContainsKey(ModThemeRoleNames.ActionCardPreview));

        var dragPreview = theme.Components[ModThemeInteractionRoles.DragPreview];
        Assert.Equal(0.12, dragPreview.Opacity);

        var validDropTarget = theme.Components[ModThemeInteractionRoles.DropTargetValid];
        Assert.Equal("success", validDropTarget.BorderColor);
        Assert.Equal(2, validDropTarget.BorderWidth);
        Assert.Equal("sm", validDropTarget.Padding?.Horizontal);

        var activeValidDropTarget = theme.Components[ModThemeInteractionRoles.DropTargetValidActive];
        Assert.Equal("successActive", activeValidDropTarget.BorderColor);
        Assert.Equal(4, activeValidDropTarget.BorderWidth);

        var invalidDropTarget = theme.Components[ModThemeInteractionRoles.DropTargetInvalid];
        Assert.Equal("danger", invalidDropTarget.BorderColor);
        Assert.Equal(2, invalidDropTarget.BorderWidth);
        Assert.Equal("sm", invalidDropTarget.Padding?.Horizontal);

        var activeInvalidDropTarget = theme.Components[ModThemeInteractionRoles.DropTargetInvalidActive];
        Assert.Equal("dangerActive", activeInvalidDropTarget.BorderColor);
        Assert.Equal(4, activeInvalidDropTarget.BorderWidth);
    }

    [Fact]
    public void Load_ThemeMetricsAreValidatedAndExposed()
    {
        var path = CreateTempMod();
        try
        {
            UpdateTheme(path, root =>
            {
                root["metrics"] = new JsonObject
                {
                    [ModThemeMetricKeys.Layout.PreparationScene.OfferTop] = 244.5,
                    [ModThemeMetricKeys.Layout.CombatScene.PlayerFieldTop] = 401,
                };
            });

            var report = new ModValidator().Validate(path);
            var theme = new ModThemeLoader().Load(path);

            Assert.True(report.IsValid);
            Assert.Equal(244.5, theme.Metrics[ModThemeMetricKeys.Layout.PreparationScene.OfferTop]);
            Assert.Equal(401, theme.Metrics[ModThemeMetricKeys.Layout.CombatScene.PlayerFieldTop]);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_InvalidThemeMetricIsReported()
    {
        var path = CreateTempMod();
        try
        {
            UpdateTheme(path, root =>
            {
                root["metrics"] = new JsonObject
                {
                    [ModThemeMetricKeys.Layout.PreparationScene.OfferTop] = "wide",
                };
            });

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_VALUE" &&
                issue.File == "presentation/theme.json" &&
                issue.Path == "$.metrics." + ModThemeMetricKeys.Layout.PreparationScene.OfferTop);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_LegacyCardAndRowThemeContractsAreRejected()
    {
        var path = CreateTempMod();
        try
        {
            UpdateTheme(path, root =>
            {
                root["metrics"] = new JsonObject
                {
                    ["row.offer.gap"] = 12,
                    ["card.board.minimumWidth"] = 120,
                };
                root["components"]!["card"] = new JsonObject { ["backgroundColor"] = "surface" };
                root["components"]!["card.board"] = new JsonObject { ["backgroundColor"] = "surface" };
                root["components"]!["playable-token"] = new JsonObject { ["backgroundColor"] = "surface" };
                root["components"]!["card-preview"] = new JsonObject { ["backgroundColor"] = "surface" };
            });

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_METRIC" && issue.Path == "$.metrics.row.offer.gap");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_METRIC" && issue.Path == "$.metrics.card.board.minimumWidth");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_COMPONENT_ROLE" && issue.Path == "$.components.card");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_COMPONENT_ROLE" && issue.Path == "$.components.card.board");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_COMPONENT_ROLE" && issue.Path == "$.components.playable-token");
            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_THEME_COMPONENT_ROLE" && issue.Path == "$.components.card-preview");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
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
