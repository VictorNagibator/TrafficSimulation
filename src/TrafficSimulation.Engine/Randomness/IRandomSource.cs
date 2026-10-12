namespace TrafficSimulation.Engine.Randomness;

/// <summary>
/// The source of randomness used by the simulation.
/// </summary>
/// <remarks>
/// Every random decision in the engine (spawning a vehicle, committing to a turn, getting into an
/// accident, rain) goes through this abstraction instead of calling <see cref="Random"/> directly.
/// That is what makes the simulation reproducible: tests inject a deterministic implementation and
/// therefore get exactly the same city, tick after tick.
/// </remarks>
public interface IRandomSource
{
    /// <summary>Returns the next pseudo-random double in the interval <c>[0, 1)</c>.</summary>
    double NextDouble();

    /// <summary>Returns the next pseudo-random integer in the interval <c>[0, exclusiveUpperBound)</c>.</summary>
    /// <param name="exclusiveUpperBound">Exclusive upper bound; must be positive.</param>
    int NextInt(int exclusiveUpperBound);

    /// <summary>Returns the next pseudo-random integer in the interval <c>[inclusiveLowerBound, exclusiveUpperBound)</c>.</summary>
    /// <param name="inclusiveLowerBound">Inclusive lower bound.</param>
    /// <param name="exclusiveUpperBound">Exclusive upper bound.</param>
    int NextRange(int inclusiveLowerBound, int exclusiveUpperBound);
}
/// <summary>
/// Default <see cref="IRandomSource"/> implementation backed by <see cref="System.Random"/>.
/// </summary>
public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    /// <summary>Creates a source seeded from the system clock.</summary>
    public SystemRandomSource()
        : this(new Random())
    {
    }

    /// <summary>Creates a source with an explicit seed, which makes a run reproducible.</summary>
    /// <param name="seed">Seed forwarded to <see cref="System.Random"/>.</param>
    public SystemRandomSource(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    /// <summary>Creates a source that wraps an existing <see cref="System.Random"/> instance.</summary>
    /// <param name="random">The generator to delegate to.</param>
    public SystemRandomSource(Random random) => _random = random;

    /// <summary>Seed of this source, or <see langword="null"/> when it is time based.</summary>
    public int? Seed { get; }

    /// <inheritdoc />
    public double NextDouble() => _random.NextDouble();

    /// <inheritdoc />
    public int NextInt(int exclusiveUpperBound) => _random.Next(exclusiveUpperBound);

    /// <inheritdoc />
    public int NextRange(int inclusiveLowerBound, int exclusiveUpperBound)
        => _random.Next(inclusiveLowerBound, exclusiveUpperBound);
}

/// <summary>
/// An <see cref="IRandomSource"/> that always returns the same value.
/// </summary>
/// <remarks>
/// Handy in tests that need to remove randomness completely, for example to force the outcome of a
/// single probabilistic decision without caring about the order of the calls.
/// </remarks>
public sealed class ConstantRandomSource : IRandomSource
{
    private readonly double _value;

    /// <summary>Creates a source that always returns <paramref name="value"/>.</summary>
    /// <param name="value">Value returned by <see cref="NextDouble"/>.</param>
    public ConstantRandomSource(double value) => _value = value;

    /// <inheritdoc />
    public double NextDouble() => _value;

    /// <inheritdoc />
    /// <remarks>Always returns <c>0</c>, which selects the first option of any collection.</remarks>
    public int NextInt(int exclusiveUpperBound) => 0;

    /// <inheritdoc />
    /// <remarks>Always returns <paramref name="inclusiveLowerBound"/>, which selects the first option.</remarks>
    public int NextRange(int inclusiveLowerBound, int exclusiveUpperBound) => inclusiveLowerBound;
}
