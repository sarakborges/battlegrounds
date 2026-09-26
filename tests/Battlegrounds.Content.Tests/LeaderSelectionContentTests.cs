using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Leaders;

namespace Battlegrounds.Content.Tests;

public sealed class LeaderSelectionContentTests
{
    [Fact]
    public void Load_ReadsLeaderSelectionRules()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "mods", "example");

        var mod = new ModLoader().Load(path);

        Assert.Equal(2, mod.LeaderSelectionRules.OfferSize);
        Assert.Equal(LeaderOfferPolicy.IndependentPerPlayer, mod.LeaderSelectionRules.OfferPolicy);
    }

    [Fact]
    public void Validate_MissingSetupRules_IsReported()
    {
        var path = CreateTempMod();
        try
        {
            File.Delete(Path.Combine(path, "rules", "setup.json"));

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "MISSING_REQUIRED_FILE" &&
                issue.File == "rules/setup.json");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_SetupRulesRejectUnknownAndMissingKeys()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "rules", "setup.json"),
                "{\"leaderOfferSize\":0,\"unexpected\":true}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue => issue.Code == "INVALID_VALUE" && issue.Path == "$.leaderOfferSize");
            Assert.Contains(report.Issues, issue => issue.Code == "MISSING_REQUIRED_KEY" && issue.Path == "$.leaderOfferPolicy");
            Assert.Contains(report.Issues, issue => issue.Code == "UNKNOWN_KEY" && issue.Path == "$.unexpected");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public void Validate_UniqueOffersRequireEnoughLeadersForMaximumLobby()
    {
        var path = CreateTempMod();
        try
        {
            File.WriteAllText(
                Path.Combine(path, "rules", "setup.json"),
                "{\"leaderOfferSize\":2,\"leaderOfferPolicy\":\"uniqueAcrossMatch\"}");

            var report = new ModValidator().Validate(path);

            Assert.Contains(report.Issues, issue =>
                issue.Code == "INSUFFICIENT_LEADERS_FOR_OFFERS" &&
                issue.File == "rules/setup.json" &&
                issue.Path == "$.leaderOfferSize");
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string CreateTempMod()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        var target = Path.Combine(Path.GetTempPath(), "battlegrounds-leader-selection", Guid.NewGuid().ToString("N"));
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
