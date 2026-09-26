using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class DynamicEffectValueValidationTests
{
    [Fact]
    public void Load_AcceptsNestedDynamicValues()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "units", "guard.json"),
                """
                {
                  "id": "guard",
                  "name": "Guard",
                  "tier": 1,
                  "attack": 2,
                  "health": 2,
                  "types": ["construct"],
                  "tags": ["starter"],
                  "triggers": [
                    {
                      "event": "onCombatStart",
                      "effects": [
                        {
                          "kind": "modifyStats",
                          "target": { "scope": "self" },
                          "attack": {
                            "kind": "multiply",
                            "values": [
                              2,
                              {
                                "kind": "unitCount",
                                "query": { "scope": "friendly", "tagId": "starter" }
                              }
                            ]
                          },
                          "health": { "kind": "sourceStat", "stat": "attack" }
                        }
                      ]
                    }
                  ]
                }
                """);

            var mod = new ModLoader().Load(path);
            var trigger = Assert.Single(mod.Units.GetRequired(new UnitId("guard")).Triggers);
            var effect = Assert.IsType<ModifyStatsEffectDefinition>(Assert.Single(trigger.Effects));

            Assert.IsType<CompositeEffectValueExpression>(effect.AttackDelta);
            Assert.IsType<SourceStatEffectValueExpression>(effect.HealthDelta);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_ReportsInvalidExpressionContextAndNestedReference()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "content", "units", "scout.json"),
                """
                {
                  "id": "scout",
                  "name": "Scout",
                  "tier": 1,
                  "attack": 1,
                  "health": 2,
                  "triggers": [
                    {
                      "event": "onPlay",
                      "effects": [
                        {
                          "kind": "addResource",
                          "amount": { "kind": "targetStat", "stat": "attack" }
                        },
                        {
                          "kind": "summonUnit",
                          "unitId": "guard",
                          "count": {
                            "kind": "unitCount",
                            "query": { "scope": "friendly", "typeId": "missing-type" }
                          }
                        }
                      ]
                    }
                  ]
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_VALUE_EXPRESSION_CONTEXT" && issue.Path.EndsWith(".amount.kind"));
            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_REFERENCE" && issue.Path.EndsWith(".count.query.typeId"));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-dynamic-values", Guid.NewGuid().ToString("N"));
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
