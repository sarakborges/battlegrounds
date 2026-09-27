using Battlegrounds.AI;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Leaders;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Preparation;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.AI.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void SelectLeader_SameStateAndSeeds_ProducesSameDecision()
    {
        var leaders = new LeaderCatalog(
        [
            new LeaderDefinition(new LeaderId("a"), "A", 0, 0),
            new LeaderDefinition(new LeaderId("b"), "B", 0, 0),
            new LeaderDefinition(new LeaderId("c"), "C", 0, 0),
        ]);
        var rules = new MatchRules(2, 2);
        var selectionRules = new LeaderSelectionRules(3, LeaderOfferPolicy.IndependentPerPlayer);
        var playerIds = new[] { new PlayerId(0), new PlayerId(1) };
        var leftSelection = LeaderSelectionState.Create(playerIds, rules, selectionRules, leaders, new SeededRandomSource(1234));
        var rightSelection = LeaderSelectionState.Create(playerIds, rules, selectionRules, leaders, new SeededRandomSource(1234));
        var preparationRules = new PreparationRules(3, 1, 10, 3, 1, 1, 7, 10, 2, [3, 4], [5]);
        var leftAgent = new PreparationAiAgent(preparationRules, new SeededRandomSource(9876), leaders: leaders);
        var rightAgent = new PreparationAiAgent(preparationRules, new SeededRandomSource(9876), leaders: leaders);

        var left = leftAgent.SelectLeader(leftSelection, new PlayerId(0));
        var right = rightAgent.SelectLeader(rightSelection, new PlayerId(0));

        Assert.Equal(left, right);
    }
}
