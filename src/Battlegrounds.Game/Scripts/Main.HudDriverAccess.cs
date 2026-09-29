namespace Battlegrounds.Game;

public partial class Main
{
    internal void TickSemanticHud()
    {
        RefreshSemanticHud();
        RefreshTavernHud();
        ApplyTavernAspectPolish();
        RefreshPreparationPresentationPolish();
        RefreshLeaderFramePolish();
        RefreshOpponentRailReferencePolish();
        RefreshCombatReferencePolish();
        RefreshReferenceCockpitLayout();
        RefreshBoardReferenceLayout();
        RefreshBoardOfferReferencePolish();
        RefreshBoardCockpitFinishing();
    }
}
