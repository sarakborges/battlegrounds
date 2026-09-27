using Battlegrounds.Content;

namespace Battlegrounds.Content.Tests;

public sealed class ModDiscoveryTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"battlegrounds-mod-discovery-{Guid.NewGuid():N}");

    [Fact]
    public void Discover_ReturnsDirectChildPackagesInDeterministicDirectoryOrder()
    {
        Directory.CreateDirectory(_tempRoot);
        CopyExampleMod(Path.Combine(_tempRoot, "zeta"));
        CopyExampleMod(Path.Combine(_tempRoot, "alpha"));

        var entries = new ModDiscovery().Discover(_tempRoot);

        Assert.Equal(["alpha", "zeta"], entries.Select(entry => entry.DirectoryName).ToArray());
        Assert.All(entries, entry => Assert.True(entry.IsValid));
        Assert.All(entries, entry => Assert.Equal("example", entry.Summary.Id));
        Assert.All(entries, entry => Assert.Equal("Example Mod", entry.DisplayName));
        Assert.All(entries, entry => Assert.Equal(1, entry.Summary.SchemaVersion));
    }

    [Fact]
    public void Discover_KeepsInvalidPackageInspectableWithoutLoadingIt()
    {
        Directory.CreateDirectory(_tempRoot);
        var invalidDirectory = Path.Combine(_tempRoot, "broken");
        CopyExampleMod(invalidDirectory);
        File.WriteAllText(Path.Combine(invalidDirectory, "mod.json"), "{");

        var entry = Assert.Single(new ModDiscovery().Discover(_tempRoot));

        Assert.False(entry.IsValid);
        Assert.NotEmpty(entry.Validation.Issues);
        Assert.Equal("broken", entry.DisplayName);
        Assert.Null(entry.Summary.Id);
        Assert.Null(entry.Summary.Name);
    }

    [Fact]
    public void Discover_RejectsMissingExplicitModsRoot()
    {
        var missing = Path.Combine(_tempRoot, "missing");

        var exception = Assert.Throws<DirectoryNotFoundException>(() => new ModDiscovery().Discover(missing));

        Assert.Contains("does not exist", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static void CopyExampleMod(string destination)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "mods", "example");
        CopyDirectory(source, destination);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));

        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
