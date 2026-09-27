using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private VBoxContainer _opponentEntries = null!;
    private Label _hudHeroName = null!;
    private Label _hudHealthValue = null!;
    private Label _hudArmorValue = null!;
    private Label _hudTierValue = null!;
    private Label _hudResourceValue = null!;
    private PanelContainer _hudArmorBadge = null!;

    private void BindHudNodes()
    {
        _opponentEntries = GetNode<VBoxContainer>("%OpponentEntries");
        _hudHeroName = GetNode<Label>("%HudHeroName");
        _hudHealthValue = GetNode<Label>("%HudHealthValue");
        _hudArmorValue = GetNode<Label>("%HudArmorValue");
        _hudTierValue = GetNode<Label>("%HudTierValue");
        _hudResourceValue = GetNode<Label>("%HudResourceValue");
        _hudArmorBadge = GetNode<PanelContainer>("%HudArmorBadge");
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

        var armor = human.Leader?.Armor ?? 0;
        _hudArmorValue.Text = armor.ToString();
        _hudArmorBadge.Visible = armor > 0;

        _hudHealthValue.TooltipText = $"{Term("health")}: {human.Health}";
        _hudArmorValue.TooltipText = $"{Term("armor")}: {armor}";
        _hudTierValue.TooltipText = $"{Term("tier")}: {human.Tier}";
        _hudResourceValue.TooltipText = $"{Term("resource")}: {human.Resource}";
        _hudHeroName.TooltipText = _hudHeroName.Text;
    }

    private void RenderOpponentRail(MatchState match)
    {
        if (_session is null)
            return;

        ClearChildren(_opponentEntries);

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
            margin.AddThemeConstantOverride("margin_left", 6);
            margin.AddThemeConstantOverride("margin_top", 4);
            margin.AddThemeConstantOverride("margin_right", 6);
            margin.AddThemeConstantOverride("margin_bottom", 4);
            entry.AddChild(margin);

            var row = new HBoxContainer
            {
                MouseFilter = Control.MouseFilterEnum.Ignore,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            row.AddThemeConstantOverride("separation", 6);
            margin.AddChild(row);

            var rankLabel = new Label
            {
                Text = (rank + 1).ToString(),
                CustomMinimumSize = new Vector2(20, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ThemeTypeVariation = "CaptionLabel",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            row.AddChild(rankLabel);

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
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });
            identity.AddChild(new Label
            {
                Text = state,
                ThemeTypeVariation = "CaptionLabel",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            });

            var stats = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(42, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            stats.AddThemeConstantOverride("separation", 0);
            row.AddChild(stats);

            var health = new Label
            {
                Text = player.Health.ToString(),
                HorizontalAlignment = HorizontalAlignment.Right,
                ThemeTypeVariation = "HealthValueLabel",
                TooltipText = $"{Term("health")}: {player.Health}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            stats.AddChild(health);

            var tier = new Label
            {
                Text = player.Tier.ToString(),
                HorizontalAlignment = HorizontalAlignment.Right,
                ThemeTypeVariation = "TierValueLabel",
                TooltipText = $"{Term("tier")}: {player.Tier}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            stats.AddChild(tier);

            _opponentEntries.AddChild(entry);
        }
    }
}
