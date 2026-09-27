using Battlegrounds.Core.Domain.Combines;
using Battlegrounds.Core.Domain.Powers;
using Battlegrounds.Core.Domain.Preparation;

namespace Battlegrounds.AI;

public sealed class PreparationAgentContext
{
    public PreparationRules Rules { get; }
    public PowerCatalog? Powers { get; }
    public UnitCombineCatalog Combines { get; }

    public PreparationAgentContext(
        PreparationRules rules,
        PowerCatalog? powers = null,
        UnitCombineCatalog? combines = null)
    {
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        Powers = powers;
        Combines = combines ?? new UnitCombineCatalog([]);
    }
}
