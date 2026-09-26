using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Recruitment;

public sealed class RecruitmentEngine
{
    private readonly RecruitmentRules _rules;
    private readonly ITavernOfferSource _offerSource;
    private readonly IRandomSource _randomSource;

    public RecruitmentEngine(
        RecruitmentRules rules,
        ITavernOfferSource offerSource,
        IRandomSource randomSource)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _offerSource = offerSource ?? throw new ArgumentNullException(nameof(offerSource));
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
    }

    public void BeginRecruitment(MatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.Phase is not (MatchPhase.Setup or MatchPhase.Combat))
        {
            throw new InvalidOperationException($"Cannot begin recruitment from {match.Phase}.");
        }

        var offers = match.Players.ToDictionary(
            player => player.Id,
            player => DrawValidatedOffer(player.TavernTier));

        match.BeginRecruitment();

        foreach (var player in match.Players)
        {
            player.BeginRecruitment(match.Round, _rules);
            player.ReplaceTavernOffer(offers[player.Id]);
        }

        match.MarkChanged();
    }

    public RecruitmentCommandResult Execute(MatchState match, IRecruitmentCommand command)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(command);

        if (match.Phase != MatchPhase.Recruitment)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.MatchNotInRecruitment);
        }

        if (!match.TryGetPlayer(command.PlayerId, out var player))
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.PlayerNotFound);
        }

        if (player.IsReadyForCombat)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.PlayerAlreadyReady);
        }

        var result = command switch
        {
            BuyMinionCommand buy => BuyMinion(match, player, buy),
            SellMinionCommand sell => SellMinion(player, sell),
            PlayMinionCommand play => PlayMinion(player, play),
            RefreshTavernCommand => RefreshTavern(player),
            UpgradeTavernCommand => UpgradeTavern(player),
            EndRecruitmentCommand => EndRecruitment(player),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.GetType().Name, "Unsupported recruitment command."),
        };

        if (!result.Succeeded)
        {
            return result;
        }

        if (match.Players.All(candidate => candidate.IsReadyForCombat))
        {
            match.BeginCombat();
        }

        match.MarkChanged();
        return result;
    }

    private RecruitmentCommandResult BuyMinion(
        MatchState match,
        PlayerState player,
        BuyMinionCommand command)
    {
        if (command.TavernSlot < 0 || command.TavernSlot >= player.TavernOffer.Count)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.InvalidTavernSlot);
        }

        if (player.Hand.Count >= _rules.HandCapacity)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.HandFull);
        }

        if (!player.CanAfford(_rules.BuyCost))
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.InsufficientGold);
        }

        var definition = player.TakeTavernCard(command.TavernSlot);
        var minion = match.CreateMinion(definition);
        player.AddToHand(minion);
        player.SpendGold(_rules.BuyCost);

        return RecruitmentCommandResult.Success();
    }

    private RecruitmentCommandResult SellMinion(PlayerState player, SellMinionCommand command)
    {
        if (command.BoardSlot < 0 || command.BoardSlot >= player.Board.Count)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.InvalidBoardSlot);
        }

        player.RemoveBoardMinion(command.BoardSlot);
        player.GainGold(_rules.SellReward, _rules.MaximumGold);

        return RecruitmentCommandResult.Success();
    }

    private RecruitmentCommandResult PlayMinion(PlayerState player, PlayMinionCommand command)
    {
        if (command.HandSlot < 0 || command.HandSlot >= player.Hand.Count)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.InvalidHandSlot);
        }

        if (player.Board.Count >= _rules.BoardCapacity)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.BoardFull);
        }

        player.MoveHandMinionToBoard(command.HandSlot);
        return RecruitmentCommandResult.Success();
    }

    private RecruitmentCommandResult RefreshTavern(PlayerState player)
    {
        if (!player.CanAfford(_rules.RefreshCost))
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.InsufficientGold);
        }

        var offer = DrawValidatedOffer(player.TavernTier);
        player.SpendGold(_rules.RefreshCost);
        player.ReplaceTavernOffer(offer);

        return RecruitmentCommandResult.Success();
    }

    private RecruitmentCommandResult UpgradeTavern(PlayerState player)
    {
        if (player.TavernTier >= _rules.MaximumTavernTier || player.UpgradeCost is null)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.MaximumTavernTier);
        }

        if (!player.CanAfford(player.UpgradeCost.Value))
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.InsufficientGold);
        }

        player.UpgradeTavern(_rules);
        return RecruitmentCommandResult.Success();
    }

    private static RecruitmentCommandResult EndRecruitment(PlayerState player)
    {
        player.MarkReadyForCombat();
        return RecruitmentCommandResult.Success();
    }

    private IReadOnlyList<CardDefinition> DrawValidatedOffer(int tavernTier)
    {
        var expectedCount = _rules.GetOfferSize(tavernTier);
        var offer = _offerSource.DrawOffer(tavernTier, expectedCount, _randomSource)
            ?? throw new InvalidOperationException("Tavern offer source returned null.");

        if (offer.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Tavern offer source returned {offer.Count} cards; expected {expectedCount}.");
        }

        if (offer.Any(card => card is null || card.TavernTier > tavernTier))
        {
            throw new InvalidOperationException("Tavern offer source returned an ineligible card.");
        }

        return offer.ToArray();
    }
}
