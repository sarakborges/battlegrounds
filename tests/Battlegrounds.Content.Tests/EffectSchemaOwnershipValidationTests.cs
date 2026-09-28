using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class EffectSchemaOwnershipValidationTests
{
    [Fact]
    public void Validate_AcceptsAdvancedAndStatefulEffectSchemaWithoutCompatibilitySuppression()
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
                      "event": "afterEventCount",
                      "counter": {
                        "event": "unitAcquired",
                        "scope": "turn",
                        "typeId": "construct"
                      },
                      "count": 2,
                      "activationLimit": { "scope": "turn", "count": 1 },
                      "conditions": [
                        {
                          "kind": "unitCount",
                          "query": {
                            "scope": "friendly",
                            "excludeSource": true,
                            "typeId": "construct"
                          },
                          "comparison": "greaterThanOrEqual",
                          "value": 1
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

            File.WriteAllText(
                Path.Combine(path, "content", "powers", "steady-pulse.json"),
                """
                {
                  "id": "steady-pulse",
                  "name": "Steady Pulse",
                  "triggers": [
                    {
                      "event": "afterEventCount",
                      "counter": {
                        "event": "unitAcquired",
                        "scope": "turn"
                      },
                      "count": 1,
                      "activationLimit": { "scope": "turn", "count": 1 },
                      "conditions": [
                        {
                          "kind": "unitCount",
                          "query": { "scope": "friendly", "tagId": "starter" },
                          "comparison": "greaterThanOrEqual",
                          "value": 1
                        }
                      ],
                      "effects": [
                        {
                          "kind": "modifyStats",
                          "target": {
                            "scope": "friendly",
                            "selection": "lowestHealth",
                            "limit": 1
                          },
                          "health": 1
                        }
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

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-effect-schema", Guid.NewGuid().ToString("N"));
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
