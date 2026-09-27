using Godot;

namespace Battlegrounds.Game;

/// <summary>
/// Keeps the presentation adapter's existing VBoxContainer contract while laying
/// dynamic card children out as a compact centered horizontal row. The container
/// owns only geometry; visual styling remains in the mod-driven theme layer.
///
/// Row geometry is inferred from its semantic scene name so shop, board, reserve,
/// and leader choices can have different silhouettes without coupling gameplay code
/// to presentation sizing.
///
/// The Field row additionally decorates its cards with small ordering controls.
/// Those controls submit ordinary Core commands through Main; this node never
/// mutates gameplay state or treats child order as authoritative.
/// </summary>
public partial class HorizontalCardRow : VBoxContainer
{
    [Export] public float Gap { get; set; } = 8.0f;
    [Export] public float PreferredCardWidth { get; set; } = 138.0f;
    [Export] public float MinimumCardWidth { get; set; } = 78.0f;
    [Export] public float PreferredCardHeight { get; set; } = 168.0f;
    [Export] public float Padding { get; set; } = 4.0f;

    private bool IsFieldRow => Name == "FieldButtons";

    public override void _Ready()
    {
        ApplySemanticGeometry();
        SetProcess(IsFieldRow);
        QueueSort();
    }

    public override void _Process(double delta)
    {
        if (!IsFieldRow) return;
        DecorateFieldCards();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren)
            return;

        var children = GetChildren()
            .OfType<Control>()
            .Where(child => child.Visible && !child.IsQueuedForDeletion())
            .ToArray();

        if (children.Length == 0)
            return;

        var innerWidth = Mathf.Max(0.0f, Size.X - Padding * 2.0f);
        var gapsWidth = Gap * Mathf.Max(0, children.Length - 1);
        var availableCardsWidth = Mathf.Max(0.0f, innerWidth - gapsWidth);
        var fittedWidth = availableCardsWidth / children.Length;
        var cardWidth = Mathf.Min(PreferredCardWidth, fittedWidth);

        if (availableCardsWidth >= MinimumCardWidth * children.Length)
            cardWidth = Mathf.Max(MinimumCardWidth, cardWidth);

        var availableHeight = Mathf.Max(0.0f, Size.Y - Padding * 2.0f);
        var cardHeight = Mathf.Min(PreferredCardHeight, availableHeight);
        var totalWidth = cardWidth * children.Length + gapsWidth;
        var x = Mathf.Max(Padding, (Size.X - totalWidth) * 0.5f);
        var y = Mathf.Max(Padding, (Size.Y - cardHeight) * 0.5f);

        foreach (var child in children)
        {
            FitChildInRect(child, new Rect2(x, y, cardWidth, cardHeight));
            x += cardWidth + Gap;
        }
    }

    private void ApplySemanticGeometry()
    {
        switch (Name.ToString())
        {
            case "LeaderButtons":
                Gap = 18.0f;
                PreferredCardWidth = 164.0f;
                MinimumCardWidth = 116.0f;
                PreferredCardHeight = 224.0f;
                Padding = 10.0f;
                break;
            case "OfferButtons":
                Gap = 10.0f;
                PreferredCardWidth = 124.0f;
                MinimumCardWidth = 88.0f;
                PreferredCardHeight = 180.0f;
                Padding = 6.0f;
                break;
            case "FieldButtons":
                Gap = 8.0f;
                PreferredCardWidth = 152.0f;
                MinimumCardWidth = 102.0f;
                PreferredCardHeight = 224.0f;
                Padding = 8.0f;
                break;
            case "ReserveButtons":
                Gap = 4.0f;
                PreferredCardWidth = 96.0f;
                MinimumCardWidth = 70.0f;
                PreferredCardHeight = 112.0f;
                Padding = 2.0f;
                break;
        }
    }

    private void DecorateFieldCards()
    {
        var main = FindMain();
        if (main is null) return;

        var cards = GetChildren()
            .OfType<PresentationCardButton>()
            .Where(card => !card.IsQueuedForDeletion())
            .ToArray();

        foreach (var card in cards)
        {
            var index = card.GetIndex();
            RemoveChild(card);

            var wrapper = new HBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
            };
            wrapper.AddThemeConstantOverride("separation", 2);

            var left = CreateOrderButton("←");
            var right = CreateOrderButton("→");
            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            card.SizeFlagsVertical = SizeFlags.ExpandFill;

            wrapper.AddChild(left);
            wrapper.AddChild(card);
            wrapper.AddChild(right);
            AddChild(wrapper);
            MoveChild(wrapper, index);

            left.Pressed += () => MoveFieldCard(main, wrapper, -1);
            right.Pressed += () => MoveFieldCard(main, wrapper, 1);
        }

        UpdateFieldOrderButtons(main);
    }

    private void UpdateFieldOrderButtons(Main main)
    {
        var wrappers = CurrentFieldWrappers();
        var canReorder = main.CanReorderHumanField && wrappers.Length > 1;
        for (var index = 0; index < wrappers.Length; index++)
        {
            var buttons = wrappers[index].GetChildren().OfType<Button>().ToArray();
            if (buttons.Length < 2) continue;

            var left = buttons[0];
            var right = buttons[^1];
            left.Visible = canReorder;
            right.Visible = canReorder;
            left.Disabled = !canReorder || index == 0;
            right.Disabled = !canReorder || index == wrappers.Length - 1;
        }
    }

    private void MoveFieldCard(Main main, HBoxContainer wrapper, int offset)
    {
        var wrappers = CurrentFieldWrappers();
        var index = Array.IndexOf(wrappers, wrapper);
        if (index < 0) return;
        main.ReorderHumanField(index, offset);
    }

    private HBoxContainer[] CurrentFieldWrappers() =>
        GetChildren()
            .OfType<HBoxContainer>()
            .Where(wrapper => !wrapper.IsQueuedForDeletion())
            .ToArray();

    private static Button CreateOrderButton(string text) => new()
    {
        Text = text,
        CustomMinimumSize = new Vector2(18, 0),
        SizeFlagsVertical = SizeFlags.ExpandFill,
        FocusMode = FocusModeEnum.None,
    };

    private Main? FindMain()
    {
        Node? node = GetParent();
        while (node is not null)
        {
            if (node is Main main) return main;
            node = node.GetParent();
        }
        return null;
    }
}
