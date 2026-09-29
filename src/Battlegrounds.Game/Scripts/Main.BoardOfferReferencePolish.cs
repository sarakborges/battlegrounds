using Battlegrounds.Core.Domain.Match;
using Godot;

namespace Battlegrounds.Game;

public partial class Main
{
    private void RefreshBoardOfferReferencePolish()
    {
        if (_session?.Match is not MatchState match || match.Phase != MatchPhase.Preparation)
            return;

        if (_tavernActionOffers is null)
            return;

        foreach (var card in _tavernActionOffers.GetChildren().OfType<PresentationCardButton>())
        {
            var margin = card.GetChildren().OfType<MarginContainer>().FirstOrDefault();
            var column = margin?.GetChildren().OfType<VBoxContainer>().FirstOrDefault();
            if (column is null)
                continue;

            column.Alignment = BoxContainer.AlignmentMode.End;
            column.AddThemeConstantOverride("separation", 0);
            var labels = column.GetChildren().OfType<Label>().ToArray();
            if (labels.Length == 0)
                continue;

            // Tavern actions should read as one offer object, not as an image with
            // a stack of dashboard labels underneath it. Keep only a compact name;
            // tier, cost, rules and detail text remain available on hover/inspect.
            labels[0].Visible = true;
            labels[0].ThemeTypeVariation = "CaptionLabel";
            labels[0].AutowrapMode = TextServer.AutowrapMode.Off;
            labels[0].TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            labels[0].HorizontalAlignment = HorizontalAlignment.Center;
            labels[0].CustomMinimumSize = new Vector2(0, 20);

            for (var index = 1; index < labels.Length; index++)
                labels[index].Visible = false;
        }
    }
}
