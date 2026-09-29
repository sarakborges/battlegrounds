namespace Battlegrounds.Game;

public partial class Main
{
    public override void _PhysicsProcess(double delta)
    {
        EnsureWebUiInitialized();
        UpdateAutomaticCombines();
        AdvanceWebCombatPlayback(delta);
    }
}
