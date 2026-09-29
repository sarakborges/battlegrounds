using System.Text.Json;
using System.Text.Json.Nodes;
using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class PresentationAssetTests
{
    [Fact]
    public void Load_ExampleAssetsAreIndexedByStableEntityId()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var assets = new ModPresentationAssetLoader().Load(path);
        Assert.Equal("assets/leaders/steady.svg", assets.GetRequired(ModPresentationEntityKind.Leader, "steady", ModPresentationAssetSlots.Portrait).RelativePath);
        Assert.Equal("assets/units/scout.svg", assets.GetRequired(ModPresentationEntityKind.Unit, "scout", ModPresentationAssetSlots.Art).RelativePath);
        Assert.Equal("assets/actions/training.svg", assets.GetRequired(ModPresentationEntityKind.Action, "training", ModPresentationAssetSlots.Art).RelativePath);
        Assert.False(assets.TryGet(ModPresentationEntityKind.Unit, "guard", ModPresentationAssetSlots.Art, out _));
    }

    [Fact]
    public void Load_ExampleCuesAreIndexedByEntityAndRole()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var cue = new ModPresentationCueLoader().Load(path)
            .GetRequired(ModPresentationEntityKind.Unit, "scout", ModPresentationCueRoles.CombatAttack);
        Assert.Equal(ModPresentationAnimation.Lunge, cue.Animation);
        Assert.Equal(0.2, cue.DurationSeconds);
    }

    [Fact]
    public void Load_MissingAssetsAndCardArtProducesEmptyCatalogs()
    {
        var path = CreateTempMod();
        try
        {
            RemoveCardArt(path, "units", "scout");
            RemoveCardArt(path, "actions", "training");
            Directory.Delete(Path.Combine(path, "assets"), recursive: true);
            Assert.True(new ModValidator().Validate(path).IsValid);
            Assert.Empty(new ModPresentationAssetLoader().Load(path).All);
            Assert.Empty(new ModPresentationCueLoader().Load(path).All);
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public void Validate_LegacyManifestCardArtIsRejected()
    {
        var path = CreateTempMod();
        try
        {
            SetManifestAsset(path, "units", "scout", "art", "assets/units/scout.svg");
            var report = new ModValidator().Validate(path);
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_PRESENTATION_ASSET_SLOT" && issue.Path == "$.units.scout.art");
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Theory]
    [InlineData("assets/../content/units/scout.json", "INVALID_PRESENTATION_ASSET_PATH")]
    [InlineData("assets/units/missing.svg", "MISSING_PRESENTATION_ASSET")]
    public void Validate_InlineCardArtPathIsValidated(string art, string expectedCode)
    {
        var path = CreateTempMod();
        try
        {
            SetCardArt(path, "units", "scout", art);
            var report = new ModValidator().Validate(path);
            Assert.Contains(report.Issues, issue => issue.Code == expectedCode && issue.File == "content/units/scout.json" && issue.Path == "$.art");
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public void Validate_InlineCardArtRejectsNonImageExtension()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "assets", "units", "scout.txt"), "not an image");
            SetCardArt(path, "units", "scout", "assets/units/scout.txt");
            var report = new ModValidator().Validate(path);
            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_PRESENTATION_ASSET_TYPE" && issue.Path == "$.art");
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    [Fact]
    public void Load_AudioCueStillUsesPresentationCueMetadata()
    {
        var path = CreateTempMod();
        try
        {
            Directory.CreateDirectory(Path.Combine(path, "assets", "audio"));
            File.WriteAllBytes(Path.Combine(path, "assets", "audio", "hit.wav"), [0x52, 0x49, 0x46, 0x46]);
            SetCue(path, "units", "scout", ModPresentationCueRoles.CombatDamage, new JsonObject
            {
                ["animation"] = "shake", ["durationSeconds"] = 0.15, ["audio"] = "assets/audio/hit.wav",
            });
            var cue = new ModPresentationCueLoader().Load(path)
                .GetRequired(ModPresentationEntityKind.Unit, "scout", ModPresentationCueRoles.CombatDamage);
            Assert.Equal("assets/audio/hit.wav", cue.Audio?.RelativePath);
            Assert.Equal(ModPresentationAnimation.Shake, cue.Animation);
        }
        finally { Directory.Delete(path, recursive: true); }
    }

    private static void SetCardArt(string modPath, string category, string entityId, string art)
    {
        var path = Path.Combine(modPath, "content", category, entityId + ".json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["art"] = art;
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void RemoveCardArt(string modPath, string category, string entityId)
    {
        var path = Path.Combine(modPath, "content", category, entityId + ".json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root.Remove("art");
        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SetManifestAsset(string modPath, string category, string entityId, string slot, string assetPath)
    {
        var manifestPath = Path.Combine(modPath, "assets", "presentation.json");
        var root = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var categoryObject = root[category]?.AsObject() ?? new JsonObject();
        root[category] = categoryObject;
        var entityObject = categoryObject[entityId]?.AsObject() ?? new JsonObject();
        categoryObject[entityId] = entityObject;
        entityObject[slot] = assetPath;
        File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void SetCue(string modPath, string category, string entityId, string role, JsonObject cue)
    {
        var manifestPath = Path.Combine(modPath, "assets", "presentation.json");
        var root = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        var categoryObject = root[category]?.AsObject() ?? new JsonObject();
        root[category] = categoryObject;
        var entityObject = categoryObject[entityId]?.AsObject() ?? new JsonObject();
        categoryObject[entityId] = entityObject;
        var cues = entityObject["cues"]?.AsObject() ?? new JsonObject();
        entityObject["cues"] = cues;
        cues[role] = cue;
        File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-presentation-assets", Guid.NewGuid().ToString("N"));
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
