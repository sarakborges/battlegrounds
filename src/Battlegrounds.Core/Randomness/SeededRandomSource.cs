namespace Battlegrounds.Core.Randomness;

public sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    public int Seed { get; }

    public SeededRandomSource(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "Maximum must be greater than minimum.");
        }

        return _random.Next(minInclusive, maxExclusive);
    }
}
