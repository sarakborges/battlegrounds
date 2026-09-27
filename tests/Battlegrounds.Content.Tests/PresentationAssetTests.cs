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

        var portrait = assets.GetRequired(ModPresentationEntityKind.Leader, "steady", ModPresentationAssetSlots.Portrait);
        var unitArt = assets.GetRequired(ModPresentationEntityKind.Unit, "scout", ModPresentationAssetSlots.Art);
        var actionArt = assets.GetRequired(ModPresentationEntityKind.Action, "training", ModPresentationAssetSlots.Art);
        Assert.Equal(ModPresentationAssetType.Image, portrait.Type);
        Assert.Equal("assets/leaders/steady.svg", portrait.RelativePath);
        Assert.Equal("assets/units/scout.svg", unitArt.RelativePath);
        Assert.Equal("assets/actions/training.svg", actionArt.RelativePath);
        Assert.False(assets.TryGet(ModPresentationEntityKind.Unit, "guard", ModPresentationAssetSlots.Art, out _));
        Assert.Throws<NotSupportedException>(() => ((IList<ModPresentationAssetEntry>)assets.All).Add(assets.All[0]));
    }

    [Fact]
    public void Load_ExampleCuesAreIndexedByEntityAndRole()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");

        var cues = new ModPresentationCueLoader().Load(path);

        var attack = cues.GetRequired(ModPresentationEntityKind.Unit, "scout", ModPresentationCueRoles.CombatAttack);
        Assert.Equal(ModPresentationAnimation.Lunge, attack.Animation);
        Assert.Equal(0.2, attack.DurationSeconds);
        Assert.Null(attack.Audio);
        Assert.True(cues.TryGet(ModPresentationEntityKind.Leader, "steady", ModPresentationCueRoles.UiSelect, out _));
        Assert.Throws<NotSupportedException>(() => ((IList<ModPresentationCue>)cues.All).Add(cues.All[0]));
    }

    [Fact]
    public void Load_MissingManifestProducesEmptyCatalogs()
    {
        var path = CreateTempMod();
        try
        {
            Directory.Delete(Path.Combine(path, "assets"), recursive: true);

            var report = new ModValidator().Validate(path);
            var assets = new ModPresentationAssetLoader().Load(path);
            var cues = new ModPresentationCueLoader().Load(path);

            Assert.True(report.IsValid);
            Assert.Empty(assets.All);
            Assert.Empty(cues.All);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_UnknownEntityReferenceIsReported()
    {
        var path = CreateTempMod();
        try
        {
            SetAsset(path, "units", "missing", "art", "assets/units/scout.svg");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_PRESENTATION_ASSET_REFERENCE" &&
                issue.File == "assets/presentation.json" &&
                issue.Path == "$.units.missing");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_PathTraversalIsRejected()
    {
        var path = CreateTempMod();
        try
        {
            SetAsset(path, "units", "scout", "art", "assets/../content/units/scout.json");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_PRESENTATION_ASSET_PATH" &&
                issue.Path == "$.units.scout.art");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_MissingAssetIsReported()
    {
        var path = CreateTempMod();
        try
        {
            SetAsset(path, "units", "scout", "art", "assets/units/missing.svg");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_PRESENTATION_ASSET" &&
                issue.Path == "$.units.scout.art");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_ImageSlotRejectsNonImageExtension()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(Path.Combine(path, "assets", "units", "scout.txt"), "not an image");
            SetAsset(path, "units", "scout", "art", "assets/units/scout.txt");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_PRESENTATION_ASSET_TYPE" &&
                issue.Path == "$.units.scout.art");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_UnknownSlotIsReported()
    {
        var path = CreateTempMod();
        try
        {
            SetAsset(path, "leaders", "steady", "banner", "assets/leaders/steady.svg");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_PRESENTATION_ASSET_SLOT" &&
                issue.Path == "$.leaders.steady.banner");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_CueRejectsUnknownRoleAndAnimation()
    {
        var path = CreateTempMod();
        try
        {
            SetCue(path, "units", "scout", "combat.teleport", new JsonObject
            {
                ["animation"] = "warp",
                ["durationSeconds"] = 0.2,
            });

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_PRESENTATION_CUE_ROLE" && issue.Path == "$.units.scout.cues.combat.teleport");
            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_PRESENTATION_CUE_ANIMATION" && issue.Path == "$.units.scout.cues.combat.teleport.animation");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_CueDurationMustBeBoundedAndAnimationBacked()
    {
        var path = CreateTempMod();
        try
        {
            SetCue(path, "units", "scout", ModPresentationCueRoles.CombatDamage, new JsonObject
            {
                ["audio"] = "assets/audio/hit.wav",
                ["durationSeconds"] = 9.0,
            });
            Directory.CreateDirectory(Path.Combine(path, "assets", "audio"));
            File.WriteAllBytes(Path.Combine(path, "assets", "audio", "hit.wav"), [0x52, 0x49, 0x46, 0x46]);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_PRESENTATION_CUE_DURATION" && issue.Path == "$.units.scout.cues.combat.damage.durationSeconds");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_AudioCueUsesSafeExistingWavPath()
    {
        var path = CreateTempMod();
        try
        {
            SetCue(path, "units", "scout", ModPresentationCueRoles.CombatAttack, new JsonObject
            {
                ["audio"] = "assets/../outside.mp3",
            });

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_PRESENTATION_ASSET_PATH" && issue.Path == "$.units.scout.cues.combat.attack.audio");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Load_AudioCueExposesImmutableAudioReference()
    {
        var path = CreateTempMod();
        try
        {
            Directory.CreateDirectory(Path.Combine(path, "assets", "audio"));
            File.WriteAllBytes(Path.Combine(path, "assets", "audio", "hit.wav"), [0x52, 0x49, 0x46, 0x46]);
            SetCue(path, "units", "scout", ModPresentationCueRoles.CombatDamage, new JsonObject
            {
                ["animation"] = "shake",
                ["durationSeconds"] = 0.15,
                ["audio"] = "assets/audio/hit.wav",
            });

            var cue = new ModPresentationCueLoader().Load(path)
                .GetRequired(ModPresentationEntityKind.Unit, "scout", ModPresentationCueRoles.CombatDamage);

            Assert.Equal(ModPresentationAssetType.Audio, cue.Audio?.Type);
            Assert.Equal("assets/audio/hit.wav", cue.Audio?.RelativePath);
            Assert.Equal(ModPresentationAnimation.Shake, cue.Animation);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void SetAsset(string modPath, string category, string entityId, string slot, string assetPath)
    {
        var manifestPath = Path.Combine(modPath, "assets", "presentation.json");
        var root = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidDataException("Test asset manifest must contain a JSON object.");
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
        var root = JsonNode.Parse(File.ReadAllText(manifestPath))?.AsObject()
            ?? throw new InvalidDataException("Test asset manifest must contain a JSON object.");
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
