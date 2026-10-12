using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;
using TrafficSimulation.Engine.Randomness;
using TrafficSimulation.Engine.Simulation;

namespace TrafficSimulation.Tests.Helpers;

/// <summary>
/// A small, fully deterministic city that the tests can build on.
/// </summary>
/// <remarks>
/// The helper hides the repetitive part of arranging a test — creating intersections, adding roads
/// and placing vehicles — so that the tests themselves read as statements about behaviour rather
/// than as setup code. All randomness is removed by default: weather never changes and vehicles
/// never break down, so a failing test fails for exactly one reason.
/// </remarks>
public sealed class TestCity
{
    /// <summary>Predictable values returned in turn by <see cref="QueuedRandomSource"/>.</summary>
    private readonly Queue<double> _doubles = new();

    /// <summary>Creates an empty, deterministic city.</summary>
    /// <param name="seed">Optional seed used by the underlying pseudo-random generator.</param>
    public TestCity(int seed = 1)
    {
        Random = new QueuedRandomSource(seed, _doubles);
        Settings = new CitySettings
        {
            TickSeconds = SimulationConstants.TickSeconds,

            // No weather, no breakdowns and no lane changes unless a test asks for them.
            RainChancePerTick = 0,
            AccidentChancePerTick = 0,
            BreakdownChancePerTick = 0,
            RainAccidentChancePerTick = 0,
            LaneChangeChancePerTick = 0,
            AccelerationKphPerTick = 1,
        };

        City = new City(Settings, Random);
    }

    /// <summary>The city under test.</summary>
    public City City { get; }

    /// <summary>Settings of the city, exposed so tests can tweak traffic behaviour.</summary>
    public CitySettings Settings { get; }

    /// <summary>Random source of the city.</summary>
    public QueuedRandomSource Random { get; }

    /// <summary>Makes the next random draw return exactly <paramref name="value"/>.</summary>
    /// <param name="value">Value the next <see cref="IRandomSource.NextDouble"/> call returns.</param>
    /// <returns>This helper, so calls can be chained.</returns>
    public TestCity ThenRandomDouble(double value)
    {
        _doubles.Enqueue(value);
        return this;
    }

    /// <summary>
    /// Returns the junction at the given position, creating it once and only once.
    /// </summary>
    /// <param name="x">Horizontal position in metres.</param>
    /// <param name="y">Vertical position in metres.</param>
    /// <returns>
    /// The canonical junction for that position, so that the roads built on it really meet.
    /// </returns>
    public Intersection Node(double x, double y) => City.GetOrCreateIntersection(new Vector2(x, y));

    /// <summary>Creates a two-way road between two intersections and adds it to the city.</summary>
    /// <param name="start">Start of the road.</param>
    /// <param name="end">End of the road.</param>
    /// <param name="lanesPerDirection">Number of lanes in each direction.</param>
    /// <param name="name">Optional road name.</param>
    /// <returns>The new road.</returns>
    public Road AddTwoWayRoad(
        Intersection start,
        Intersection end,
        int lanesPerDirection = 1,
        string? name = null)
    {
        Road road = new(start, end)
        {
            Type = RoadType.TwoWay,
            LanesPerDirection = lanesPerDirection,
            Name = name,
        };

        City.AddRoad(road);
        return road;
    }

    /// <summary>Creates a one-way road from start to end and adds it to the city.</summary>
    /// <param name="start">Start of the road.</param>
    /// <param name="end">End of the road.</param>
    /// <param name="lanesPerDirection">Number of lanes.</param>
    /// <returns>The new road.</returns>
    public Road AddOneWayRoad(Intersection start, Intersection end, int lanesPerDirection = 1)
    {
        Road road = new(start, end)
        {
            Type = RoadType.OneWay,
            LanesPerDirection = lanesPerDirection,
        };

        City.AddRoad(road);
        return road;
    }

    /// <summary>Places a stationary vehicle on a road at an exact position.</summary>
    /// <param name="road">Road to place the vehicle on.</param>
    /// <param name="position">Distance from the start of the road, in metres.</param>
    /// <param name="lane">Lane number, counted from the right.</param>
    /// <param name="isMovingForward">Direction of travel.</param>
    /// <param name="speedKph">Initial speed.</param>
    /// <returns>The vehicle that was added to the city.</returns>
    public Vehicle PlaceVehicle(
        Road road,
        double position,
        int lane = 1,
        bool isMovingForward = true,
        double speedKph = 0)
    {
        Vehicle vehicle = Vehicle.Place(road, position, lane, isMovingForward, speedKph);
        City.AddVehicle(vehicle);
        return vehicle;
    }

    /// <summary>Builds the simulation context the city would use for the next tick.</summary>
    /// <returns>A context that can be handed to <see cref="Vehicle.Update"/>.</returns>
    public SimulationContext CreateContext()
        => new(City, City.TrafficLights, City.IsRaining, Random);
}

/// <summary>
/// An <see cref="IRandomSource"/> that returns values the test chooses.
/// </summary>
/// <remarks>
/// <para>
/// Values queued with <see cref="TestCity.ThenRandomDouble"/> are returned first, in order. Once the
/// queue is empty the source falls back to a seeded pseudo-random generator, so a test only has to
/// pin down the draws it actually cares about.
/// </para>
/// <para>
/// Integer draws always return the first option unless a specific index was queued, which makes
/// "pick a random road" and "pick a random lane" reproducible.
/// </para>
/// </remarks>
public sealed class QueuedRandomSource : IRandomSource
{
    private readonly Queue<double> _doubles;
    private readonly Random _fallback;
    private readonly Queue<int> _integers = new();

    /// <summary>Creates the source.</summary>
    /// <param name="seed">Seed of the fallback generator.</param>
    /// <param name="doubles">Queue the test fills through <see cref="TestCity.ThenRandomDouble"/>.</param>
    public QueuedRandomSource(int seed, Queue<double> doubles)
    {
        _fallback = new Random(seed);
        _doubles = doubles;
    }

    /// <summary>Queues the index that the next integer draw should return.</summary>
    /// <param name="value">Index to return.</param>
    /// <returns>This source, so calls can be chained.</returns>
    public QueuedRandomSource ThenInteger(int value)
    {
        _integers.Enqueue(value);
        return this;
    }

    /// <inheritdoc />
    public double NextDouble() => _doubles.Count > 0 ? _doubles.Dequeue() : _fallback.NextDouble();

    /// <inheritdoc />
    public int NextInt(int exclusiveUpperBound) => _integers.Count > 0 ? _integers.Dequeue() : 0;

    /// <inheritdoc />
    public int NextRange(int inclusiveLowerBound, int exclusiveUpperBound)
        => _integers.Count > 0
            ? _integers.Dequeue()
            : inclusiveLowerBound;
}
