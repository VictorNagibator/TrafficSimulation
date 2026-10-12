using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.Engine.Domain;

/// <summary>
/// A spatial index of the junctions of a city.
/// </summary>
/// <remarks>
/// <para>
/// Roads are wired to their end points by reference, and a lot of behaviour depends on recognising
/// that two roads meet at a junction. That only works when a position is represented by exactly one
/// <see cref="Intersection"/> instance, so junctions are never created twice: everything that needs
/// one asks this index for it.
/// </para>
/// <para>
/// Looking a junction up by its exact position is enough because every junction is placed by
/// <see cref="GetOrCreate"/>, which rounds the coordinates to millimetres first. Two floating point
/// values that describe the same place therefore produce the same key.
/// </para>
/// </remarks>
public sealed class IntersectionIndex
{
    private const double PrecisionFactor = 1000;

    private readonly Dictionary<(long X, long Y), Intersection> _byPosition = [];

    /// <summary>All junctions, in the order they were first placed.</summary>
    public IReadOnlyCollection<Intersection> All => _byPosition.Values;

    /// <summary>Number of junctions in the index.</summary>
    public int Count => _byPosition.Count;

    /// <summary>
    /// Returns the junction at a position, creating it when it does not exist yet.
    /// </summary>
    /// <param name="position">Position of the junction, in metres.</param>
    /// <returns>The canonical junction for that position.</returns>
    public Intersection GetOrCreate(Vector2 position)
    {
        (long X, long Y) key = KeyOf(position);

        if (_byPosition.TryGetValue(key, out Intersection? existing))
        {
            return existing;
        }

        Intersection created = new(Round(position));
        _byPosition[key] = created;
        return created;
    }

    /// <summary>Returns the junction at a position, or <see langword="null"/> when there is none.</summary>
    /// <param name="position">Position to look up, in metres.</param>
    /// <returns>The junction, or <see langword="null"/>.</returns>
    public Intersection? Find(Vector2 position)
        => _byPosition.GetValueOrDefault(KeyOf(position));

    /// <summary>Clears the index.</summary>
    public void Clear() => _byPosition.Clear();

    /// <summary>Computes the lookup key of a position, rounding it to millimetres.</summary>
    /// <param name="position">Position in metres.</param>
    private static (long X, long Y) KeyOf(Vector2 position)
        => ((long)Math.Round(position.X * PrecisionFactor), (long)Math.Round(position.Y * PrecisionFactor));

    /// <summary>Rounds a position to the precision used by the index.</summary>
    /// <param name="position">Position in metres.</param>
    private static Vector2 Round(Vector2 position)
        => new(
            Math.Round(position.X, 3, MidpointRounding.AwayFromZero),
            Math.Round(position.Y, 3, MidpointRounding.AwayFromZero));
}
