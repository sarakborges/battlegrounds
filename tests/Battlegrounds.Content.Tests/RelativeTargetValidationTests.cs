using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class RelativeTargetValidationTests
{
    [Fact]
    public void Validate_SelectedRelativeAdjacencyRequiresContextAndAllowsEnemyNeighbors()
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
                          "target": {
                            "scope": "enemy",
                            "selection": "adjacent",
                            "relativeTo": "selected"
                          },
                          "amount": 1
                        }
                      ]
                    }
                  ]
                }
                """;

            File.WriteAllText(unitPath, attackJson);
            var validReport = new ModValidator().Validate(path);
            Assert.True(
                validReport.IsValid,
                string.Join(Environment.NewLine, validReport.Issues.Select(issue =>
                    $"{issue.Code}: {issue.File} {issue.Path} {issue.Message}")));

            File.WriteAllText(
                unitPath,
                attackJson.Replace("onAttack", "onCombatStart", StringComparison.Ordinal));
            var invalidReport = new ModValidator().Validate(path);

            Assert.Contains(invalidReport.Issues, issue =>
                issue.Code == "INVALID_VALUE" && issue.Path.EndsWith(".target.relativeTo", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(
            Path.GetTempPath(),
            "battlegrounds-relative-targets",
            Guid.NewGuid().ToString("N"));
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
