using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.Engine.Domain;

/// <summary>Direction of travel along a road, as seen from a vehicle standing on it.</summary>
public enum RoadTravelDirection
{
    /// <summary>From <see cref="Road.StartIntersection"/> towards <see cref="Road.EndIntersection"/>.</summary>
    Forward,

    /// <summary>From <see cref="Road.EndIntersection"/> towards <see cref="Road.StartIntersection"/>.</summary>
    Backward,
}

/// <summary>Turn a vehicle may perform when it leaves a road through an intersection.</summary>
public enum RoadDirection
{
    /// <summary>Turn to the driver's left.</summary>
    Left,

    /// <summary>Continue straight on.</summary>
    Forward,

    /// <summary>Turn to the driver's right.</summary>
    Right,
}

/// <summary>How a road may be used by traffic.</summary>
public enum RoadType
{
    /// <summary>Traffic flows in a single direction, from start to end.</summary>
    OneWay,

    /// <summary>Traffic flows in both directions; each direction has its own lanes.</summary>
    TwoWay,
}

/// <summary>Traffic density of a road, used by the "watch traffic jams" overlay.</summary>
public enum TrafficJamLevel
{
    /// <summary>Up to a quarter of the theoretical capacity is used.</summary>
    Free,

    /// <summary>Up to half of the theoretical capacity is used.</summary>
    Busy,

    /// <summary>Up to three quarters of the theoretical capacity is used.</summary>
    Congested,

    /// <summary>More than three quarters of the theoretical capacity is used.</summary>
    Jammed,
}

/// <summary>
/// A straight stretch of road between two intersections.
/// </summary>
/// <remarks>
/// <para>
/// A road is a straight line: it knows its two end points, how many lanes it has in each direction,
/// and which vehicles are currently on it. It owns no knowledge of how it is drawn and no knowledge
/// of the wider network: questions such as "where can I go from here" are answered against the
/// intersection that is passed in.
/// </para>
/// <para>
/// Lanes are numbered from <c>1</c> to <see cref="LanesPerDirection"/>, counted from the
/// <em>right</em> hand side of the direction of travel. Lane <c>1</c> is therefore the rightmost
/// lane both for a vehicle moving forward and for a vehicle moving backward, which is what makes
/// the turn rules lane based rather than direction based.
/// </para>
/// </remarks>
public sealed class Road
{
    private readonly List<Vehicle> _vehicles = [];

    private double _speedLimitKph = SimulationConstants.DefaultSpeedLimitKph;
    private double _quality = SimulationConstants.DefaultRoadQuality;

    /// <summary>Creates a road between two intersections.</summary>
    /// <param name="start">Intersection where the road begins.</param>
    /// <param name="end">Intersection where the road ends.</param>
    /// <remarks>
    /// The road is not registered with the intersections by this constructor; call
    /// <see cref="City.AddRoad"/> to put it on the map. A prototype road that is only being previewed
    /// by the map editor is never registered at all.
    /// </remarks>
    public Road(Intersection start, Intersection end)
    {
        StartIntersection = start;
        EndIntersection = end;
    }

    /// <summary>Display name of the road, shown on the map and in the settings window.</summary>
    public string? Name { get; set; }

    /// <summary>Intersection where the road begins.</summary>
    /// <remarks>
    /// Settable so that the map editor can move the free end of a road while the user drags it.
    /// A road that is on the map must be re-registered through <see cref="City.AddRoad"/> after its
    /// end points changed, which is what <see cref="Simulation.CityEditor"/> does.
    /// </remarks>
    public Intersection StartIntersection { get; set; } = null!;

    /// <summary>Intersection where the road ends.</summary>
    /// <remarks>See <see cref="StartIntersection"/> for why this is settable.</remarks>
    public Intersection EndIntersection { get; set; } = null!;

    /// <summary>How the road may be used by traffic.</summary>
    /// <remarks>
    /// Changing the type changes the topology of the network: a road that becomes one-way stops
    /// being usable in one of the two directions, so vehicles travelling the wrong way have to be
    /// removed. That bookkeeping lives in <see cref="City.SetRoadType"/>, which must be used instead
    /// of assigning this property directly.
    /// </remarks>
    public RoadType Type { get; set; } = RoadType.TwoWay;

    /// <summary>Number of lanes available in each direction of travel.</summary>
    /// <remarks>
    /// Assigning a value does not move the vehicles that are already on the road; use
    /// <see cref="City.SetLanesPerDirection"/> for that.
    /// </remarks>
    public int LanesPerDirection { get; set; } = SimulationConstants.MinLanesPerDirection;

    /// <summary>Vehicles currently driving on this road.</summary>
    public IReadOnlyList<Vehicle> Vehicles => _vehicles;

    /// <summary>
    /// Speed limit of this road in km/h, clamped to the range accepted by the application.
    /// </summary>
    public double SpeedLimitKph
    {
        get => _speedLimitKph;
        set => _speedLimitKph = Math.Clamp(value, SimulationConstants.MinSpeedLimitKph, SimulationConstants.MaxSpeedLimitKph);
    }

    /// <summary>
    /// Quality of the pavement in the range <c>[0.05, 1]</c>, where <c>1</c> is a brand new road.
    /// </summary>
    /// <remarks>
    /// Quality limits the achievable speed and increases the probability of an accident.
    /// </remarks>
    public double Quality
    {
        get => _quality;
        set => _quality = Math.Clamp(value, SimulationConstants.MinRoadQuality, SimulationConstants.MaxRoadQuality);
    }

    /// <summary>Length of the road in metres.</summary>
    public double Length => StartIntersection.Position.DistanceTo(EndIntersection.Position);

    /// <summary>Direction from <see cref="StartIntersection"/> to <see cref="EndIntersection"/>, in radians.</summary>
    public double Angle => Math.Atan2(
        EndIntersection.Y - StartIntersection.Y,
        EndIntersection.X - StartIntersection.X);

    /// <summary>Total width of the paved surface, in metres.</summary>
    /// <remarks>
    /// Both directions are measured together for a two-way road; a one-way road is only as wide as
    /// the lanes it actually has.
    /// </remarks>
    public double Width => Type == RoadType.OneWay
        ? LanesPerDirection * SimulationConstants.LaneWidthMetres
        : LanesPerDirection * SimulationConstants.LaneWidthMetres * 2;

    /// <summary>The line segment this road occupies in space.</summary>
    public LineSegment Segment => new(StartIntersection.Position, EndIntersection.Position);

    /// <summary>
    /// Upper bound of the number of vehicles this road can physically hold.
    /// </summary>
    /// <remarks>
    /// A rough packing estimate that assumes every vehicle needs two vehicle lengths of road, in
    /// every lane of every direction. It exists purely to normalise the traffic-jam display.
    /// </remarks>
    public int Capacity
    {
        get
        {
            int directions = Type == RoadType.OneWay ? 1 : 2;
            double usableLength = Length * LanesPerDirection * directions;
            return Math.Max(1, (int)(usableLength / (SimulationConstants.VehicleLengthMetres * 2)));
        }
    }

    /// <summary>Density of traffic on this road, expressed as a four-level scale.</summary>
    public TrafficJamLevel TrafficJamLevel
    {
        get
        {
            double ratio = (double)_vehicles.Count / Capacity;

            return ratio switch
            {
                <= 0.25 => TrafficJamLevel.Free,
                <= 0.5 => TrafficJamLevel.Busy,
                <= 0.75 => TrafficJamLevel.Congested,
                _ => TrafficJamLevel.Jammed,
            };
        }
    }

    /// <summary>The other end point of the road, given one of them.</summary>
    /// <param name="intersection">One of the two end points.</param>
    /// <returns>The opposite end point.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="intersection"/> is not an end point.</exception>
    public Intersection GetOppositeIntersection(Intersection intersection)
    {
        if (intersection == StartIntersection)
        {
            return EndIntersection;
        }

        if (intersection == EndIntersection)
        {
            return StartIntersection;
        }

        throw new ArgumentException("The intersection is not an end point of this road.", nameof(intersection));
    }

    /// <summary>
    /// Returns the position of a vehicle along the road as an absolute point in metres.
    /// </summary>
    /// <param name="vehicle">Vehicle standing on this road.</param>
    /// <returns>The vehicle's position on the road centre line.</returns>
    public Vector2 GetPositionOnRoad(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        return StartIntersection.Position + Vector2.FromAngle(Angle, vehicle.Position);
    }

    /// <summary>
    /// Enumerates the roads reachable from this road through the given intersection, filtered by
    /// the turn a vehicle intends to make there.
    /// </summary>
    /// <param name="intersection">One of the two end points of this road.</param>
    /// <param name="direction">Turn to perform at the intersection.</param>
    /// <param name="outgoingOnly">
    /// When <see langword="true"/> (the default), only roads that can actually be entered are
    /// returned. Pass <see langword="false"/> to also include roads that merely lead into the
    /// intersection; the right-of-way rule needs that wider view.
    /// </param>
    /// <returns>The matching roads, in no particular order.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="intersection"/> is not an end point.</exception>
    public IReadOnlyList<Road> GetRoadsInDirection(
        Intersection intersection,
        RoadDirection direction,
        bool outgoingOnly = true)
    {
        ArgumentNullException.ThrowIfNull(intersection);

        if (intersection != StartIntersection && intersection != EndIntersection)
        {
            throw new ArgumentException("The intersection is not an end point of this road.", nameof(intersection));
        }

        // Direction in which a vehicle travelling along this road leaves the intersection. A
        // two-way road is traversed from either end, so the reference direction depends on which
        // end is being approached: a vehicle arriving at the end of the road was heading along the
        // road, while one arriving at the start was heading against it.
        double referenceAngle = intersection == EndIntersection
            ? Angle
            : Angle + Math.PI;

        List<Road> candidates = [.. intersection.OutgoingRoads, .. intersection.IncomingRoads];
        List<Road> result = [];

        foreach (Road candidate in candidates.Distinct())
        {
            bool canBeEntered = intersection.OutgoingRoads.Contains(candidate);

            // The turn is measured geometrically, so a road that can only be used in the opposite
            // direction still lines up with the direction it physically points; the caller decides
            // whether such a road may actually be taken.
            if (outgoingOnly && !canBeEntered)
            {
                continue;
            }

            if (candidate == this)
            {
                continue;
            }

            // Direction in which the candidate road leads away from the junction. A road that only
            // leads into the junction runs in the opposite direction, so its angle has to be flipped.
            double candidateAngle = candidate.StartIntersection == intersection
                ? candidate.Angle
                : candidate.Angle + Math.PI;

            double turn = Angles.Difference(referenceAngle, candidateAngle);
            double tolerance = SimulationConstants.AngleToleranceRadians;

            bool matches = direction switch
            {
                // The Y axis points down and angles grow clockwise, so a driver heading east has the
                // southern branch on the right hand: a positive turn is a turn to the driver's right
                // and a negative turn is a turn to the driver's left.
                RoadDirection.Left => turn < -tolerance,
                RoadDirection.Forward => Math.Abs(turn) <= tolerance,
                RoadDirection.Right => turn > tolerance,
                _ => false,
            };

            if (matches)
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns the lanes of this road that a vehicle may move into when it enters the road, that is
    /// the lanes with enough clear space beyond the entry point.
    /// </summary>
    /// <param name="enteringVehicle">Vehicle that is about to enter this road.</param>
    /// <returns>The numbers of the free lanes; the list is empty when the road is blocked.</returns>
    /// <remarks>
    /// <para>
    /// Only the space <em>ahead</em> of the entry point matters. A vehicle that is already behind it
    /// has passed the point and must not be treated as an obstacle: counting it as one used to wedge a
    /// whole queue of vehicles at the end of a road, each waiting for the others to move, so the
    /// traffic never recovered.
    /// </para>
    /// <para>
    /// The clearance demanded is the vehicle's own length plus its stopping distance, so that an
    /// entering vehicle does not come to a halt overlapping the car that is already there.
    /// </para>
    /// </remarks>
    public IReadOnlyList<int> GetFreeLanes(Vehicle enteringVehicle)
    {
        ArgumentNullException.ThrowIfNull(enteringVehicle);

        // Where along the road the vehicle will come in, measured from the start of the road.
        double entryPoint = GetEntryPosition(enteringVehicle);

        // Which end the vehicle comes in through decides which way "further along the road" is: it
        // always drives away from the junction it entered through.
        bool drivesAwayFromStart = WillEnterDrivingAwayFromStart(enteringVehicle);
        double clearance = enteringVehicle.StopDistance + SimulationConstants.VehicleLengthMetres;

        List<int> freeLanes = [];
        for (int lane = 1; lane <= LanesPerDirection; lane++)
        {
            bool blocked = _vehicles.Any(vehicle =>
                vehicle != enteringVehicle &&
                vehicle.LaneNumber == lane &&
                !vehicle.IsDisabled &&
                IsInsideClearance(vehicle.Position, drivesAwayFromStart, entryPoint, clearance));

            if (!blocked)
            {
                freeLanes.Add(lane);
            }
        }

        return freeLanes;
    }

    /// <summary>
    /// Determines whether a vehicle entering this road will drive away from the start of the road.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A one-way road can only be entered from its start. A two-way road can be entered from either
    /// end, and which end that is follows from how the vehicle is travelling: a vehicle that drives
    /// forward along the road it is leaving arrives at the far junction and therefore drives forward
    /// along this road as well.
    /// </para>
    /// <para>
    /// The vehicle's own <see cref="Vehicle.NextIntersection"/> cannot be used here, because the
    /// vehicle changes that field while it crosses the junction; deriving the direction from the
    /// vehicle's travel direction keeps the answer independent of that bookkeeping.
    /// </para>
    /// </remarks>
    /// <param name="enteringVehicle">Vehicle that is about to enter this road.</param>
    /// <returns><see langword="true"/> when the vehicle will drive away from the start junction.</returns>
    private bool WillEnterDrivingAwayFromStart(Vehicle enteringVehicle)
    {
        if (Type == RoadType.OneWay)
        {
            // A one-way road can only be driven from its start, so that is the only way in.
            return true;
        }

        if (ReferenceEquals(enteringVehicle.CurrentRoad, this))
        {
            // A U-turn at the end of the road: keep driving the way the vehicle already faces.
            return enteringVehicle.IsMovingForward;
        }

        // Driving forward along the current road means arriving at its end junction, which is how a
        // vehicle that drives forward continues to drive forward.
        return enteringVehicle.IsMovingForward;
    }

    /// <summary>
    /// Returns the position, measured from the start of the road, at which a vehicle comes in.
    /// </summary>
    /// <param name="enteringVehicle">Vehicle that is about to enter this road.</param>
    /// <returns>The entry position in metres.</returns>
    private double GetEntryPosition(Vehicle enteringVehicle)
    {
        if (ReferenceEquals(enteringVehicle.CurrentRoad, this))
        {
            // A U-turn at the end of the road: the vehicle is already on the road it is entering, so
            // it comes in exactly where it stands.
            return Math.Clamp(enteringVehicle.Position, 0, Length);
        }

        return WillEnterDrivingAwayFromStart(enteringVehicle) ? 0 : Length;
    }

    /// <summary>
    /// Determines whether a vehicle standing at <paramref name="position"/> occupies the clear space
    /// an entering vehicle needs.
    /// </summary>
    /// <param name="position">Position of the vehicle already on the road, in metres.</param>
    /// <param name="drivesAwayFromStart">Whether the entering vehicle drives away from the start.</param>
    /// <param name="entryPoint">Where along the road the entry happens, in metres.</param>
    /// <param name="clearance">Length of the clear space required, in metres.</param>
    /// <returns><see langword="true"/> when the space is occupied.</returns>
    private static bool IsInsideClearance(
        double position,
        bool drivesAwayFromStart,
        double entryPoint,
        double clearance)
        => drivesAwayFromStart
            ? position >= entryPoint && position - entryPoint < clearance
            : position <= entryPoint && entryPoint - position < clearance;

    /// <summary>Determines whether this road crosses another road in space.</summary>
    /// <param name="other">The road to test against.</param>
    /// <returns><see langword="true"/> when the two segments intersect.</returns>
    public bool Intersects(Road other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Segment.Intersects(other.Segment);
    }

    /// <summary>Adds a vehicle to this road.</summary>
    /// <param name="vehicle">Vehicle to add.</param>
    internal void AddVehicle(Vehicle vehicle) => _vehicles.Add(vehicle);

    /// <summary>Removes a vehicle from this road.</summary>
    /// <param name="vehicle">Vehicle to remove.</param>
    /// <returns><see langword="true"/> when the vehicle was on this road.</returns>
    internal bool RemoveVehicle(Vehicle vehicle) => _vehicles.Remove(vehicle);

    /// <summary>Removes every vehicle from this road, used when the road is removed or the city is reset.</summary>
    internal void ClearVehicles() => _vehicles.Clear();


}
