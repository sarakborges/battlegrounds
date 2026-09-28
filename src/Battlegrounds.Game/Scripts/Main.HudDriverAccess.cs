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
    }
}
