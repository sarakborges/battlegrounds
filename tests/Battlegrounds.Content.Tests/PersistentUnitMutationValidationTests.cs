using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class PersistentUnitMutationValidationTests
{
    [Fact]
    public void Load_MapsPersistentMutationEffectsFromActions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var mod = new ModLoader().Load(path);

        var action = mod.Actions.GetRequired(new ActionId("mutation-lab"));
        Assert.Collection(
            action.Effects,
            effect =>
            {
                var apply = Assert.IsType<ApplyUnitModifierEffectDefinition>(effect);
                Assert.Equal("mutation-lab", apply.ModifierKey);
                Assert.Equal(EffectTargetScope.Selected, apply.Target.Scope);
            },
            effect => Assert.IsType<CopyUnitToReserveEffectDefinition>(effect),
            effect =>
            {
                var remove = Assert.IsType<RemoveUnitModifierEffectDefinition>(effect);
                Assert.Equal("mutation-lab", remove.ModifierKey);
            },
            effect =>
            {
                var transform = Assert.IsType<TransformUnitEffectDefinition>(effect);
                Assert.Equal(new UnitId("guard"), transform.UnitId);
                Assert.Equal(EffectTargetScope.Selected, transform.Target.Scope);
            });
    }

    [Fact]
    public void Validate_RejectsPersistentMutationFromCombatTrigger()
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
                      "kind": "transformUnit",
                      "target": { "scope": "self" },
                      "unitId": "guard"
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
    public void Validate_RejectsUnknownTransformUnitAndIncompleteModifier()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "actions", "mutation-lab.json"),
                """
                {
                  "id": "mutation-lab",
                  "name": "Mutation Lab",
                  "tier": 1,
                  "cost": 0,
                  "effects": [
                    {
                      "kind": "transformUnit",
                      "target": { "scope": "selected" },
                      "unitId": "missing-unit"
                    },
                    {
                      "kind": "applyUnitModifier",
                      "target": { "scope": "selected" },
                      "modifierKey": "empty-change"
                    }
                  ]
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_REFERENCE" && issue.Path.EndsWith(".unitId", StringComparison.Ordinal));
            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_PARAMETER" &&
                issue.Message.Contains("attack and/or health", StringComparison.Ordinal));
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
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-persistent-mutation", Guid.NewGuid().ToString("N"));
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
