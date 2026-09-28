using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private bool _preparationPresentationPolishBound;

    private void RefreshPreparationPresentationPolish()
    {
        if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation)
            return;

        if (!_preparationPresentationPolishBound)
        {
            BindPreparationPresentationPolish();
            _preparationPresentationPolishBound = true;
        }

        // A short readiness label keeps the turn control from forcing a giant
        // right-hand rail. The command is still EndPreparationCommand; this is
        // presentation only.
        _endPreparationButton.Text = Text("ui.ready");

        if (!_hudBound)
            return;

        // Until the mod provides coin art, the numeric current/max resource is
        // substantially cleaner than the unicode dot row (which several fonts
        // render as an ellipsis-like cluster).
        _hudResourcePips.Visible = false;
        _hudResourcePips.Text = string.Empty;

        // The leader name is already available via the portrait tooltip and the
        // opponent rail. Keeping another nameplate inside the compact hero
        // portrait caused the fallback monogram, name, health, and armor to
        // overlap. The bottom HUD now reads as portrait -> power -> resource.
        _hudHeroName.Visible = false;
        if (_hudHeroPortrait.GetParent() is PanelContainer portraitFrame &&
            portraitFrame.GetNodeOrNull<Label>("PortraitNameplate") is { } nameplate)
        {
            nameplate.Visible = false;
        }
    }

    private void BindPreparationPresentationPolish()
    {
        const string preparationPath = "Margin/Shell/CenterStage/PreparationPanel";
        var preparation = GetNode<VBoxContainer>(preparationPath);
        var board = GetNode<PanelContainer>($"{preparationPath}/BoardStage");
        var reserve = GetNode<PanelContainer>($"{preparationPath}/ReserveShelf");
        var heroDock = GetNode<PanelContainer>($"{preparationPath}/HeroDock");

        // Board/hand should occupy their semantic rows, not consume every spare
        // vertical pixel. A transparent table-space control absorbs the remaining
        // height and keeps the hero dock anchored toward the bottom of the table.
        board.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        reserve.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
        heroDock.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;

        var tableSpace = preparation.GetNodeOrNull<Control>("TableSpace");
        if (tableSpace is null)
        {
            tableSpace = new Control
            {
                Name = "TableSpace",
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            preparation.AddChild(tableSpace);
            preparation.MoveChild(tableSpace, heroDock.GetIndex());
        }

        var turnRail = GetNode<PanelContainer>("Margin/Shell/TurnRail");
        turnRail.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        _endPreparationButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
    }
}
