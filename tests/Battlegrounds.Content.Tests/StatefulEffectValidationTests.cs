using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Effects;
using Battlegrounds.Core.Domain.Ids;

namespace Battlegrounds.Content.Tests;

public sealed class StatefulEffectValidationTests
{
    [Fact]
    public void Load_AcceptsCountedEventTriggerAndActivationLimit()
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
                      "effects": [
                        {
                          "kind": "addResource",
                          "amount": {
                            "kind": "eventCount",
                            "event": "unitAcquired",
                            "scope": "turn"
                          }
                        }
                      ]
                    }
                  ]
                }
                """);

            var mod = new ModLoader().Load(path);
            var trigger = Assert.Single(mod.Units.GetRequired(new UnitId("guard")).Triggers);

            Assert.Equal(NativeTriggerKeys.AfterEventCount, trigger.Event);
            Assert.Equal(2, trigger.Count);
            Assert.NotNull(trigger.Counter);
            Assert.Equal(NativeGameEventKeys.UnitAcquired, trigger.Counter!.Event);
            Assert.Equal(EffectHistoryScope.Turn, trigger.Counter.Scope);
            Assert.Equal(new UnitTypeId("construct"), trigger.Counter.RequiredTypeId);
            Assert.Equal(new TriggerActivationLimit(EffectHistoryScope.Turn, 1), trigger.ActivationLimit);
            var effect = Assert.IsType<AddResourceEffectDefinition>(Assert.Single(trigger.Effects));
            Assert.IsType<EventCountEffectValueExpression>(effect.Amount);
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_ReportsMissingCounterForCountedEventTrigger()
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
                      "event": "afterEventCount",
                      "count": 2,
                      "effects": [ { "kind": "addResource", "amount": 1 } ]
                    }
                  ]
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_PARAMETER" &&
                issue.Path == "$.triggers[0].counter");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_ReportsInvalidHistoryQueryAndActivationLimit()
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
                      "event": "afterEventCount",
                      "counter": {
                        "event": "doesNotExist",
                        "scope": "forever",
                        "typeId": "missing-type"
                      },
                      "count": 1,
                      "activationLimit": { "scope": "turn", "count": 0 },
                      "effects": [ { "kind": "addResource", "amount": 1 } ]
                    }
                  ]
                }
                """);

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_VALUE" && issue.Path.EndsWith(".counter.event"));
            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_VALUE" && issue.Path.EndsWith(".counter.scope"));
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_REFERENCE" && issue.Path.EndsWith(".counter.typeId"));
            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_VALUE" && issue.Path.EndsWith(".activationLimit.count"));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-stateful-effects", Guid.NewGuid().ToString("N"));
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
