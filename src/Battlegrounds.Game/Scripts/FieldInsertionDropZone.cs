using Godot;

namespace Battlegrounds.Game;

internal sealed partial class FieldInsertionDropZone : Control
{
    private int _insertionIndex;
    private bool _enabled;
    private bool _hot;
    private Action<int, int>? _dropHandler;

    public int InsertionIndex => _insertionIndex;

    public FieldInsertionDropZone()
    {
        ThemeTypeVariation = "FieldInsertionZone";
        MouseFilter = MouseFilterEnum.Ignore;
        FocusMode = FocusModeEnum.None;
        ZIndex = 20;
    }

    internal void Configure(int insertionIndex, bool enabled, Action<int, int> dropHandler)
    {
        _insertionIndex = insertionIndex;
        _enabled = enabled;
        _dropHandler = dropHandler;
        MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        if (!enabled)
            ResetFeedback();
    }

    internal void ResetFeedback()
    {
        if (!_hot)
            return;

        _hot = false;
        ThemeTypeVariation = "FieldInsertionZone";
        Scale = Vector2.One;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        if (!_enabled || _dropHandler is null || !PreparationDragPayload.TryReadField(data, out var sourceIndex))
        {
            ResetFeedback();
            return false;
        }

        if (GetParent() is HorizontalCardRow parentRow)
            parentRow.PreviewFieldInsertion(sourceIndex, _insertionIndex);

        var resultingIndex = _insertionIndex > sourceIndex ? _insertionIndex - 1 : _insertionIndex;
        var valid = resultingIndex != sourceIndex;
        SetHot(valid);
        return valid;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (!_enabled || _dropHandler is null || !PreparationDragPayload.TryReadField(data, out var sourceIndex))
            return;

        ResetFeedback();
        _dropHandler(sourceIndex, _insertionIndex);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExitSelf || what == NotificationDragEnd)
            ResetFeedback();
    }

    private void SetHot(bool hot)
    {
        if (_hot == hot)
            return;

        _hot = hot;
        ThemeTypeVariation = hot ? "FieldInsertionZoneHot" : "FieldInsertionZone";
        Scale = hot ? new Vector2(1.08f, 1.02f) : Vector2.One;
    }
}
