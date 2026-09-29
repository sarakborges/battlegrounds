using Battlegrounds.Content;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private PanelContainer? _cardInspectPanel;
    private Control? _cardInspectCanvas;
    private TextureRect? _cardInspectArt;
    private Label? _cardInspectTitle;
    private Label? _cardInspectSubtitle;
    private Label? _cardInspectStats;
    private Label? _cardInspectDescription;
    private Label? _cardInspectDetails;
    private PanelContainer? _cardInspectTierBadge;
    private Label? _cardInspectTierValue;
    private PanelContainer? _cardInspectAttackBadge;
    private Label? _cardInspectAttackValue;
    private PanelContainer? _cardInspectHealthBadge;
    private Label? _cardInspectHealthValue;
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
        SetInspectText(_cardInspectDescription!, data.Description);
        SetInspectText(_cardInspectDetails!, data.Details);

        var hasTokenStats = data.Attack.HasValue && data.Health.HasValue;
        SetInspectText(_cardInspectStats!, hasTokenStats ? null : data.Stats);
        ConfigureInspectBadge(_cardInspectTierBadge!, _cardInspectTierValue!, data.Tier);
        ConfigureInspectBadge(_cardInspectAttackBadge!, _cardInspectAttackValue!, data.Attack);
        ConfigureInspectBadge(_cardInspectHealthBadge!, _cardInspectHealthValue!, data.Health);

        var themedWidth = ResolvePresentationMetric(ModThemeMetricKeys.Card.InspectWidth, 120.0f, 1024.0f);
        var themedMinimumHeight = ResolvePresentationMetric(ModThemeMetricKeys.Card.InspectMinimumHeight, 120.0f, 1400.0f);
        var viewportWidth = Mathf.Max(1.0f, Size.X);
        var viewportHeight = Mathf.Max(1.0f, Size.Y);
        var width = Mathf.Min(themedWidth, Mathf.Clamp(viewportWidth * 0.16f, 220.0f, 260.0f));
        var minimumHeight = Mathf.Min(themedMinimumHeight, Mathf.Clamp(width * 1.48f, 320.0f, 400.0f));
        var badgeSize = Mathf.Min(
            ResolvePresentationMetric(ModThemeMetricKeys.Card.InspectBadgeSize, 16.0f, 160.0f),
            width * 0.15f);
        var badgeInset = Mathf.Min(
            ResolvePresentationMetric(ModThemeMetricKeys.Card.InspectBadgeInset, 0.0f, 96.0f),
            width * 0.035f);

        _cardInspectPanel!.CustomMinimumSize = new Vector2(width, minimumHeight);
        _cardInspectCanvas!.CustomMinimumSize = new Vector2(width, minimumHeight);

        LayoutInspectCardSlots(hasTokenStats);
        LayoutInspectBadge(_cardInspectTierBadge!, 0.0f, 0.0f, badgeInset, badgeInset, badgeSize);
        LayoutInspectBadge(_cardInspectAttackBadge!, 0.0f, 1.0f, badgeInset, -badgeInset - badgeSize, badgeSize);
        LayoutInspectBadge(_cardInspectHealthBadge!, 1.0f, 1.0f, -badgeInset - badgeSize, -badgeInset - badgeSize, badgeSize);

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
            ClipContents = true,
        };
        AddChild(_cardInspectPanel);

        _cardInspectCanvas = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _cardInspectPanel.AddChild(_cardInspectCanvas);
        _cardInspectCanvas.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _cardInspectArt = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _cardInspectCanvas.AddChild(_cardInspectArt);

        _cardInspectTitle = CreateInspectLabel("HeadingLabel", wrap: true);
        _cardInspectTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _cardInspectTitle.VerticalAlignment = VerticalAlignment.Center;
        _cardInspectCanvas.AddChild(_cardInspectTitle);

        _cardInspectSubtitle = CreateInspectLabel("CaptionLabel", wrap: true);
        _cardInspectSubtitle.HorizontalAlignment = HorizontalAlignment.Center;
        _cardInspectSubtitle.VerticalAlignment = VerticalAlignment.Center;
        _cardInspectCanvas.AddChild(_cardInspectSubtitle);

        _cardInspectStats = CreateInspectLabel("BodyLabel", wrap: true);
        _cardInspectStats.HorizontalAlignment = HorizontalAlignment.Center;
        _cardInspectStats.VerticalAlignment = VerticalAlignment.Center;
        _cardInspectCanvas.AddChild(_cardInspectStats);

        _cardInspectDescription = CreateInspectLabel("BodyLabel", wrap: true);
        _cardInspectDescription.HorizontalAlignment = HorizontalAlignment.Center;
        _cardInspectDescription.VerticalAlignment = VerticalAlignment.Center;
        _cardInspectCanvas.AddChild(_cardInspectDescription);

        _cardInspectDetails = CreateInspectLabel("CaptionLabel", wrap: true);
        _cardInspectDetails.HorizontalAlignment = HorizontalAlignment.Center;
        _cardInspectDetails.VerticalAlignment = VerticalAlignment.Center;
        _cardInspectCanvas.AddChild(_cardInspectDetails);

        (_cardInspectTierBadge, _cardInspectTierValue) = CreateInspectBadge("TierBadge", "TierValueLabel");
        (_cardInspectAttackBadge, _cardInspectAttackValue) = CreateInspectBadge("AttackBadge", "AttackValueLabel");
        (_cardInspectHealthBadge, _cardInspectHealthValue) = CreateInspectBadge("HealthBadge", "HealthValueLabel");
        _cardInspectCanvas.AddChild(_cardInspectTierBadge);
        _cardInspectCanvas.AddChild(_cardInspectAttackBadge);
        _cardInspectCanvas.AddChild(_cardInspectHealthBadge);
    }

    private void LayoutInspectCardSlots(bool hasTokenStats)
    {
        LayoutInspectSlot(_cardInspectArt!, 0.105f, 0.055f, 0.895f, 0.485f);
        LayoutInspectSlot(_cardInspectTitle!, 0.105f, 0.425f, 0.895f, 0.545f);
        LayoutInspectSlot(_cardInspectSubtitle!, 0.15f, 0.535f, 0.85f, 0.595f);

        if (hasTokenStats)
        {
            LayoutInspectSlot(_cardInspectDescription!, 0.13f, 0.59f, 0.87f, 0.775f);
            LayoutInspectSlot(_cardInspectDetails!, 0.13f, 0.775f, 0.87f, 0.895f);
            LayoutInspectSlot(_cardInspectStats!, 0.2f, 0.60f, 0.8f, 0.67f);
        }
        else
        {
            LayoutInspectSlot(_cardInspectStats!, 0.16f, 0.585f, 0.84f, 0.65f);
            LayoutInspectSlot(_cardInspectDescription!, 0.13f, 0.65f, 0.87f, 0.80f);
            LayoutInspectSlot(_cardInspectDetails!, 0.13f, 0.80f, 0.87f, 0.90f);
        }
    }

    private static void LayoutInspectSlot(Control control, float left, float top, float right, float bottom)
    {
        control.AnchorLeft = left;
        control.AnchorTop = top;
        control.AnchorRight = right;
        control.AnchorBottom = bottom;
        control.OffsetLeft = 0;
        control.OffsetTop = 0;
        control.OffsetRight = 0;
        control.OffsetBottom = 0;
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

        var sourceCenterY = sourceLocal.Y + sourceRect.Size.Y * 0.5f;
        float y;
        if (sourceCenterY < Size.Y * 0.46f)
        {
            // Tavern cards inspect below the shop so the card never covers Bob or
            // the upgrade/refresh/freeze controls.
            y = sourceLocal.Y + sourceRect.Size.Y + offset;
        }
        else
        {
            // Board/hand cards inspect above their source, which keeps the bottom
            // hero cockpit and the hand itself readable.
            y = sourceLocal.Y - panelSize.Y - offset;
        }

        var maxY = Mathf.Max(offset, Size.Y - panelSize.Y - offset);
        y = Mathf.Clamp(y, offset, maxY);

        _cardInspectPanel.Position = new Vector2(x, y);
    }

    private static void ConfigureInspectBadge(PanelContainer badge, Label label, int? value)
    {
        badge.Visible = value.HasValue;
        label.Text = value?.ToString() ?? string.Empty;
    }

    private static void LayoutInspectBadge(
        Control badge,
        float anchorX,
        float anchorY,
        float left,
        float top,
        float size)
    {
        badge.AnchorLeft = anchorX;
        badge.AnchorTop = anchorY;
        badge.AnchorRight = anchorX;
        badge.AnchorBottom = anchorY;
        badge.OffsetLeft = left;
        badge.OffsetTop = top;
        badge.OffsetRight = left + size;
        badge.OffsetBottom = top + size;
        badge.CustomMinimumSize = new Vector2(size, size);
    }

    private static (PanelContainer Panel, Label Label) CreateInspectBadge(string panelVariation, string labelVariation)
    {
        var panel = new PanelContainer
        {
            ThemeTypeVariation = panelVariation,
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 4,
        };
        var label = CreateInspectLabel(labelVariation, wrap: false);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        panel.AddChild(label);
        return (panel, label);
    }

    private static Label CreateInspectLabel(string variation, bool wrap)
    {
        var label = new Label
        {
            ThemeTypeVariation = variation,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipText = true,
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
