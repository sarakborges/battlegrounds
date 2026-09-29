using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private void RefreshBoardCockpitFinishing()
    {
        if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation || !_hudBound)
            return;

        var width = Size.X;
        var height = Size.Y;
        if (width <= 1.0f || height <= 1.0f)
            return;

        // The hand originates from the bottom edge and may barely overlap the hero,
        // but the hero portrait/power must remain visually above it.
        _reserveButtons.ZIndex = 42;
        if (_referenceHeroCluster is not null)
            _referenceHeroCluster.ZIndex = 46;
        _powerButton.ZIndex = 47;

        // Align the interaction with the physical plaque already painted into the
        // right edge of the board cosmetic instead of rendering a second orange tab.
        var readyWidth = Mathf.Clamp(width * 0.067f, 96.0f, 116.0f);
        var readyHeight = Mathf.Clamp(height * 0.068f, 50.0f, 62.0f);
        ResetFreeControl(_endPreparationButton);
        _endPreparationButton.Position = new Vector2(width * 0.755f, height * 0.43f);
        _endPreparationButton.Size = new Vector2(readyWidth, readyHeight);
        _endPreparationButton.CustomMinimumSize = _endPreparationButton.Size;
        _endPreparationButton.ZIndex = 48;

        // Restore the coin/pip grammar after the legacy preparation polish hides it.
        // The numerical current/max value remains visible beside the pips.
        if (match.TryGetPlayer(_session.HumanPlayerId, out var human))
        {
            var maximum = _session.Mod.PreparationRules.GetResourceForRound(Math.Max(1, match.Round));
            _hudResourcePips.Text = BuildResourcePips(human.Resource, maximum);
            _hudResourcePips.Visible = maximum > 0;
        }
    }
}
