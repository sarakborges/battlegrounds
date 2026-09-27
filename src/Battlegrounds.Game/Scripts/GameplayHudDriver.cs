using Godot;

namespace Battlegrounds.Game;

public partial class GameplayHudDriver : Node
{
    public override void _Process(double delta)
    {
        if (GetParent() is Main main)
            main.TickSemanticHud();
    }
}
