using System.Collections.ObjectModel;
using Battlegrounds.Core.Domain.Ids;
using Battlegrounds.Core.Domain.Units;

namespace Battlegrounds.Core.Domain.Choices;

public sealed class PendingUnitChoice : PendingChoice
{
    private readonly ReadOnlyCollection<UnitDefinition> _options;

    public override PendingChoiceKind Kind => PendingChoiceKind.Unit;
    public IReadOnlyList<UnitDefinition> Options => _options;

    internal PendingUnitChoice(ChoiceId id, IEnumerable<UnitDefinition> options)
        : base(id)
    {
        ArgumentNullException.ThrowIfNull(options);
        var materialized = options.ToArray();
        if (materialized.Length == 0)
            throw new ArgumentException("A pending choice requires at least one option.", nameof(options));
        if (materialized.Any(option => option is null))
            throw new ArgumentException("Choice options cannot contain null values.", nameof(options));
        if (materialized.Select(option => option.Id).Distinct().Count() != materialized.Length)
            throw new ArgumentException("Choice options must be unique by unit id.", nameof(options));

        _options = Array.AsReadOnly(materialized);
    }
}
