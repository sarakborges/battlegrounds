namespace Battlegrounds.Core.Randomness;

public interface IRandomSource
{
    int NextInt(int minInclusive, int maxExclusive);
}
