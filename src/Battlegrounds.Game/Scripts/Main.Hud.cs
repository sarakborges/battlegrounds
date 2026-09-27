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
    private PanelContainer _hudArmorBadge = null!;
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
        _hudArmorBadge = GetNode<PanelContainer>("%HudArmorBadge");
        _hudHeroPortrait = EnsureHeroPortraitSlot();
    }

    private TextureRect EnsureHeroPortraitSlot()
    {
        var heroDock = GetNode<HBoxContainer>("Margin/Shell/CenterStage/PreparationPanel/HeroDock");
        heroDock.CustomMinimumSize = new Vector2(0, 108);

        var existing = heroDock.GetNodeOrNull<PanelContainer>("HeroPortraitFrame");
        if (existing is not null && existing.GetNodeOrNull<TextureRect>("Portrait") is TextureRect existingPortrait)
            return existingPortrait;

        var frame = new PanelContainer
        {
            Name = "HeroPortraitFrame",
            CustomMinimumSize = new Vector2(88, 88),
            ThemeTypeVariation = "HeroPortraitFrame",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var portrait = new TextureRect
        {
            Name = "Portrait",
            CustomMinimumSize = new Vector2(88, 88),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        frame.AddChild(portrait);

        var heroCore = heroDock.GetNode<Control>("HeroCore");
        var insertAt = heroCore.GetIndex();
        heroDock.AddChild(frame);
        heroDock.MoveChild(frame, insertAt);
        return portrait;
    }

    private void RenderSemanticHud(MatchState match, PlayerState human)
    {
        RenderOpponentRail(match);

        _hudHeroName.Text = human.Leader is null
            ? Text("ui.noLeader", ("leader", Term("leader")))
            : LeaderName(human.Leader.Definition.Id);
        _hudHealthValue.Text = human.Health.ToString();
        _hudTierValue.Text = human.Tier.ToString();
        _hudResourceValue.Text = human.Resource.ToString();

        var heroPortrait = ResolveLeaderPortrait(human);
        _hudHeroPortrait.Texture = heroPortrait;
        _hudHeroPortrait.Visible = heroPortrait is not null;

        var armor = human.Leader?.Armor ?? 0;
        _hudArmorValue.Text = armor.ToString();
        _hudArmorBadge.Visible = armor > 0;

        _hudHealthValue.TooltipText = $"{Term("health")}: {human.Health}";
        _hudArmorValue.TooltipText = $"{Term("armor")}: {armor}";
        _hudTierValue.TooltipText = $"{Term("tier")}: {human.Tier}";
        _hudResourceValue.TooltipText = $"{Term("resource")}: {human.Resource}";
        _hudHeroName.TooltipText = _hudHeroName.Text;
        _hudHeroPortrait.TooltipText = _hudHeroName.Text;
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
                CustomMinimumSize = new Vector2(0, 58),
                ThemeTypeVariation = player.IsEliminated
                    ? "OpponentEntryEliminated"
                    : isHuman ? "OpponentEntrySelf" : "OpponentEntry",
                TooltipText = $"{leaderName} · {Term("health")} {player.Health} · {Term("armor")} {armor} · {Term("tier")} {player.Tier} · {state}",
            };

            var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            margin.AddThemeConstantOverride("margin_left", 5);
            margin.AddThemeConstantOverride("margin_top", 4);
            margin.AddThemeConstantOverride("margin_right", 5);
            margin.AddThemeConstantOverride("margin_bottom", 4);
            entry.AddChild(margin);

            var row = new HBoxContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            row.AddThemeConstantOverride("separation", 5);
            margin.AddChild(row);

            row.AddChild(new Label
            {
                Text = (rank + 1).ToString(),
                CustomMinimumSize = new Vector2(18, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ThemeTypeVariation = "CaptionLabel",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            var portraitFrame = new PanelContainer
            {
                CustomMinimumSize = new Vector2(44, 44),
                ThemeTypeVariation = "OpponentPortraitFrame",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            var portrait = new TextureRect
            {
                Texture = ResolveLeaderPortrait(player),
                CustomMinimumSize = new Vector2(44, 44),
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
            identity.AddThemeConstantOverride("separation", 0);
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
                CustomMinimumSize = new Vector2(34, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            stats.AddThemeConstantOverride("separation", 0);
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
