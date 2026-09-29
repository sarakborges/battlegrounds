using System.Text.Json;
using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Playables;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _webCosmeticsLoaded;
    private JsonElement? _webCosmetics;
    private bool _webPresentationAssetsLoaded;
    private ModPresentationAssetCatalog? _webPresentationAssets;

    private object? BuildWebCosmeticsState()
    {
        if (_webCosmeticsLoaded)
            return _webCosmetics;

        _webCosmeticsLoaded = true;
        try
        {
            var modDirectory = Path.GetFullPath(ProjectSettings.GlobalizePath(ModPath));
            var cosmeticsPath = Path.Combine(modDirectory, "presentation", "cosmetics.json");
            if (!File.Exists(cosmeticsPath))
                return null;

            using var document = JsonDocument.Parse(File.ReadAllText(cosmeticsPath));
            _webCosmetics = document.RootElement.Clone();
            return _webCosmetics;
        }
        catch (Exception exception)
        {
            AppendLog($"Presentation cosmetics could not be loaded: {exception.Message}");
            return null;
        }
    }

    private string? PlayableArtPath(PlayableKind kind, string id) => kind switch
    {
        PlayableKind.Unit => PresentationArtPath(ModPresentationEntityKind.Unit, id, ModPresentationAssetSlots.Art),
        PlayableKind.Action => PresentationArtPath(ModPresentationEntityKind.Action, id, ModPresentationAssetSlots.Art),
        _ => null,
    };

    private string? UnitArtPath(string id) =>
        PresentationArtPath(ModPresentationEntityKind.Unit, id, ModPresentationAssetSlots.Art);

    private string? PresentationArtPath(ModPresentationEntityKind kind, string id, string slot)
    {
        EnsureWebPresentationAssetsLoaded();
        return _webPresentationAssets is not null && _webPresentationAssets.TryGet(kind, id, slot, out var asset)
            ? asset.RelativePath
            : null;
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
