using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Domain.Match;
using Battlegrounds.Core.Domain.Players;
using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Domain.Recruitment;

public sealed class RecruitmentEngine
{
    private readonly RecruitmentRules _rules;
    private readonly ITavernPool _tavernPool;
    private readonly IRandomSource _randomSource;

    public RecruitmentEngine(
        RecruitmentRules rules,
        ITavernPool tavernPool,
        IRandomSource randomSource)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _tavernPool = tavernPool ?? throw new ArgumentNullException(nameof(tavernPool));
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
            PrepareNextOffer);

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
            FreezeTavernCommand => FreezeTavern(player),
            UnfreezeTavernCommand => UnfreezeTavern(player),
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

        var minion = player.Board[command.BoardSlot];
        _tavernPool.ReturnMinion(minion.Definition);
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

        var offer = _tavernPool.ExchangeOffer(
            player.TavernOffer.ToArray(),
            player.TavernTier,
            _rules.GetOfferSize(player.TavernTier),
            _randomSource);

        player.SpendGold(_rules.RefreshCost);
        player.ReplaceTavernOffer(ValidateOffer(offer, player.TavernTier));
        player.ClearTavernFrozen();

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

    private static RecruitmentCommandResult FreezeTavern(PlayerState player)
    {
        if (player.IsTavernFrozen)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.TavernAlreadyFrozen);
        }

        player.SetTavernFrozen(true);
        return RecruitmentCommandResult.Success();
    }

    private static RecruitmentCommandResult UnfreezeTavern(PlayerState player)
    {
        if (!player.IsTavernFrozen)
        {
            return RecruitmentCommandResult.Failure(RecruitmentFailureCode.TavernNotFrozen);
        }

        player.SetTavernFrozen(false);
        return RecruitmentCommandResult.Success();
    }

    private static RecruitmentCommandResult EndRecruitment(PlayerState player)
    {
        player.MarkReadyForCombat();
        return RecruitmentCommandResult.Success();
    }

    private IReadOnlyList<CardDefinition> PrepareNextOffer(PlayerState player)
    {
        var expectedCount = _rules.GetOfferSize(player.TavernTier);

        if (!player.IsTavernFrozen)
        {
            var replacement = _tavernPool.ExchangeOffer(
                player.TavernOffer.ToArray(),
                player.TavernTier,
                expectedCount,
                _randomSource);

            return ValidateOffer(replacement, player.TavernTier);
        }

        if (player.TavernOffer.Count > expectedCount)
        {
            throw new InvalidOperationException("Frozen tavern offer exceeds the configured offer size.");
        }

        var missingCount = expectedCount - player.TavernOffer.Count;
        if (missingCount == 0)
        {
            return player.TavernOffer.ToArray();
        }

        var additions = _tavernPool.DrawOffer(player.TavernTier, missingCount, _randomSource);
        var combined = player.TavernOffer.Concat(additions).ToArray();
        return ValidateOffer(combined, player.TavernTier);
    }

    private IReadOnlyList<CardDefinition> ValidateOffer(
        IReadOnlyList<CardDefinition> offer,
        int tavernTier)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var expectedCount = _rules.GetOfferSize(tavernTier);
        if (offer.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Tavern pool returned {offer.Count} cards; expected {expectedCount}.");
        }

        if (offer.Any(card => card is null || card.TavernTier > tavernTier))
        {
            throw new InvalidOperationException("Tavern pool returned an ineligible card.");
        }

        return offer;
    }
}
