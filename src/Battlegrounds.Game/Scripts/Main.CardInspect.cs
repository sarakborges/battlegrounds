using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private PanelContainer? _cardInspectPanel;
    private TextureRect? _cardInspectArt;
    private Label? _cardInspectTitle;
    private Label? _cardInspectSubtitle;
    private Label? _cardInspectStats;
    private Label? _cardInspectDescription;
    private Label? _cardInspectDetails;
    private PresentationCardButton? _cardInspectSource;

    internal void ShowCardInspect(PresentationCardButton source, PresentationCardInspectData data)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(data);
        EnsureCardInspectOverlay();

        _cardInspectSource = source;
        _cardInspectArt!.Texture = data.Texture;
        _cardInspectArt.Visible = data.Texture is not null;
        _cardInspectTitle!.Text = data.Title;
        SetInspectText(_cardInspectSubtitle!, data.Subtitle);
        SetInspectText(_cardInspectStats!, data.Stats);
        SetInspectText(_cardInspectDescription!, data.Description);
        SetInspectText(_cardInspectDetails!, data.Details);

        var width = ResolvePresentationMetric(ModThemeMetricKeys.Card.InspectWidth, 120.0f, 1024.0f);
        var artHeight = ResolvePresentationMetric(ModThemeMetricKeys.Card.InspectArtHeight, 0.0f, 1024.0f);
        _cardInspectPanel!.CustomMinimumSize = new Vector2(width, 0);
        _cardInspectArt.CustomMinimumSize = new Vector2(0, artHeight);
        _cardInspectPanel.Visible = true;
        Callable.From(() => PositionCardInspect(source)).CallDeferred();
    }

    internal void HideCardInspect(PresentationCardButton source)
    {
        if (_cardInspectPanel is null || _cardInspectSource != source)
            return;

        _cardInspectPanel.Visible = false;
        _cardInspectSource = null;
    }

    private void EnsureCardInspectOverlay()
    {
        if (_cardInspectPanel is not null)
            return;

        _cardInspectPanel = new PanelContainer
        {
            Name = "CardInspectOverlay",
            ThemeTypeVariation = "CardInspectSurface",
            ZIndex = 300,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_cardInspectPanel);

        var content = new VBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        content.AddThemeConstantOverride("separation", 6);
        _cardInspectPanel.AddChild(content);

        _cardInspectArt = new TextureRect
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        content.AddChild(_cardInspectArt);

        _cardInspectTitle = CreateInspectLabel("HeadingLabel", wrap: true);
        _cardInspectTitle.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(_cardInspectTitle);

        _cardInspectSubtitle = CreateInspectLabel("CaptionLabel", wrap: true);
        _cardInspectSubtitle.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(_cardInspectSubtitle);

        _cardInspectStats = CreateInspectLabel("BodyLabel", wrap: true);
        _cardInspectStats.HorizontalAlignment = HorizontalAlignment.Center;
        content.AddChild(_cardInspectStats);

        _cardInspectDescription = CreateInspectLabel("BodyLabel", wrap: true);
        content.AddChild(_cardInspectDescription);

        _cardInspectDetails = CreateInspectLabel("CaptionLabel", wrap: true);
        content.AddChild(_cardInspectDetails);
    }

    private void PositionCardInspect(Control source)
    {
        if (_cardInspectPanel is null || !_cardInspectPanel.Visible || _cardInspectSource != source)
            return;

        var offset = ResolvePresentationMetric(ModThemeMetricKeys.Card.InspectOffset, 0.0f, 256.0f);
        var rootRect = GetGlobalRect();
        var sourceRect = source.GetGlobalRect();
        var panelSize = _cardInspectPanel.GetCombinedMinimumSize();
        _cardInspectPanel.Size = panelSize;

        var sourceLocal = sourceRect.Position - rootRect.Position;
        var rightX = sourceLocal.X + sourceRect.Size.X + offset;
        var leftX = sourceLocal.X - panelSize.X - offset;
        var x = rightX + panelSize.X <= Size.X - offset ? rightX : leftX;
        var maxX = Mathf.Max(offset, Size.X - panelSize.X - offset);
        x = Mathf.Clamp(x, offset, maxX);

        var y = sourceLocal.Y + sourceRect.Size.Y * 0.5f - panelSize.Y * 0.5f;
        var maxY = Mathf.Max(offset, Size.Y - panelSize.Y - offset);
        y = Mathf.Clamp(y, offset, maxY);

        _cardInspectPanel.Position = new Vector2(x, y);
    }

    private static Label CreateInspectLabel(string variation, bool wrap)
    {
        var label = new Label
        {
            ThemeTypeVariation = variation,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (wrap)
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private static void SetInspectText(Label label, string? value)
    {
        label.Text = value ?? string.Empty;
        label.Visible = !string.IsNullOrWhiteSpace(value);
    }
}
