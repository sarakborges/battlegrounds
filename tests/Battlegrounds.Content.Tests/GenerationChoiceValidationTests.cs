using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class GenerationChoiceValidationTests
{
    [Fact]
    public void Load_AcceptsPreparationGenerationAndChoiceEffects()
    {
        var path = CreateTempMod();
        try
        {
            WriteScout(
                path,
                """
                {
                  "event": "onPlay",
                  "effects": [
                    { "kind": "generateUnitToReserve", "unitId": "guard", "count": 2 },
                    {
                      "kind": "generateUnitChoice",
                      "generationQuery": {
                        "minimumTier": 1,
                        "maximumTier": 2,
                        "typeId": "construct",
                        "tagId": "starter",
                        "excludeSource": true
                      },
                      "optionCount": 3
                    }
                  ]
                }
                """);

            var mod = new ModLoader().Load(path);
            var trigger = Assert.Single(mod.Units.GetRequired(new UnitId("scout")).Triggers);
            var generate = Assert.IsType<GenerateUnitToReserveEffectDefinition>(trigger.Effects[0]);
            var choice = Assert.IsType<GenerateUnitChoiceEffectDefinition>(trigger.Effects[1]);

            Assert.Equal(new UnitId("guard"), generate.UnitId);
            Assert.Equal(3, choice.OptionCount);
            Assert.Equal(1, choice.Query.MinimumTier);
            Assert.Equal(2, choice.Query.MaximumTier);
            Assert.Equal(new UnitTypeId("construct"), choice.Query.RequiredTypeId);
            Assert.Equal(new TagId("starter"), choice.Query.RequiredTagId);
            Assert.True(choice.Query.ExcludeSource);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_RejectsGenerationFromCombatTrigger()
    {
        var path = CreateTempMod();
        try
        {
            WriteScout(
                path,
                """
                {
                  "event": "onCombatStart",
                  "effects": [
                    {
                      "kind": "generateUnitChoice",
                      "generationQuery": { "typeId": "construct" },
                      "optionCount": 3
                    }
                  ]
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_EFFECT_CONTEXT" &&
                issue.Path.EndsWith(".effects[0].kind", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void WriteScout(string root, string triggerJson)
    {
        File.WriteAllText(
            Path.Combine(root, "content", "units", "scout.json"),
            $$"""
            {
              "id": "scout",
              "name": "Scout",
              "tier": 1,
              "attack": 1,
              "health": 2,
              "types": ["construct"],
              "tags": ["starter"],
              "triggers": [
                {{triggerJson}}
              ]
            }
            """);
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-generation-choice", Guid.NewGuid().ToString("N"));
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
