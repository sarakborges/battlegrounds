using System.Text.Json;
using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _webCosmeticsLoaded;
    private Dictionary<string, object?>? _webCosmetics;
    private readonly Dictionary<string, (byte[] Bytes, string MimeType)> _webTransportAssets = new(StringComparer.Ordinal);
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
        _webCosmetics["leaderPowers"] = BuildWebLeaderPowersState();
        return _webCosmetics;
    }

    private object BuildWebLeaderPowersState()
    {
        var powers = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (_session is null)
            return powers;

        foreach (var leader in _session.Mod.Leaders.All)
        {
            if (leader.InitialPowerId is null)
                continue;

            var power = _session.Mod.Powers.GetRequired(leader.InitialPowerId.Value);
            powers[leader.Id.Value] = new
            {
                id = power.Id.Value,
                name = PowerName(power.Id),
                description = PowerDescription(power.Id),
                cost = power.Cost,
                activatable = power.IsActivatable,
            };
        }

        return powers;
    }

    private object BuildWebPresentationAssetsState()
    {
        EnsureWebPresentationAssetsLoaded();
        var leaders = new Dictionary<string, string>(StringComparer.Ordinal);
        var units = new Dictionary<string, string>(StringComparer.Ordinal);
        var actions = new Dictionary<string, string>(StringComparer.Ordinal);

        // Populate conventional paths first so one malformed/partial presentation
        // catalog cannot make authored card art disappear from the Web UI.
        foreach (var leader in _session?.Mod.Leaders.All ?? [])
            leaders[leader.Id.Value] = $"assets/cosmetics/leaders/{leader.Id.Value}/base.png";

        foreach (var unit in _session?.Mod.Units.All ?? [])
            units[unit.Id.Value] = $"assets/units/{unit.Id.Value}.webp";

        foreach (var action in _session?.Mod.Actions.All ?? [])
            actions[action.Id.Value] = $"assets/actions/{action.Id.Value}.webp";

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
            if (Path.GetExtension(fullPath).Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                bytes.Length > 512 * 1024 &&
                !_webTransportAssets.TryGetValue(normalized, out var cachedTransport))
            {
                var image = Image.LoadFromFile(fullPath);
                if (image is not null && !image.IsEmpty())
                {
                    var webp = image.SaveWebpToBuffer(false);
                    if (webp is not null && webp.Length > 0 && webp.Length < bytes.Length)
                    {
                        _webTransportAssets[normalized] = (webp, "image/webp");
                        bytes = webp;
                        mimeType = "image/webp";
                    }
                }
            }
            else if (cachedTransport.Bytes is not null)
            {
                bytes = cachedTransport.Bytes;
                mimeType = cachedTransport.MimeType;
            }

            const int maxIpcBytes = 48 * 1024;
            if (bytes.Length <= maxIpcBytes)
            {
                var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
                SendWebUi("asset", new { path = normalized, dataUrl });
                return;
            }

            var chunkCount = (bytes.Length + maxIpcBytes - 1) / maxIpcBytes;
            SendWebUi("asset", new
            {
                path = normalized,
                chunked = true,
                chunkCount,
                mimeType
            });

            for (var chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
            {
                var offset = chunkIndex * maxIpcBytes;
                var count = Math.Min(maxIpcBytes, bytes.Length - offset);
                var chunkData = Convert.ToBase64String(bytes, offset, count);
                // Keep each CEF IPC message comfortably below the transport limit.
                // Godot/CEF serializes the base64 string inside JSON, so the raw
                // byte chunk must be smaller than the apparent IPC budget.
                _webUiHost?.CallDeferred("send_message", JsonSerializer.Serialize(new
                {
                    type = "asset",
                    payload = new
                    {
                        path = normalized,
                        chunkIndex,
                        chunkData
                    }
                }, WebJsonOptions));
            }
        }
        catch (Exception exception)
        {
            AppendLog($"Web asset '{normalized}' could not be loaded: {exception.Message}");
            SendWebUi("asset", new { path = normalized, dataUrl = (string?)null });
        }
    }
}
