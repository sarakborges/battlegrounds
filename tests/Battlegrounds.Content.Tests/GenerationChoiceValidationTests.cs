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
    public void Validate_AcceptsUnitAndActionGenerationAcrossPreparationTriggers()
    {
        var path = CreateTempMod();
        try
        {
            var generationEffects = """
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
                },
                { "kind": "generateActionToReserve", "actionId": "training", "count": 1 },
                {
                  "kind": "generateActionChoice",
                  "actionQuery": {
                    "minimumTier": 1,
                    "maximumTier": 2,
                    "excludeActionId": "mutation-lab"
                  },
                  "optionCount": 2
                }
                """;

            WriteScout(
                path,
                $$"""
                {
                  "event": "onPlay",
                  "effects": [
                    {{generationEffects}}
                  ]
                }
                """);

            File.WriteAllText(
                Path.Combine(path, "content", "powers", "quiet-aura.json"),
                $$"""
                {
                  "id": "quiet-aura",
                  "name": "Quiet Aura",
                  "triggers": [
                    {
                      "event": "onTurnStart",
                      "effects": [
                        {{generationEffects}}
                      ]
                    }
                  ]
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.True(
                report.IsValid,
                string.Join(Environment.NewLine, report.Issues.Select(issue => $"{issue.Code} {issue.File} {issue.Path}: {issue.Message}")));
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

    [Fact]
    public void Validate_RejectsPendingChoicesAtTurnEndButAllowsDirectGeneration()
    {
        var path = CreateTempMod();
        try
        {
            WriteScout(
                path,
                """
                {
                  "event": "onTurnEnd",
                  "effects": [
                    { "kind": "generateUnitToReserve", "unitId": "guard", "count": 1 },
                    {
                      "kind": "generateUnitChoice",
                      "generationQuery": { "typeId": "construct" },
                      "optionCount": 1
                    }
                  ]
                }
                """);

            File.WriteAllText(
                Path.Combine(path, "content", "powers", "quiet-aura.json"),
                """
                {
                  "id": "quiet-aura",
                  "name": "Quiet Aura",
                  "triggers": [
                    {
                      "event": "onTurnEnd",
                      "effects": [
                        { "kind": "generateActionToReserve", "actionId": "training", "count": 1 },
                        {
                          "kind": "generateActionChoice",
                          "actionQuery": { "minimumTier": 1, "maximumTier": 2 },
                          "optionCount": 1
                        }
                      ]
                    }
                  ]
                }
                """);

            var report = new ModValidator().Validate(path);
            var contextIssues = report.Issues
                .Where(issue => issue.Code == "INVALID_EFFECT_CONTEXT")
                .ToArray();

            Assert.Contains(contextIssues, issue =>
                issue.File.EndsWith("scout.json", StringComparison.Ordinal) &&
                issue.Path.EndsWith(".effects[1].kind", StringComparison.Ordinal));
            Assert.Contains(contextIssues, issue =>
                issue.File.EndsWith("quiet-aura.json", StringComparison.Ordinal) &&
                issue.Path.EndsWith(".effects[1].kind", StringComparison.Ordinal));
            Assert.DoesNotContain(contextIssues, issue => issue.Path.EndsWith(".effects[0].kind", StringComparison.Ordinal));
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
