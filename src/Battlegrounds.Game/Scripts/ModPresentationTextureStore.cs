using System.Text.Json;
using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

internal sealed class ModPresentationTextureStore
{
    private const string BaseSkinId = "base";

    private readonly string _modDirectory;
    private readonly ModPresentationAssetCatalog _assets;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Texture2D?> _cosmeticTextures = new(StringComparer.Ordinal);
    private readonly CosmeticSelection _cosmetics;

    public ModPresentationTextureStore(string modDirectory)
        : this(modDirectory, new ModPresentationAssetLoader().Load(modDirectory))
    {
    }

    public ModPresentationTextureStore(string modDirectory, ModPresentationAssetCatalog assets)
    {
        if (string.IsNullOrWhiteSpace(modDirectory)) throw new ArgumentException("Mod directory cannot be empty.", nameof(modDirectory));
        _modDirectory = Path.GetFullPath(modDirectory);
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        _cosmetics = LoadCosmeticSelection();
    }

    public bool TryGetImage(
        ModPresentationEntityKind entityKind,
        string entityId,
        string slot,
        out Texture2D? texture)
    {
        texture = null;

        if (entityKind == ModPresentationEntityKind.Leader &&
            string.Equals(slot, ModPresentationAssetSlots.Portrait, StringComparison.Ordinal) &&
            TryGetCosmeticImage("leaders", entityId, ResolveLeaderSkin(entityId), out texture))
        {
            return true;
        }

        if (!_assets.TryGet(entityKind, entityId, slot, out var asset) || asset.Type != ModPresentationAssetType.Image)
            return false;

        if (_textures.TryGetValue(asset.RelativePath, out var cached))
        {
            texture = cached;
            return true;
        }

        var fullPath = Path.Combine(
            _modDirectory,
            asset.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var image = Image.LoadFromFile(fullPath);
        if (image.IsEmpty()) return false;

        texture = ImageTexture.CreateFromImage(image);
        _textures.Add(asset.RelativePath, texture);
        return true;
    }

    public bool TryGetShopkeeperImage(out Texture2D? texture)
    {
        texture = null;
        if (string.IsNullOrWhiteSpace(_cosmetics.ShopkeeperId))
            return false;

        return TryGetCosmeticImage(
            "shopkeepers",
            _cosmetics.ShopkeeperId,
            _cosmetics.ShopkeeperSkin,
            out texture);
    }

    public bool TryApplyImage(
        TextureRect target,
        ModPresentationEntityKind entityKind,
        string entityId,
        string slot)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!TryGetImage(entityKind, entityId, slot, out var texture)) return false;
        target.Texture = texture;
        return true;
    }

    public bool TryApplyIcon(
        Button target,
        ModPresentationEntityKind entityKind,
        string entityId,
        string slot)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!TryGetImage(entityKind, entityId, slot, out var texture)) return false;
        target.Icon = texture;
        return true;
    }

    private string ResolveLeaderSkin(string leaderId) =>
        _cosmetics.LeaderSkins.TryGetValue(leaderId, out var skinId) ? skinId : BaseSkinId;

    private bool TryGetCosmeticImage(
        string category,
        string entityId,
        string skinId,
        out Texture2D? texture)
    {
        texture = null;
        if (!IsSafeSegment(entityId) || !IsSafeSegment(skinId))
            return false;

        if (TryLoadCosmeticImage(category, entityId, skinId, out texture))
            return true;

        if (!string.Equals(skinId, BaseSkinId, StringComparison.Ordinal) &&
            TryLoadCosmeticImage(category, entityId, BaseSkinId, out texture))
        {
            return true;
        }

        return false;
    }

    private bool TryLoadCosmeticImage(
        string category,
        string entityId,
        string skinId,
        out Texture2D? texture)
    {
        var fullPath = Path.Combine(
            _modDirectory,
            "assets",
            "cosmetics",
            category,
            entityId,
            $"{skinId}.png");

        if (_cosmeticTextures.TryGetValue(fullPath, out texture) && texture is not null)
            return true;

        texture = null;
        if (!File.Exists(fullPath))
            return false;

        try
        {
            var image = new Image();
            var error = image.LoadPngFromBuffer(File.ReadAllBytes(fullPath));
            if (error != Error.Ok || image.IsEmpty())
            {
                GD.PushWarning($"Could not decode cosmetic PNG '{fullPath}': {error}.");
                return false;
            }

            texture = ImageTexture.CreateFromImage(image);
            _cosmeticTextures[fullPath] = texture;
            return true;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not load cosmetic image '{fullPath}': {exception.Message}");
            return false;
        }
    }

    private CosmeticSelection LoadCosmeticSelection()
    {
        var path = Path.Combine(_modDirectory, "presentation", "cosmetics.json");
        if (!File.Exists(path))
            return CosmeticSelection.Empty;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            string? shopkeeperId = null;
            var shopkeeperSkin = BaseSkinId;
            if (root.TryGetProperty("shopkeeper", out var shopkeeper) &&
                shopkeeper.ValueKind == JsonValueKind.Object)
            {
                if (shopkeeper.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                    shopkeeperId = idElement.GetString();
                if (shopkeeper.TryGetProperty("skin", out var skinElement) && skinElement.ValueKind == JsonValueKind.String)
                    shopkeeperSkin = skinElement.GetString() ?? BaseSkinId;
            }

            if (!IsSafeSegment(shopkeeperId))
                shopkeeperId = null;
            if (!IsSafeSegment(shopkeeperSkin))
                shopkeeperSkin = BaseSkinId;

            var leaderSkins = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("leaders", out var leaders) && leaders.ValueKind == JsonValueKind.Object)
            {
                foreach (var leader in leaders.EnumerateObject())
                {
                    if (leader.Value.ValueKind != JsonValueKind.String)
                        continue;
                    var skin = leader.Value.GetString();
                    if (IsSafeSegment(leader.Name) && IsSafeSegment(skin))
                        leaderSkins[leader.Name] = skin!;
                }
            }

            return new CosmeticSelection(shopkeeperId, shopkeeperSkin, leaderSkins);
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not load optional cosmetics selection '{path}': {exception.Message}");
            return CosmeticSelection.Empty;
        }
    }

    private static bool IsSafeSegment(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_');

    private sealed record CosmeticSelection(
        string? ShopkeeperId,
        string ShopkeeperSkin,
        IReadOnlyDictionary<string, string> LeaderSkins)
    {
        public static CosmeticSelection Empty { get; } = new(
            null,
            BaseSkinId,
            new Dictionary<string, string>(StringComparer.Ordinal));
    }
}