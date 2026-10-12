using TrafficSimulation.Engine.Geometry;
using TrafficSimulation.Engine.Randomness;
using TrafficSimulation.Engine.Simulation;

namespace TrafficSimulation.Engine.Domain;

/// <summary>
/// The simulated city: a road network, the vehicles driving on it, its traffic lights and its
/// configuration.
/// </summary>
/// <remarks>
/// <para>
/// The city is the root of the simulation and the only object that owns mutable shared state. It is
/// deliberately free of any user-interface concern: it can be created, driven forward tick by tick
/// and inspected entirely from a unit test.
/// </para>
/// <para>
/// All structural changes (adding or removing roads, changing a road type) go through the methods of
/// this class rather than through property setters on <see cref="Road"/> or
/// <see cref="Intersection"/>, because they have to keep several pieces of state in sync: the road
/// list, the incoming and outgoing sets of both end points, the traffic lights and the planned route
/// of every affected vehicle.
/// </para>
/// <para>
/// The simulation runs in two phases per tick. First the weather and the traffic lights advance, then
/// every vehicle is updated against the resulting <see cref="SimulationContext"/>. Doing it in that
/// order means vehicles never observe each other half updated, which keeps a run reproducible.
/// </para>
/// </remarks>
public sealed class City
{
    private readonly List<Road> _roads = [];
    private readonly List<Vehicle> _vehicles = [];
    private readonly SimulationEnvironment _environment;
    private readonly IRandomSource _random;
    private readonly ChangeNotifier _changes;

    private readonly IntersectionIndex _intersections = new();

    private CityEditor? _editor;

    /// <summary>Creates an empty city with default settings and a time based random source.</summary>
    public City()
        : this(new CitySettings(), new SystemRandomSource())
    {
    }

    /// <summary>Creates an empty city with an explicit random source, which makes runs reproducible.</summary>
    /// <param name="random">Source of randomness for weather, spawning and vehicle decisions.</param>
    public City(IRandomSource random)
        : this(new CitySettings(), random)
    {
    }

    /// <summary>Creates an empty city.</summary>
    /// <param name="settings">Configuration of the city.</param>
    /// <param name="random">Source of randomness for weather, spawning and vehicle decisions.</param>
    public City(CitySettings settings, IRandomSource random)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _environment = new SimulationEnvironment(settings, random);
        _changes = new ChangeNotifier(() => Changed?.Invoke(this, EventArgs.Empty));
    }
    /// <summary>Raised after any change to the road network, its lights or its vehicles.</summary>
    /// <remarks>
    /// The view layer subscribes to this event to know when a redraw is required while the
    /// simulation is paused.
    /// </remarks>
    public event EventHandler? Changed;

    /// <summary>Configuration of the city, shared with its vehicles.</summary>
    public CitySettings Settings { get; }

    /// <summary>All roads of the city, in insertion order.</summary>
    public IReadOnlyList<Road> Roads => _roads;

    /// <summary>All vehicles of the city, in no particular order.</summary>
    /// <remarks>
    /// The city keeps its own list instead of asking every road, so that iterating the vehicles does
    /// not allocate a fresh collection on every tick.
    /// </remarks>
    public IReadOnlyList<Vehicle> Vehicles => _vehicles;

    /// <summary>All traffic lights of the city.</summary>
    public TrafficLightController TrafficLights => _environment.TrafficLights;

    /// <summary>
    /// Structural editing operations (creating and splitting roads) that keep the network consistent.
    /// </summary>
    public CityEditor Editor => _editor ??= new CityEditor(this);

    /// <summary>
    /// Seed of the random source, when the city was created with a seeded one; otherwise <c>0</c>.
    /// </summary>
    public int Seed => (_random as SystemRandomSource)?.Seed ?? 0;

    /// <summary>Whether it currently rains.</summary>
    public bool IsRaining => _environment.IsRaining;

    /// <summary>Speed limit applied to every road of the city, in km/h.</summary>
    public double SpeedLimitKph
    {
        get => Settings.SpeedLimitKph;
        set
        {
            Settings.SpeedLimitKph = value;
            foreach (Road road in _roads)
            {
                road.SpeedLimitKph = Settings.SpeedLimitKph;
            }

            RaiseChanged();
        }
    }

    /// <summary>Pavement quality applied to every road of the city, in the range (0, 1].</summary>
    public double RoadQuality
    {
        get => Settings.RoadQuality;
        set
        {
            Settings.RoadQuality = value;
            foreach (Road road in _roads)
            {
                road.Quality = Settings.RoadQuality;
            }

            RaiseChanged();
        }
    }

    /// <summary>Probability per tick that a moving vehicle gets into an accident.</summary>
    public double AccidentChancePerTick
    {
        get => Settings.AccidentChancePerTick;
        set => Settings.AccidentChancePerTick = value;
    }

    /// <summary>Probability per tick that a moving vehicle breaks down.</summary>
    public double BreakdownChancePerTick
    {
        get => Settings.BreakdownChancePerTick;
        set => Settings.BreakdownChancePerTick = value;
    }

    /// <summary>Probability per tick that rain starts.</summary>
    public double RainChancePerTick
    {
        get => Settings.RainChancePerTick;
        set => Settings.RainChancePerTick = value;
    }

    /// <summary>How long a rain period lasts, in ticks.</summary>
    public int RainDurationTicks
    {
        get => Settings.RainDurationTicks;
        set => Settings.RainDurationTicks = value;
    }

    /// <summary>
    /// All junctions of the city, in the order in which they were placed.
    /// </summary>
    /// <remarks>
    /// Junctions are owned by this index rather than derived from the roads, which is what guarantees
    /// that two roads meeting at the same place share one junction instance.
    /// </remarks>
    public IReadOnlyCollection<Intersection> Intersections => _intersections.All;

    /// <summary>Spatial index of the junctions, used by the map editor to snap and to create nodes.</summary>
    public IntersectionIndex IntersectionIndex => _intersections;

    /// <summary>
    /// Adds a road to the network and registers it with both of its end points.
    /// </summary>
    /// <param name="road">Road to add; its end points must already be set.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="road"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the road has no end points or is already on the map.</exception>
    public void AddRoad(Road road)
    {
        ArgumentNullException.ThrowIfNull(road);

        if (road.StartIntersection is null || road.EndIntersection is null)
        {
            throw new InvalidOperationException("Both end points of the road must be set before adding it to the city.");
        }

        if (_roads.Contains(road))
        {
            throw new InvalidOperationException("The road is already part of the city.");
        }

        road.SpeedLimitKph = Settings.SpeedLimitKph;
        road.Quality = Settings.RoadQuality;

        _roads.Add(road);
        road.StartIntersection.AddRoad(road);
        road.EndIntersection.AddRoad(road);

        RaiseChanged();
    }

    /// <summary>Creates a road between two intersections and adds it to the network.</summary>
    /// <param name="start">Intersection where the road begins.</param>
    /// <param name="end">Intersection where the road ends.</param>
    /// <returns>The newly created road.</returns>
    public Road CreateRoad(Intersection start, Intersection end)
    {
        Road road = new(start, end);
        AddRoad(road);
        return road;
    }

    /// <summary>Removes a road and everything that depends on it.</summary>
    /// <param name="road">Road to remove.</param>
    /// <returns><see langword="true"/> when the road was part of the city.</returns>
    /// <remarks>
    /// Vehicles standing on the road are removed as well, and the vehicles that were planning to
    /// enter it re-plan their route.
    /// </remarks>
    public bool RemoveRoad(Road road)
    {
        ArgumentNullException.ThrowIfNull(road);

        if (!_roads.Remove(road))
        {
            return false;
        }

        road.StartIntersection.RemoveRoad(road);
        road.EndIntersection.RemoveRoad(road);

        // Vehicles cannot exist on a road that no longer exists.
        foreach (Vehicle vehicle in road.Vehicles.ToList())
        {
            _vehicles.Remove(vehicle);
        }

        road.ClearVehicles();

        // Vehicles with their sights set on this road need a new plan.
        if (_roads.Count > 0)
        {
            foreach (Vehicle vehicle in _vehicles)
            {
                if (vehicle.NextRoad == road)
                {
                    vehicle.RefreshNextRoad();
                }
            }
        }

        RaiseChanged();
        return true;
    }

    /// <summary>
    /// Changes the type of a road and repairs everything that type change invalidates.
    /// </summary>
    /// <param name="road">Road whose type changes.</param>
    /// <param name="type">The new type.</param>
    /// <remarks>
    /// A road that becomes one-way can no longer be driven in one direction. Vehicles travelling the
    /// wrong way are removed from the map, and every vehicle that had planned to use the road
    /// re-plans its route, because the set of legal turns just changed.
    /// </remarks>
    public void SetRoadType(Road road, RoadType type)
    {
        ArgumentNullException.ThrowIfNull(road);

        if (road.Type == type)
        {
            return;
        }

        road.Type = type;
        road.StartIntersection.RefreshRoad(road);
        road.EndIntersection.RefreshRoad(road);

        if (type == RoadType.OneWay)
        {
            // On a one-way road the only legal direction is start -> end.
            foreach (Vehicle vehicle in road.Vehicles.Where(vehicle => !vehicle.IsMovingForward).ToList())
            {
                RemoveVehicle(vehicle);
            }
        }

        foreach (Vehicle vehicle in _vehicles)
        {
            vehicle.RefreshNextRoad();
        }

        RaiseChanged();
    }

    /// <summary>Sets the number of lanes of a road.</summary>
    /// <param name="road">Road to modify.</param>
    /// <param name="lanesPerDirection">New number of lanes in each direction.</param>
    public void SetLanesPerDirection(Road road, int lanesPerDirection)
    {
        ArgumentNullException.ThrowIfNull(road);

        int clamped = Math.Clamp(
            lanesPerDirection,
            SimulationConstants.MinLanesPerDirection,
            SimulationConstants.MaxLanesPerDirection);

        if (road.LanesPerDirection == clamped)
        {
            return;
        }

        road.LanesPerDirection = clamped;

        // Vehicles standing in a lane that no longer exists have to be moved.
        foreach (Vehicle vehicle in road.Vehicles.Where(vehicle => vehicle.LaneNumber > clamped).ToList())
        {
            vehicle.LaneNumber = clamped;
            vehicle.RefreshNextRoad();
        }

        RaiseChanged();
    }

    /// <summary>Adds an existing vehicle to the city and registers it with its current road.</summary>
    /// <param name="vehicle">Vehicle to add; it must already be placed on a road of this city.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="vehicle"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the vehicle is not placed on a road of this city.</exception>
    public void AddVehicle(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (vehicle.CurrentRoad is null || !_roads.Contains(vehicle.CurrentRoad))
        {
            throw new InvalidOperationException("The vehicle must be placed on a road that belongs to this city.");
        }

        if (_vehicles.Contains(vehicle))
        {
            return;
        }

        // Repair the junction references: a vehicle may have been built with an equivalent but
        // distinct Intersection instance, and the stopping logic compares junctions by reference.
        vehicle.NextIntersection = vehicle.IsMovingForward
            ? vehicle.CurrentRoad.EndIntersection
            : vehicle.CurrentRoad.StartIntersection;

        vehicle.ApplySettings(Settings.ToVehicleSettings(), _random);
        _vehicles.Add(vehicle);
        vehicle.CurrentRoad.AddVehicle(vehicle);

        // The route is chosen once the vehicle is on the map, so that it sees the current lights.
        vehicle.RefreshNextRoad();

        RaiseChanged();
    }

    /// <summary>Removes a vehicle from the city.</summary>
    /// <param name="vehicle">Vehicle to remove.</param>
    /// <returns><see langword="true"/> when the vehicle was part of the city.</returns>
    public bool RemoveVehicle(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        if (!_vehicles.Remove(vehicle))
        {
            return false;
        }

        vehicle.CurrentRoad?.RemoveVehicle(vehicle);
        RaiseChanged();
        return true;
    }

    /// <summary>Creates a vehicle at a random position and adds it to the city.</summary>
    /// <returns>The new vehicle, or <see langword="null"/> when the city has no roads.</returns>
    public Vehicle? CreateVehicle()
    {
        Vehicle? vehicle = Vehicle.Spawn(this, _random);
        if (vehicle is null)
        {
            return null;
        }

        _vehicles.Add(vehicle);
        vehicle.CurrentRoad.AddVehicle(vehicle);

        RaiseChanged();
        return vehicle;
    }

    /// <summary>Removes every vehicle from the city.</summary>
    public void RemoveAllVehicles()
    {
        foreach (Road road in _roads)
        {
            road.ClearVehicles();
        }

        _vehicles.Clear();
        RaiseChanged();
    }

    /// <summary>Removes every road, vehicle and traffic light, leaving an empty city.</summary>
    public void Clear()
    {
        foreach (Road road in _roads)
        {
            road.StartIntersection.RemoveRoad(road);
            road.EndIntersection.RemoveRoad(road);
            road.ClearVehicles();
        }

        _roads.Clear();
        _vehicles.Clear();
        _environment.TrafficLights.Clear();
        _environment.ClearWeather();
        _intersections.Clear();

        RaiseChanged();
    }

    /// <summary>Advances the whole simulation by one tick.</summary>
    public void Update()
    {
        // Phase one: the world around the vehicles changes.
        _environment.Update();
        _environment.TrafficLights.Tick();

        // Phase two: every vehicle reacts to the world as it is now.
        SimulationContext context = new(this, _environment.TrafficLights, _environment.IsRaining, _random);
        foreach (Vehicle vehicle in _vehicles.ToList())
        {
            vehicle.Update(context);
        }
    }

    /// <summary>Advance the simulation by a number of ticks.</summary>
    /// <param name="ticks">Number of ticks to simulate.</param>
    public void Update(int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            Update();
        }
    }

    /// <summary>
    /// Returns the junction at a position, creating it when the city does not have one there yet.
    /// </summary>
    /// <param name="position">Position of the junction, in metres.</param>
    /// <returns>
    /// The canonical junction for that position. Roads attached to the returned instance are
    /// guaranteed to meet any other road that uses the same position.
    /// </returns>
    public Intersection GetOrCreateIntersection(Vector2 position) => _intersections.GetOrCreate(position);

    /// <summary>Notifies subscribers that the city changed.</summary>
    internal void RaiseChanged() => _changes.Notify();

    /// <summary>
    /// Runs an action while change notifications are batched, so that a compound edit produces a
    /// single notification.
    /// </summary>
    /// <param name="action">Action that may change the city any number of times.</param>
    /// <remarks>
    /// The notification is still raised synchronously at the end of the batch: the method exists to
    /// avoid redrawing the map several times for one user gesture, not to make editing asynchronous.
    /// </remarks>
    internal void BatchChanges(Action action) => _changes.Batch(action);
}
