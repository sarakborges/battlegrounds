using Godot;

namespace Battlegrounds.Game;

internal sealed partial class FieldInsertionDropZone : Control
{
    private int _insertionIndex;
    private bool _enabled;
    private Action<int, int>? _dropHandler;

    public FieldInsertionDropZone()
    {
        ThemeTypeVariation = "FieldInsertionZone";
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ZIndex = 10;
    }

    internal void Configure(int insertionIndex, bool enabled, Action<int, int> dropHandler)
    {
        _insertionIndex = insertionIndex;
        _enabled = enabled;
        _dropHandler = dropHandler;
        MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!_enabled || _dropHandler is null || !PreparationDragPayload.TryReadField(data, out var sourceIndex))
            return false;

        var resultingIndex = _insertionIndex > sourceIndex ? _insertionIndex - 1 : _insertionIndex;
        return resultingIndex != sourceIndex;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_enabled || _dropHandler is null || !PreparationDragPayload.TryReadField(data, out var sourceIndex))
            return;

        _dropHandler(sourceIndex, _insertionIndex);
    }
}
