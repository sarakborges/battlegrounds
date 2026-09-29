using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private void RefreshBoardReferenceFinalPass()
    {
        if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation || !_hudBound)
            return;

        var width = Size.X;
        var height = Size.Y;
        if (width <= 1.0f || height <= 1.0f)
            return;

        // The board cosmetic is the canvas. Legacy shell rails must never mask its
        // left/right edges; only the individual opponent entries remain visible.
        if (_boardReferenceOpponentRail is not null)
        {
            MakePanelTransparent(_boardReferenceOpponentRail);
            _boardReferenceOpponentRail.ClipContents = false;

            var railWidth = Mathf.Clamp(width * 0.088f, 132.0f, 154.0f);
            var entryHeight = Mathf.Clamp(height * 0.073f, 56.0f, 66.0f);
            var liveEntries = Math.Max(1, _opponentEntries.GetChildren().OfType<PanelContainer>().Count());
            var railHeight = liveEntries * entryHeight + 10.0f;

            ResetFreeControl(_boardReferenceOpponentRail);
            _boardReferenceOpponentRail.Position = new Vector2(4.0f, 4.0f);
            _boardReferenceOpponentRail.Size = new Vector2(railWidth, railHeight);
            _boardReferenceOpponentRail.CustomMinimumSize = Vector2.Zero;
            _boardReferenceOpponentRail.ZIndex = 35;

            if (_boardReferenceOpponentRail.GetNodeOrNull<Control>("Content/OpponentSpacer") is { } spacer)
            {
                spacer.Visible = false;
                spacer.CustomMinimumSize = Vector2.Zero;
            }

            if (_boardReferenceOpponentRail.GetNodeOrNull<Label>("Content/MatchSummary") is { } summary)
                summary.Visible = false;
        }

        // The old right rail was sized as a full-height layout column. READY is now
        // an overlay on the plaque painted into the board cosmetic, so the rail itself
        // must remain collapsed every frame.
        if (_boardReferenceTurnRail is not null)
        {
            _boardReferenceTurnRail.Visible = false;
            _boardReferenceTurnRail.CustomMinimumSize = Vector2.Zero;
            _boardReferenceTurnRail.Size = Vector2.Zero;
        }

        // These legacy panels no longer own their interactive children after the
        // reference overlay binds. Keeping them visible can still paint over the board.
        if (_boardReferenceTavernShelf is not null)
            _boardReferenceTavernShelf.Visible = false;
        if (_boardReferenceBoardStage is not null)
            _boardReferenceBoardStage.Visible = false;

        var reserveShelf = GetNodeOrNull<PanelContainer>("Margin/Shell/CenterStage/PreparationPanel/ReserveShelf");
        if (reserveShelf is not null)
            reserveShelf.Visible = false;

        var heroDock = GetNodeOrNull<PanelContainer>("Margin/Shell/CenterStage/PreparationPanel/HeroDock");
        if (heroDock is not null)
            heroDock.Visible = false;

        // Shop offers sit on the upper half of the physical board, with enough width
        // for the full seven-slot Tavern without letting low counts inflate in size.
        if (_boardReferenceShopRow is not null)
        {
            var shopWidth = width * 0.54f;
            var shopHeight = Mathf.Clamp(height * 0.195f, 138.0f, 168.0f);
            ResetFreeControl(_boardReferenceShopRow);
            _boardReferenceShopRow.Position = new Vector2((width - shopWidth) * 0.5f, height * 0.225f);
            _boardReferenceShopRow.Size = new Vector2(shopWidth, shopHeight);
            _boardReferenceShopRow.CustomMinimumSize = Vector2.Zero;
            _boardReferenceShopRow.ZIndex = 28;

            var offerWidth = Mathf.Clamp(width * 0.058f, 88.0f, 104.0f);
            var offerHeight = Mathf.Clamp(height * 0.158f, 118.0f, 140.0f);
            ConfigureReferenceRow(_offerButtons, offerWidth, offerHeight, 12.0f, 2.0f);
            if (_tavernActionOffers is not null)
                ConfigureReferenceRow(_tavernActionOffers, offerWidth, offerHeight, 12.0f, 2.0f);
        }

        // A dedicated seven-unit band. It deliberately leaves the hero cockpit below.
        var fieldWidth = width * 0.58f;
        var fieldHeight = Mathf.Clamp(height * 0.17f, 118.0f, 146.0f);
        ResetFreeControl(_fieldButtons);
        _fieldButtons.Position = new Vector2((width - fieldWidth) * 0.5f, height * 0.50f);
        _fieldButtons.Size = new Vector2(fieldWidth, fieldHeight);
        _fieldButtons.CustomMinimumSize = Vector2.Zero;
        _fieldButtons.ClipContents = false;
        _fieldButtons.ZIndex = 30;
        ConfigureReferenceRow(
            _fieldButtons,
            Mathf.Clamp(width * 0.055f, 82.0f, 98.0f),
            Mathf.Clamp(height * 0.142f, 104.0f, 124.0f),
            10.0f,
            2.0f);

        // Hand cards originate at the actual bottom edge. They sit behind the hero,
        // exactly like Battlegrounds, instead of becoming a card-shaped backdrop to it.
        ResetFreeControl(_reserveButtons);
        var handTop = height * 0.885f;
        _reserveButtons.Position = new Vector2(width * 0.30f, handTop);
        _reserveButtons.Size = new Vector2(width * 0.40f, height - handTop);
        _reserveButtons.CustomMinimumSize = Vector2.Zero;
        _reserveButtons.ClipContents = false;
        _reserveButtons.PreferredCardWidth = Mathf.Clamp(width * 0.047f, 70.0f, 84.0f);
        _reserveButtons.MinimumCardWidth = Mathf.Clamp(width * 0.040f, 62.0f, 72.0f);
        _reserveButtons.PreferredCardHeight = Mathf.Clamp(height * 0.125f, 92.0f, 112.0f);
        _reserveButtons.Gap = 6.0f;
        _reserveButtons.Padding = 0.0f;
        _reserveButtons.ZIndex = 41;
        _reserveButtons.QueueSort();

        if (_referenceHeroCluster is not null)
        {
            var heroSize = Mathf.Clamp(height * 0.118f, 88.0f, 106.0f);
            var heroWidth = heroSize + 30.0f;
            var heroHeight = heroSize + 16.0f;
            var heroTop = height * 0.735f;

            ResetFreeControl(_referenceHeroCluster);
            _referenceHeroCluster.Position = new Vector2(width * 0.5f - heroWidth * 0.5f, heroTop);
            _referenceHeroCluster.Size = new Vector2(heroWidth, heroHeight);
            _referenceHeroCluster.CustomMinimumSize = _referenceHeroCluster.Size;
            _referenceHeroCluster.ZIndex = 47;

            if (_referenceHeroCluster.GetNodeOrNull<PanelContainer>("HeroPortraitFrame") is { } heroFrame)
            {
                heroFrame.AnchorLeft = 0.5f;
                heroFrame.AnchorTop = 0.5f;
                heroFrame.AnchorRight = 0.5f;
                heroFrame.AnchorBottom = 0.5f;
                heroFrame.OffsetLeft = -heroSize * 0.5f;
                heroFrame.OffsetTop = -heroSize * 0.5f;
                heroFrame.OffsetRight = heroSize * 0.5f;
                heroFrame.OffsetBottom = heroSize * 0.5f;
                heroFrame.ClipContents = false;
            }

            var powerSize = Mathf.Clamp(heroSize * 0.62f, 58.0f, 70.0f);
            ResetFreeControl(_powerButton);
            _powerButton.Position = new Vector2(
                width * 0.5f + heroSize * 0.52f + 8.0f,
                heroTop + heroHeight * 0.5f - powerSize * 0.5f);
            _powerButton.Size = new Vector2(powerSize, powerSize);
            _powerButton.CustomMinimumSize = _powerButton.Size;
            _powerButton.ZIndex = 48;
        }

        // Gold belongs to the lower-right cockpit but not the extreme window edge.
        var goldWidth = Mathf.Clamp(width * 0.085f, 124.0f, 150.0f);
        var goldHeight = Mathf.Clamp(height * 0.047f, 36.0f, 42.0f);
        ResetFreeControl(_hudResourceBadge);
        _hudResourceBadge.Position = new Vector2(width * 0.74f, height - goldHeight - 10.0f);
        _hudResourceBadge.Size = new Vector2(goldWidth, goldHeight);
        _hudResourceBadge.CustomMinimumSize = _hudResourceBadge.Size;
        _hudResourceBadge.ZIndex = 49;

        // Use the board artwork's own right-side plaque. The button is only a hitbox
        // and text layer; no second orange polygon should float above the scene.
        var readyWidth = Mathf.Clamp(width * 0.073f, 104.0f, 126.0f);
        var readyHeight = Mathf.Clamp(height * 0.061f, 46.0f, 56.0f);
        ResetFreeControl(_endPreparationButton);
        _endPreparationButton.Position = new Vector2(width * 0.754f, height * 0.405f);
        _endPreparationButton.Size = new Vector2(readyWidth, readyHeight);
        _endPreparationButton.CustomMinimumSize = _endPreparationButton.Size;
        _endPreparationButton.ZIndex = 50;
        ApplyTransparentBoardButtonStyle(_endPreparationButton);
    }

    private static void ApplyTransparentBoardButtonStyle(Button button)
    {
        foreach (var state in new[] { "normal", "hover", "pressed", "focus", "disabled" })
            button.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
    }
}
