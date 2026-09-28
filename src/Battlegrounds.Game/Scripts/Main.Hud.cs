using Battlegrounds.Content;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private VBoxContainer _opponentEntries = null!;
    private TextureRect _hudHeroPortrait = null!;
    private Label _hudHeroName = null!;
    private Label _hudHealthValue = null!;
    private Label _hudArmorValue = null!;
    private Label _hudTierValue = null!;
    private Label _hudResourceValue = null!;
    private Label _hudResourcePips = null!;
    private PanelContainer _hudHealthBadge = null!;
    private PanelContainer _hudArmorBadge = null!;
    private PanelContainer _hudResourceBadge = null!;
    private bool _hudBound;
    private string? _hudSignature;

    private void RefreshSemanticHud()
    {
        if (!_hudBound)
        {
            BindHudNodes();
            _hudBound = true;
        }

        if (_session?.Match is not MatchState match ||
            !match.TryGetPlayer(_session.HumanPlayerId, out var human))
        {
            return;
        }

        var signature = BuildHudSignature(match, human);
        if (string.Equals(signature, _hudSignature, StringComparison.Ordinal))
            return;

        _hudSignature = signature;
        RenderSemanticHud(match, human);
    }

    private void BindHudNodes()
    {
        _opponentEntries = GetNode<VBoxContainer>("%OpponentEntries");
        _hudHeroName = GetNode<Label>("%HudHeroName");
        _hudHealthValue = GetNode<Label>("%HudHealthValue");
        _hudArmorValue = GetNode<Label>("%HudArmorValue");
        _hudTierValue = GetNode<Label>("%HudTierValue");
        _hudResourceValue = GetNode<Label>("%HudResourceValue");
        _hudHealthBadge = GetNode<PanelContainer>("Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroDockRow/HealthBadge");
        _hudArmorBadge = GetNode<PanelContainer>("%HudArmorBadge");
        _hudResourceBadge = GetNode<PanelContainer>("Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroDockRow/ResourceBadge");
        _hudResourcePips = EnsureResourcePips();
        _hudHeroPortrait = EnsureHeroPortraitSlot();
    }

    private TextureRect EnsureHeroPortraitSlot()
    {
        var heroDock = GetNode<HBoxContainer>("Margin/Shell/CenterStage/PreparationPanel/HeroDock/HeroDockRow");
        var dockHeight = ResolvePresentationMetric(ModThemeMetricKeys.Hud.HeroDockMinimumHeight, 1.0f, 2048.0f);
        var portraitSize = ResolvePresentationMetric(ModThemeMetricKeys.Hud.HeroPortraitSize, 1.0f, 2048.0f);
        var healthWidth = ResolvePresentationMetric(ModThemeMetricKeys.Layout.HeroDockHealthBadgeWidth, 1.0f, 512.0f);
        var healthHeight = ResolvePresentationMetric(ModThemeMetricKeys.Layout.HeroDockHealthBadgeHeight, 1.0f, 512.0f);
        var armorWidth = ResolvePresentationMetric(ModThemeMetricKeys.Layout.HeroDockArmorBadgeWidth, 1.0f, 512.0f);
        var armorHeight = ResolvePresentationMetric(ModThemeMetricKeys.Layout.HeroDockArmorBadgeHeight, 1.0f, 512.0f);
        heroDock.CustomMinimumSize = new Vector2(0, dockHeight);

        var cluster = heroDock.GetNodeOrNull<Control>("HeroPortraitCluster");
        if (cluster is null)
        {
            cluster = new Control
            {
                Name = "HeroPortraitCluster",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var insertAt = _hudHealthBadge.GetIndex();
            heroDock.AddChild(cluster);
            heroDock.MoveChild(cluster, insertAt);

            heroDock.RemoveChild(_hudHealthBadge);
            cluster.AddChild(_hudHealthBadge);
            heroDock.RemoveChild(_hudArmorBadge);
            cluster.AddChild(_hudArmorBadge);
        }

        var badgeAllowance = Mathf.Max(healthWidth, armorWidth) * 0.42f;
        var clusterWidth = portraitSize + badgeAllowance;
        var clusterHeight = portraitSize + Mathf.Max(healthHeight, armorHeight) * 0.28f;
        cluster.CustomMinimumSize = new Vector2(clusterWidth, clusterHeight);

        var frame = cluster.GetNodeOrNull<PanelContainer>("HeroPortraitFrame");
        TextureRect portrait;
        if (frame is null)
        {
            frame = new PanelContainer
            {
                Name = "HeroPortraitFrame",
                ThemeTypeVariation = "HeroPortraitFrame",
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ZIndex = 0,
            };
            portrait = new TextureRect
            {
                Name = "Portrait",
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            frame.AddChild(portrait);
            cluster.AddChild(frame);
            cluster.MoveChild(frame, 0);
        }
        else
        {
            portrait = frame.GetNode<TextureRect>("Portrait");
        }

        frame.AnchorLeft = 0.5f;
        frame.AnchorTop = 0.5f;
        frame.AnchorRight = 0.5f;
        frame.AnchorBottom = 0.5f;
        frame.OffsetLeft = -portraitSize * 0.5f;
        frame.OffsetTop = -portraitSize * 0.5f;
        frame.OffsetRight = portraitSize * 0.5f;
        frame.OffsetBottom = portraitSize * 0.5f;
        portrait.CustomMinimumSize = new Vector2(portraitSize, portraitSize);

        AnchorHudBadge(_hudHealthBadge, right: true, healthWidth, healthHeight);
        AnchorHudBadge(_hudArmorBadge, right: false, armorWidth, armorHeight);
        return portrait;
    }

    private Label EnsureResourcePips()
    {
        if (_hudResourceBadge.GetNodeOrNull<HBoxContainer>("ResourceContent") is { } existing &&
            existing.GetNodeOrNull<Label>("ResourcePips") is { } existingPips)
        {
            return existingPips;
        }

        _hudResourceBadge.RemoveChild(_hudResourceValue);
        var content = new HBoxContainer
        {
            Name = "ResourceContent",
            Alignment = BoxContainer.AlignmentMode.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        content.AddThemeConstantOverride("separation", 8);
        _hudResourceBadge.AddChild(content);

        _hudResourceValue.HorizontalAlignment = HorizontalAlignment.Center;
        _hudResourceValue.VerticalAlignment = VerticalAlignment.Center;
        _hudResourceValue.CustomMinimumSize = new Vector2(48, 0);
        content.AddChild(_hudResourceValue);

        var pips = new Label
        {
            Name = "ResourcePips",
            ThemeTypeVariation = "CaptionLabel",
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        if (ResolveThemeColor("resourceAccent", out var resourceColor))
            pips.AddThemeColorOverride("font_color", resourceColor);
        content.AddChild(pips);
        return pips;
    }

    private static void AnchorHudBadge(PanelContainer badge, bool right, float width, float height)
    {
        badge.MouseFilter = Control.MouseFilterEnum.Ignore;
        badge.ZIndex = 2;
        badge.CustomMinimumSize = new Vector2(width, height);
        badge.AnchorLeft = right ? 1.0f : 0.0f;
        badge.AnchorTop = 1.0f;
        badge.AnchorRight = right ? 1.0f : 0.0f;
        badge.AnchorBottom = 1.0f;
        badge.OffsetLeft = right ? -width : 0.0f;
        badge.OffsetTop = -height;
        badge.OffsetRight = right ? 0.0f : width;
        badge.OffsetBottom = 0.0f;
    }

    private void RenderSemanticHud(MatchState match, PlayerState human)
    {
        RenderOpponentRail(match);

        _hudHeroName.Text = human.Leader is null
            ? Text("ui.noLeader", ("leader", Term("leader")))
            : LeaderName(human.Leader.Definition.Id);
        _hudHealthValue.Text = human.Health.ToString();
        _hudTierValue.Text = human.Tier.ToString();

        var maximumResource = _session is null
            ? human.Resource
            : _session.Mod.PreparationRules.GetResourceForRound(Math.Max(1, match.Round));
        _hudResourceValue.Text = $"{human.Resource}/{maximumResource}";
        _hudResourcePips.Text = BuildResourcePips(human.Resource, maximumResource);

        var heroPortrait = ResolveLeaderPortrait(human);
        _hudHeroPortrait.Texture = heroPortrait;
        _hudHeroPortrait.Visible = heroPortrait is not null;

        var armor = human.Leader?.Armor ?? 0;
        _hudArmorValue.Text = armor.ToString();
        _hudArmorBadge.Visible = armor > 0;

        _hudHealthValue.TooltipText = $"{Term("health")}: {human.Health}";
        _hudArmorValue.TooltipText = $"{Term("armor")}: {armor}";
        _hudTierValue.TooltipText = $"{Term("tier")}: {human.Tier}";
        _hudResourceValue.TooltipText = $"{Term("resource")}: {human.Resource}/{maximumResource}";
        _hudResourcePips.TooltipText = _hudResourceValue.TooltipText;
        _hudHeroName.TooltipText = _hudHeroName.Text;
        _hudHeroPortrait.TooltipText = _hudHeroName.Text;
    }

    private static string BuildResourcePips(int current, int maximum)
    {
        if (maximum <= 0)
            return string.Empty;

        const int maximumVisiblePips = 12;
        var visibleMaximum = Math.Min(maximum, maximumVisiblePips);
        var visibleCurrent = Math.Clamp(current, 0, visibleMaximum);
        var pips = new string('●', visibleCurrent) + new string('○', visibleMaximum - visibleCurrent);
        return maximum > maximumVisiblePips ? pips + "…" : pips;
    }

    private void RenderOpponentRail(MatchState match)
    {
        if (_session is null)
            return;

        foreach (var child in _opponentEntries.GetChildren())
        {
            _opponentEntries.RemoveChild(child);
            child.QueueFree();
        }

        var entryHeight = ResolvePresentationMetric(ModThemeMetricKeys.Hud.OpponentEntryHeight, 1.0f, 2048.0f);
        var marginHorizontal = Mathf.RoundToInt(ResolvePresentationMetric(ModThemeMetricKeys.Hud.OpponentMarginHorizontal, 0.0f, 512.0f));
        var marginVertical = Mathf.RoundToInt(ResolvePresentationMetric(ModThemeMetricKeys.Hud.OpponentMarginVertical, 0.0f, 512.0f));
        var rowGap = Mathf.RoundToInt(ResolvePresentationMetric(ModThemeMetricKeys.Hud.OpponentGap, 0.0f, 512.0f));
        var rankWidth = ResolvePresentationMetric(ModThemeMetricKeys.Hud.OpponentRankWidth, 1.0f, 512.0f);
        var portraitSize = ResolvePresentationMetric(ModThemeMetricKeys.Hud.OpponentPortraitSize, 1.0f, 1024.0f);
        var statsWidth = ResolvePresentationMetric(ModThemeMetricKeys.Hud.OpponentStatsWidth, 1.0f, 1024.0f);
        var identityGap = Mathf.RoundToInt(ResolvePresentationMetric(ModThemeMetricKeys.Hud.OpponentIdentityGap, 0.0f, 256.0f));
        var statsGap = Mathf.RoundToInt(ResolvePresentationMetric(ModThemeMetricKeys.Hud.OpponentStatsGap, 0.0f, 256.0f));

        var ordered = match.Players
            .OrderBy(player => player.IsEliminated)
            .ThenByDescending(player => player.Health)
            .ThenBy(player => player.Id.Value)
            .ToArray();

        for (var rank = 0; rank < ordered.Length; rank++)
        {
            var player = ordered[rank];
            var isHuman = player.Id == _session.HumanPlayerId;
            var armor = player.Leader?.Armor ?? 0;
            var leaderName = player.Leader is null
                ? Text("ui.noLeader", ("leader", Term("leader")))
                : LeaderName(player.Leader.Definition.Id);
            var state = player.IsEliminated
                ? Text("ui.eliminated")
                : player.IsReadyForCombat
                    ? Text("ui.ready")
                    : Text("ui.active");

            var entry = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, entryHeight),
                ThemeTypeVariation = player.IsEliminated
                    ? "OpponentEntryEliminated"
                    : isHuman ? "OpponentEntrySelf" : "OpponentEntry",
                TooltipText = $"{leaderName} · {Term("health")} {player.Health} · {Term("armor")} {armor} · {Term("tier")} {player.Tier} · {state}",
            };

            var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            margin.AddThemeConstantOverride("margin_left", marginHorizontal);
            margin.AddThemeConstantOverride("margin_top", marginVertical);
            margin.AddThemeConstantOverride("margin_right", marginHorizontal);
            margin.AddThemeConstantOverride("margin_bottom", marginVertical);
            entry.AddChild(margin);

            var row = new HBoxContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            row.AddThemeConstantOverride("separation", rowGap);
            margin.AddChild(row);

            row.AddChild(new Label
            {
                Text = (rank + 1).ToString(),
                CustomMinimumSize = new Vector2(rankWidth, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ThemeTypeVariation = "CaptionLabel",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            var portraitFrame = new PanelContainer
            {
                CustomMinimumSize = new Vector2(portraitSize, portraitSize),
                ThemeTypeVariation = "OpponentPortraitFrame",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var portrait = new TextureRect
            {
                Texture = ResolveLeaderPortrait(player),
                CustomMinimumSize = new Vector2(portraitSize, portraitSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            portrait.Visible = portrait.Texture is not null;
            portraitFrame.AddChild(portrait);
            row.AddChild(portraitFrame);

            var identity = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            identity.AddThemeConstantOverride("separation", identityGap);
            row.AddChild(identity);

            identity.AddChild(new Label
            {
                Text = leaderName,
                ThemeTypeVariation = "BodyLabel",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            identity.AddChild(new Label
            {
                Text = state,
                ThemeTypeVariation = "CaptionLabel",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            var stats = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(statsWidth, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            stats.AddThemeConstantOverride("separation", statsGap);
            row.AddChild(stats);

            stats.AddChild(new Label
            {
                Text = player.Health.ToString(),
                HorizontalAlignment = HorizontalAlignment.Right,
                ThemeTypeVariation = "HealthValueLabel",
                TooltipText = $"{Term("health")}: {player.Health}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            stats.AddChild(new Label
            {
                Text = player.Tier.ToString(),
                HorizontalAlignment = HorizontalAlignment.Right,
                ThemeTypeVariation = "TierValueLabel",
                TooltipText = $"{Term("tier")}: {player.Tier}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            _opponentEntries.AddChild(entry);
        }
    }

    private Texture2D? ResolveLeaderPortrait(PlayerState player)
    {
        if (player.Leader is null)
            return null;

        PresentationTextures.TryGetImage(
            ModPresentationEntityKind.Leader,
            player.Leader.Definition.Id.Value,
            ModPresentationAssetSlots.Portrait,
            out var texture);
        return texture;
    }

    private static string BuildHudSignature(MatchState match, PlayerState human)
    {
        var players = string.Join(';', match.Players
            .OrderBy(player => player.Id.Value)
            .Select(player => $"{player.Id.Value}:{player.Health}:{player.Leader?.Armor ?? 0}:{player.Tier}:{player.IsReadyForCombat}:{player.IsEliminated}:{player.Leader?.Definition.Id.Value}"));
        return $"{match.Round}:{match.Phase}:{human.Resource}:{players}";
    }
}
