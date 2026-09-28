using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class AdvancedEffectValidationTests
{
    [Fact]
    public void Validate_ReportsInvalidTargetCombinationsAndConditionReferences()
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
                  "triggers": [
                    {
                      "event": "onCombatStart",
                      "conditions": [
                        {
                          "kind": "unitCount",
                          "query": { "scope": "friendly", "typeId": "missing" },
                          "comparison": "greaterThanOrEqual",
                          "value": 1
                        }
                      ],
                      "effects": [
                        {
                          "kind": "modifyStats",
                          "target": {
                            "scope": "enemy",
                            "selection": "adjacent",
                            "excludeSource": true,
                            "limit": 0
                          },
                          "attack": 1
                        }
                      ]
                    }
                  ]
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "UNKNOWN_REFERENCE" && issue.Path.EndsWith(".conditions[0].query.typeId"));
            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_PARAMETER" && issue.Path.EndsWith(".target.selection"));
            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_PARAMETER" && issue.Path.EndsWith(".target.excludeSource"));
            Assert.Contains(report.Issues, issue =>
                issue.Code == "INVALID_VALUE" && issue.Path.EndsWith(".target.limit"));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_AcceptsComposableTargetAndConditions()
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
                  "triggers": [
                    {
                      "event": "onCombatStart",
                      "conditions": [
                        {
                          "kind": "unitCount",
                          "query": { "scope": "friendly", "excludeSource": true, "typeId": "organic" },
                          "comparison": "greaterThanOrEqual",
                          "value": 1
                        },
                        {
                          "kind": "sourceStat",
                          "stat": "health",
                          "comparison": "greaterThan",
                          "value": 0
                        }
                      ],
                      "effects": [
                        {
                          "kind": "modifyStats",
                          "target": {
                            "scope": "friendly",
                            "selection": "lowestAttack",
                            "excludeSource": true,
                            "limit": 1,
                            "tagId": "starter"
                          },
                          "attack": 1
                        }
                      ]
                    }
                  ]
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Issues.Select(issue => $"{issue.Code}: {issue.File} {issue.Path} {issue.Message}")));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_SelectedUnitTargetRequiresContextProvidingTrigger()
    {
        var path = CreateTempMod();
        try
        {
            var unitPath = Path.Combine(path, "content", "units", "guard.json");
            var attackJson =
                """
                {
                  "id": "guard",
                  "name": "Guard",
                  "tier": 1,
                  "attack": 2,
                  "health": 2,
                  "triggers": [
                    {
                      "event": "onAttack",
                      "effects": [
                        {
                          "kind": "dealDamage",
                          "target": { "scope": "selected" },
                          "amount": {
                            "kind": "unitCount",
                            "query": { "scope": "selected" }
                          }
                        }
                      ]
                    }
                  ]
                }
                """;

            File.WriteAllText(unitPath, attackJson);
            var validReport = new ModValidator().Validate(path);
            Assert.True(validReport.IsValid, string.Join(Environment.NewLine, validReport.Issues.Select(issue => $"{issue.Code}: {issue.File} {issue.Path} {issue.Message}")));

            File.WriteAllText(unitPath, attackJson.Replace("onAttack", "onDamage", StringComparison.Ordinal));
            var damageReport = new ModValidator().Validate(path);
            Assert.True(damageReport.IsValid, string.Join(Environment.NewLine, damageReport.Issues.Select(issue => $"{issue.Code}: {issue.File} {issue.Path} {issue.Message}")));

            File.WriteAllText(unitPath, attackJson.Replace("onAttack", "onCombatStart", StringComparison.Ordinal));
            var invalidReport = new ModValidator().Validate(path);
            Assert.Contains(invalidReport.Issues, issue =>
                issue.Code == "INVALID_VALUE" && issue.Path.EndsWith(".target.scope"));
            Assert.Contains(invalidReport.Issues, issue =>
                issue.Code == "INVALID_VALUE" && issue.Path.EndsWith(".amount.query.scope"));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-advanced-effects", Guid.NewGuid().ToString("N"));
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
