using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private PanelContainer? _combatTopHeroPortrait;
    private PanelContainer? _combatBottomHeroPortrait;

    private void RefreshCombatReferencePolish()
    {
        if (_combatOverlay?.Visible != true || _combatPlayback is null)
            return;

        if (ResolveThemeColor("background", out var background))
        {
            _combatOverlay.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = background,
                BorderWidthLeft = 0,
                BorderWidthTop = 0,
                BorderWidthRight = 0,
                BorderWidthBottom = 0,
            });
        }

        if (_combatTitle is not null)
            _combatTitle.Visible = false;
        if (_combatProgress is not null)
            _combatProgress.Visible = false;

        if (_combatEvent is not null)
        {
            _combatEvent.Visible = _combatPlayback.CurrentEvent is not null;
            _combatEvent.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        }

        HideCombatEmptyLabels(_combatLeftUnits);
        HideCombatEmptyLabels(_combatRightUnits);

        var topBoard = _combatOverlay.FindChild("CombatTopBoard", recursive: true, owned: false) as VBoxContainer;
        var bottomBoard = _combatOverlay.FindChild("CombatBottomBoard", recursive: true, owned: false) as VBoxContainer;
        if (topBoard is not null)
            _combatTopHeroPortrait = EnsureCombatHeroPortrait(
                topBoard,
                _combatTopHeroPortrait,
                "CombatTopHeroPortrait",
                _combatPlayback.RightPlayerId,
                placeFirst: true);
        if (bottomBoard is not null)
            _combatBottomHeroPortrait = EnsureCombatHeroPortrait(
                bottomBoard,
                _combatBottomHeroPortrait,
                "CombatBottomHeroPortrait",
                _combatPlayback.LeftPlayerId,
                placeFirst: false);

        ApplyCombatHeader(_combatRightHeader, _combatPlayback.RightPlayerId);
        ApplyCombatHeader(_combatLeftHeader, _combatPlayback.LeftPlayerId);

        if (_combatOverlay.FindChild("CombatControls", recursive: true, owned: false) is HBoxContainer controls)
        {
            controls.Alignment = BoxContainer.AlignmentMode.Center;
            controls.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        }

        if (_combatNextButton is not null)
        {
            _combatNextButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            _combatNextButton.CustomMinimumSize = new Vector2(150.0f, 46.0f);
        }

        if (_combatSkipButton is not null)
        {
            _combatSkipButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            _combatSkipButton.CustomMinimumSize = new Vector2(150.0f, 46.0f);
        }
    }

    private static void HideCombatEmptyLabels(HBoxContainer? units)
    {
        if (units is null)
            return;

        foreach (var label in units.GetChildren().OfType<Label>())
            label.Visible = false;
    }

    private PanelContainer EnsureCombatHeroPortrait(
        VBoxContainer board,
        PanelContainer? existing,
        string nodeName,
        PlayerId playerId,
        bool placeFirst)
    {
        var portraitSize = ResolvePresentationMetric(
            Battlegrounds.Content.ModThemeMetricKeys.Hud.HeroPortraitSize,
            1.0f,
            512.0f) * 0.82f;

        var host = existing;
        if (host is null || !GodotObject.IsInstanceValid(host) || host.GetParent() != board)
        {
            host = board.GetNodeOrNull<PanelContainer>(nodeName);
            if (host is null)
            {
                host = new PanelContainer
                {
                    Name = nodeName,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                };
                board.AddChild(host);
            }
        }

        FramedCosmeticPortrait.ConfigureSquare(host, portraitSize);

        var art = host.GetNodeOrNull<TextureRect>("Portrait");
        if (art is null)
        {
            art = new TextureRect { Name = "Portrait" };
            host.AddChild(art);
        }
        FramedCosmeticPortrait.ConfigureArt(art, TextureRect.StretchModeEnum.KeepAspectCovered);
        art.Texture = ResolveCombatLeaderPortrait(playerId);
        art.Visible = art.Texture is not null;

        var frameTexture = ResolveSharedPortraitFrameTexture();
        if (frameTexture is not null)
            FramedCosmeticPortrait.ApplyFrame(host, frameTexture, "LeaderFrameOverlay");

        if (placeFirst)
            board.MoveChild(host, 0);
        else
        {
            var header = _combatLeftHeader;
            var targetIndex = header?.GetParent() == board ? header.GetIndex() : board.GetChildCount();
            board.MoveChild(host, targetIndex);
        }

        return host;
    }

    private Texture2D? ResolveCombatLeaderPortrait(PlayerId playerId)
    {
        if (_session?.Match is not MatchState match || !match.TryGetPlayer(playerId, out var player))
            return null;
        return ResolveLeaderPortrait(player);
    }

    private void ApplyCombatHeader(Label? header, PlayerId playerId)
    {
        if (header is null)
            return;

        header.Text = ResolveCombatLeaderName(playerId);
        header.ThemeTypeVariation = "CaptionLabel";
        header.Visible = !string.IsNullOrWhiteSpace(header.Text);
        header.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
    }

    private string ResolveCombatLeaderName(PlayerId playerId)
    {
        if (_session?.Match is not MatchState match || !match.TryGetPlayer(playerId, out var player) || player.Leader is null)
            return string.Empty;
        return LeaderName(player.Leader.Definition.Id);
    }
}
