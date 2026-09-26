using Battlegrounds.Core.Randomness;

namespace Battlegrounds.Core.Tests.Randomness;

public sealed class SeededRandomSourceTests
{
    [Fact]
    public void SameSeedProducesSameSequence()
    {
        var first = new SeededRandomSource(42);
        var second = new SeededRandomSource(42);

        var firstSequence = Enumerable.Range(0, 20)
            .Select(_ => first.NextInt(0, 10_000))
            .ToArray();
        var secondSequence = Enumerable.Range(0, 20)
            .Select(_ => second.NextInt(0, 10_000))
            .ToArray();

        Assert.Equal(firstSequence, secondSequence);
    }
}
