using System.Text.Json;
using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _webCosmeticsLoaded;
    private Dictionary<string, object?>? _webCosmetics;
    private bool _webPresentationAssetsLoaded;
    private ModPresentationAssetCatalog? _webPresentationAssets;

    private object BuildWebCosmeticsState()
    {
        if (_webCosmeticsLoaded)
            return _webCosmetics ?? new Dictionary<string, object?>();

        _webCosmeticsLoaded = true;
        _webCosmetics = new Dictionary<string, object?>(StringComparer.Ordinal);
        try
        {
            var modDirectory = Path.GetFullPath(ProjectSettings.GlobalizePath(ModPath));
            var cosmeticsPath = Path.Combine(modDirectory, "presentation", "cosmetics.json");
            if (File.Exists(cosmeticsPath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(cosmeticsPath));
                foreach (var property in document.RootElement.EnumerateObject())
                    _webCosmetics[property.Name] = property.Value.Clone();
            }
        }
        catch (Exception exception)
        {
            AppendLog($"Presentation cosmetics could not be loaded: {exception.Message}");
        }

        _webCosmetics["presentationAssets"] = BuildWebPresentationAssetsState();
        return _webCosmetics;
    }

    private object BuildWebPresentationAssetsState()
    {
        EnsureWebPresentationAssetsLoaded();
        var leaders = new Dictionary<string, string>(StringComparer.Ordinal);
        var units = new Dictionary<string, string>(StringComparer.Ordinal);
        var actions = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in _webPresentationAssets?.All ?? [])
        {
            if (entry.Asset.Type != ModPresentationAssetType.Image)
                continue;

            switch (entry.EntityKind)
            {
                case ModPresentationEntityKind.Leader when entry.Slot == ModPresentationAssetSlots.Portrait:
                    leaders[entry.EntityId] = entry.Asset.RelativePath;
                    break;
                case ModPresentationEntityKind.Unit when entry.Slot == ModPresentationAssetSlots.Art:
                    units[entry.EntityId] = entry.Asset.RelativePath;
                    break;
                case ModPresentationEntityKind.Action when entry.Slot == ModPresentationAssetSlots.Art:
                    actions[entry.EntityId] = entry.Asset.RelativePath;
                    break;
            }
        }

        return new { leaders, units, actions };
    }

    private void EnsureWebPresentationAssetsLoaded()
    {
        if (_webPresentationAssetsLoaded)
            return;

        _webPresentationAssetsLoaded = true;
        try
        {
            var modDirectory = Path.GetFullPath(ProjectSettings.GlobalizePath(ModPath));
            _webPresentationAssets = new ModPresentationAssetLoader().Load(modDirectory);
        }
        catch (Exception exception)
        {
            AppendLog($"Presentation assets could not be loaded: {exception.Message}");
            _webPresentationAssets = null;
        }
    }

    private void SendWebAsset(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (!normalized.StartsWith("assets/", StringComparison.Ordinal) ||
            normalized.Split('/').Any(segment => segment is ".." or "."))
        {
            SendWebUi("asset", new { path = normalized, dataUrl = (string?)null });
            return;
        }

        try
        {
            var modDirectory = Path.GetFullPath(ProjectSettings.GlobalizePath(ModPath));
            var localPath = normalized.Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(modDirectory, localPath));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var rootPrefix = modDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(rootPrefix, comparison) || !File.Exists(fullPath))
            {
                SendWebUi("asset", new { path = normalized, dataUrl = (string?)null });
                return;
            }

            var mimeType = Path.GetExtension(fullPath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ttf" => "font/ttf",
                ".otf" => "font/otf",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                _ => null,
            };
            if (mimeType is null)
            {
                SendWebUi("asset", new { path = normalized, dataUrl = (string?)null });
                return;
            }

            var bytes = File.ReadAllBytes(fullPath);
            var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
            SendWebUi("asset", new { path = normalized, dataUrl });
        }
        catch (Exception exception)
        {
            AppendLog($"Web asset '{normalized}' could not be loaded: {exception.Message}");
            SendWebUi("asset", new { path = normalized, dataUrl = (string?)null });
        }
    }
}
