using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Cards;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Recruitment;

namespace Battlegrounds.Core.Domain.Players;

public sealed class PlayerState
{
    private readonly List<MinionInstance> _hand = [];
    private readonly List<MinionInstance> _board = [];
    private readonly List<CardDefinition> _tavernOffer = [];
    private readonly ReadOnlyCollection<MinionInstance> _handView;
    private readonly ReadOnlyCollection<MinionInstance> _boardView;
    private readonly ReadOnlyCollection<CardDefinition> _tavernOfferView;

    public PlayerId Id { get; }
    public int Gold { get; private set; }
    public int TavernTier { get; private set; } = 1;
    public int? UpgradeCost { get; private set; }
    public bool IsReadyForCombat { get; private set; }
    public IReadOnlyList<MinionInstance> Hand => _handView;
    public IReadOnlyList<MinionInstance> Board => _boardView;
    public IReadOnlyList<CardDefinition> TavernOffer => _tavernOfferView;

    internal PlayerState(PlayerId id)
    {
        Id = id;
        _handView = _hand.AsReadOnly();
        _boardView = _board.AsReadOnly();
        _tavernOfferView = _tavernOffer.AsReadOnly();
    }

    internal void BeginRecruitment(int round, RecruitmentRules rules)
    {
        Gold = rules.GetGoldForRound(round);
        IsReadyForCombat = false;

        if (UpgradeCost is null)
        {
            UpgradeCost = rules.GetInitialUpgradeCost(TavernTier);
        }
        else if (round > 1)
        {
            UpgradeCost = Math.Max(0, UpgradeCost.Value - 1);
        }
    }

    internal bool CanAfford(int amount) => Gold >= amount;

    internal void SpendGold(int amount)
    {
        if (amount < 0 || amount > Gold)
        {
            throw new InvalidOperationException("Gold spend violates player state invariants.");
        }

        Gold -= amount;
    }

    internal void GainGold(int amount, int maximumGold)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        Gold = Math.Min(maximumGold, Gold + amount);
    }

    internal CardDefinition TakeTavernCard(int slot)
    {
        var card = _tavernOffer[slot];
        _tavernOffer.RemoveAt(slot);
        return card;
    }

    internal void ReplaceTavernOffer(IEnumerable<CardDefinition> cards)
    {
        _tavernOffer.Clear();
        _tavernOffer.AddRange(cards);
    }

    internal void AddToHand(MinionInstance minion) => _hand.Add(minion);

    internal MinionInstance MoveHandMinionToBoard(int handSlot)
    {
        var minion = _hand[handSlot];
        _hand.RemoveAt(handSlot);
        _board.Add(minion);
        return minion;
    }

    internal MinionInstance RemoveBoardMinion(int boardSlot)
    {
        var minion = _board[boardSlot];
        _board.RemoveAt(boardSlot);
        return minion;
    }

    internal void UpgradeTavern(RecruitmentRules rules)
    {
        if (TavernTier >= rules.MaximumTavernTier || UpgradeCost is null)
        {
            throw new InvalidOperationException("Cannot upgrade beyond the maximum tavern tier.");
        }

        SpendGold(UpgradeCost.Value);
        TavernTier++;
        UpgradeCost = rules.GetInitialUpgradeCost(TavernTier);
    }

    internal void MarkReadyForCombat() => IsReadyForCombat = true;
}
