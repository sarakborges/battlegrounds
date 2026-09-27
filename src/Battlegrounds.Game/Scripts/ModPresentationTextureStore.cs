using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

internal sealed class ModPresentationTextureStore
{
    private readonly string _modDirectory;
    private readonly ModPresentationAssetCatalog _assets;
    private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);

    public ModPresentationTextureStore(string modDirectory)
        : this(modDirectory, new ModPresentationAssetLoader().Load(modDirectory))
    {
    }

    public ModPresentationTextureStore(string modDirectory, ModPresentationAssetCatalog assets)
    {
        if (string.IsNullOrWhiteSpace(modDirectory)) throw new ArgumentException("Mod directory cannot be empty.", nameof(modDirectory));
        _modDirectory = Path.GetFullPath(modDirectory);
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    public bool TryGetImage(
        ModPresentationEntityKind entityKind,
        string entityId,
        string slot,
        out Texture2D? texture)
    {
        texture = null;
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
}
