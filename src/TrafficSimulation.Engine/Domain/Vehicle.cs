using TrafficSimulation.Engine.Geometry;
using TrafficSimulation.Engine.Randomness;
using TrafficSimulation.Engine.Simulation;

namespace TrafficSimulation.Engine.Domain;

/// <summary>Reason why a vehicle cannot move.</summary>
public enum VehicleDisabledReason
{
    /// <summary>The vehicle is driving normally.</summary>
    None,

    /// <summary>The vehicle has been involved in an accident.</summary>
    CarAccident,

    /// <summary>The vehicle has broken down.</summary>
    Breakdown,
}

/// <summary>
/// A car driving through the city.
/// </summary>
/// <remarks>
/// <para>
/// The vehicle is a small autonomous agent. Every tick it is handed a <see cref="SimulationContext"/>
/// describing the world, and it decides on its own whether to keep going or to stop. The order of
/// the checks is the heart of the simulation and mirrors real driving priorities:
/// </para>
/// <list type="number">
/// <item>come to a halt if the car is already broken down or crashed;</item>
/// <item>stop at a red or yellow light before the stop line;</item>
/// <item>stop for the vehicle in front, and try to overtake it when it is stuck;</item>
/// <item>give way to traffic coming from the right at an uncontrolled junction;</item>
/// <item>stop when the road ahead is full;</item>
/// <item>stop at a dead end and turn around.</item>
/// </list>
/// <para>
/// Only if none of these applies does the car accelerate and move. The vehicle never asks the
/// city to do anything and never touches the traffic lights; it only reads them.
/// </para>
/// <para>
/// A vehicle lives on exactly one <see cref="Road"/> at a time and is registered with it. The
/// position is measured in metres from the <see cref="Road.StartIntersection"/> along the centre
/// line, so it is independent of the current zoom level.
/// </para>
/// </remarks>
public sealed class Vehicle
{
    private int _remainingDisabledTicks;
    private double _carAccidentRiskBonusPerTick;
    private double _rainAccidentRiskPerTick = 0.005;
    private int _accidentDurationTicks = (int)(30 / SimulationConstants.TickSeconds);
    private int _breakdownDurationTicks = (int)(30 / SimulationConstants.TickSeconds);
    private double _laneChangeChancePerTick = 0.05;
    private double _accelerationKphPerTick = 1;
    private double _tickSeconds = SimulationConstants.TickSeconds;
    private IRandomSource _random = new SystemRandomSource();

    /// <summary>Creates a vehicle without placing it on the map.</summary>
    /// <remarks>
    /// The created instance is not attached to any road. Use <see cref="City.CreateVehicle()"/> to
    /// spawn a vehicle in the city, or <see cref="City.AddVehicle"/> to place a configured one.
    /// </remarks>
    public Vehicle()
    {
    }

    /// <summary>Current speed in km/h.</summary>
    public double SpeedKph { get; private set; }

    /// <summary>
    /// Speed the vehicle gains each tick, in km/h per tick. One km/h per tick equals roughly
    /// 2.8 m/s² at the default tick length.
    /// </summary>
    public double AccelerationKphPerTick
    {
        get => _accelerationKphPerTick;
        set => _accelerationKphPerTick = Math.Max(0, value);
    }

    /// <summary>Road the vehicle is currently driving on.</summary>
    public Road CurrentRoad { get; internal set; } = null!;

    /// <summary>Number of the lane the vehicle occupies, counted from the right, starting at 1.</summary>
    public int LaneNumber { get; internal set; } = 1;

    /// <summary>Distance of the vehicle from <see cref="Road.StartIntersection"/>, in metres.</summary>
    public double Position { get; internal set; }

    /// <summary>Intersection the vehicle is currently heading towards.</summary>
    public Intersection NextIntersection { get; internal set; } = null!;

    /// <summary>
    /// Road the vehicle intends to take after the current one, or <see langword="null"/> when there
    /// is nowhere to go.
    /// </summary>
    public Road? NextRoad { get; internal set; }

    /// <summary>
    /// Junction the vehicle is driving towards and will leave its current road through.
    /// </summary>
    /// <remarks>
    /// This is <em>not</em> the same as <see cref="NextIntersection"/>: that one is the junction the
    /// vehicle will cross when it moves onto <see cref="NextRoad"/>. Everything that depends on the
    /// stop line, the traffic light or the right of way has to ask for the junction being approached,
    /// otherwise the rules are evaluated against the wrong crossing.
    /// </remarks>
    public Intersection ApproachedJunction
        => IsMovingForward ? CurrentRoad.EndIntersection : CurrentRoad.StartIntersection;

    /// <summary>
    /// Determines whether this vehicle is heading towards a given junction.
    /// </summary>
    /// <param name="junction">Junction to compare against.</param>
    /// <returns><see langword="true"/> when the vehicle is about to cross it.</returns>
    public bool IsApproaching(Intersection junction) => ApproachedJunction == junction;

    /// <summary>
    /// Whether the vehicle travels from <see cref="Road.StartIntersection"/> towards
    /// <see cref="Road.EndIntersection"/>. Always <see langword="true"/> on a one-way road.
    /// </summary>
    public bool IsMovingForward { get; internal set; } = true;

    /// <summary>Why the vehicle is currently unable to move.</summary>
    public VehicleDisabledReason DisabledReason { get; private set; } = VehicleDisabledReason.None;

    /// <summary>Whether the vehicle stands still because of an accident or a breakdown.</summary>
    public bool IsDisabled => DisabledReason != VehicleDisabledReason.None;

    /// <summary>Whether the vehicle stands still because of an accident.</summary>
    public bool IsInCarAccident => DisabledReason == VehicleDisabledReason.CarAccident;

    /// <summary>Whether the vehicle stands still because of a breakdown.</summary>
    public bool HasBrokenDown => DisabledReason == VehicleDisabledReason.Breakdown;

    /// <summary>Direction of travel expressed as a road direction.</summary>
    public RoadTravelDirection TravelDirection
        => IsMovingForward ? RoadTravelDirection.Forward : RoadTravelDirection.Backward;

    /// <summary>
    /// Distance from the junction at which the vehicle has to stop, in metres.
    /// </summary>
    /// <remarks>
    /// Half of the widest road of the junction, plus half of the vehicle, so that the front bumper
    /// stops right at the edge of the crossing area.
    /// </remarks>
    public double StopDistance
        => ApproachedJunction.HalfWidth + (SimulationConstants.VehicleLengthMetres / 2);

    /// <summary>Speed of the vehicle converted to metres per second.</summary>
    public double SpeedMetresPerSecond => SpeedKph / 3.6;

    /// <summary>Distance covered by the vehicle during one tick, in metres.</summary>
    public double DistancePerTick => SpeedMetresPerSecond * _tickSeconds;

    /// <summary>Speed the vehicle tries to reach, in km/h.</summary>
    public double SpeedLimitKph => CurrentRoad.SpeedLimitKph;

    /// <summary>Layout direction of the vehicle on its road, in radians.</summary>
    public double HeadingRadians
        => IsMovingForward ? CurrentRoad.Angle : CurrentRoad.Angle + Math.PI;

    /// <summary>
    /// Additional accident probability per tick caused by poor pavement quality, in <c>[0, 1]</c>.
    /// </summary>
    public double CarAccidentRiskBonusPerTick => _carAccidentRiskBonusPerTick;

    /// <summary>Number of ticks left before a disabled vehicle becomes mobile again.</summary>
    public int RemainingDisabledTicks => _remainingDisabledTicks;

    /// <summary>
    /// Places a vehicle at a random position on a random road of the city.
    /// </summary>
    /// <param name="city">City to place the vehicle in.</param>
    /// <param name="random">Source of randomness used to pick the road, direction and lane.</param>
    /// <returns>
    /// The spawned vehicle, or <see langword="null"/> when the city has no roads yet.
    /// </returns>
    public static Vehicle? Spawn(City city, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(random);

        if (city.Roads.Count == 0)
        {
            return null;
        }

        Road road = city.Roads[random.NextInt(city.Roads.Count)];

        // A one-way road can only be driven in one direction; a two-way road in either.
        bool isMovingForward = road.Type == RoadType.OneWay || random.NextInt(2) == 0;
        int lane = random.NextRange(SimulationConstants.MinLanesPerDirection, road.LanesPerDirection + 1);

        Vehicle vehicle = new()
        {
            CurrentRoad = road,
            LaneNumber = lane,
            IsMovingForward = isMovingForward,
            Position = isMovingForward ? 0 : road.Length,
            NextIntersection = isMovingForward ? road.EndIntersection : road.StartIntersection,
        };

        vehicle.ApplySettings(city.Settings.ToVehicleSettings(), random);
        vehicle.RefreshNextRoad();

        return vehicle;
    }

    /// <summary>Applies the behavioural settings of the city to this vehicle.</summary>
    /// <param name="settings">Settings to apply.</param>
    /// <param name="random">
    /// Source of randomness this vehicle uses for the decisions it makes on its own. Pass the same
    /// source the city uses to keep a run reproducible.
    /// </param>
    public void ApplySettings(VehicleBehaviourSettings settings, IRandomSource? random = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _tickSeconds = settings.TickSeconds;
        _accelerationKphPerTick = settings.AccelerationKphPerTick;
        _laneChangeChancePerTick = settings.LaneChangeChancePerTick;
        _carAccidentRiskBonusPerTick = settings.AccidentChancePerTick;
        _rainAccidentRiskPerTick = settings.RainAccidentChancePerTick;
        _accidentDurationTicks = settings.AccidentDurationTicks;
        _breakdownDurationTicks = settings.BreakdownDurationTicks;

        if (random is not null)
        {
            _random = random;
        }
    }

    /// <summary>Creates a vehicle placed at an exact position on a road.</summary>
    /// <param name="road">Road the vehicle is placed on.</param>
    /// <param name="position">Distance from the start of the road, in metres.</param>
    /// <param name="lane">Lane number, counted from the right; defaults to the rightmost lane.</param>
    /// <param name="isMovingForward">Direction of travel; defaults to the direction of the road.</param>
    /// <param name="speedKph">Initial speed in km/h.</param>
    /// <returns>A vehicle that is ready to be handed to <see cref="City.AddVehicle"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="road"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This is the explicit counterpart of <see cref="Spawn"/>: instead of picking a random road, the
    /// caller decides exactly where the vehicle starts. The direction is forced to forward on a
    /// one-way road, because driving the wrong way is impossible there.
    /// </remarks>
    public static Vehicle Place(
        Road road,
        double position,
        int lane = SimulationConstants.MinLanesPerDirection,
        bool isMovingForward = true,
        double speedKph = 0)
    {
        ArgumentNullException.ThrowIfNull(road);

        bool forward = road.Type == RoadType.OneWay || isMovingForward;

        return new Vehicle
        {
            CurrentRoad = road,
            LaneNumber = Math.Clamp(
                lane,
                SimulationConstants.MinLanesPerDirection,
                Math.Max(SimulationConstants.MinLanesPerDirection, road.LanesPerDirection)),
            IsMovingForward = forward,
            Position = position,
            NextIntersection = forward ? road.EndIntersection : road.StartIntersection,
            SpeedKph = Math.Max(0, speedKph),
        };
    }

    /// <summary>
    /// Probability that this vehicle is disabled during the current tick.
    /// </summary>
    /// <param name="context">State of the world for the current tick.</param>
    /// <returns>The combined accident and breakdown probability, in <c>[0, 1]</c>.</returns>
    public double GetDisabledRisk(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A bad surface roughly doubles the risk; rain adds a flat bonus on top of it.
        double accidentRisk = _carAccidentRiskBonusPerTick * (1 + (1 - CurrentRoad.Quality));
        accidentRisk += context.IsRaining ? _rainAccidentRiskPerTick : 0;

        return accidentRisk + context.Settings.BreakdownChancePerTick;
    }

    /// <summary>Advances the vehicle by one tick.</summary>
    /// <param name="context">State of the world for this tick.</param>
    public void Update(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (IsDisabled)
        {
            _remainingDisabledTicks--;
            if (_remainingDisabledTicks <= 0)
            {
                DisabledReason = VehicleDisabledReason.None;
                _remainingDisabledTicks = 0;
            }

            return;
        }

        // A vehicle is only allowed to break down where it can come to a halt, otherwise it would
        // block the middle of a junction. The risk is drawn first so that the number of random
        // calls per tick does not depend on the position of the vehicle.
        double disabledRisk = GetDisabledRisk(context);
        bool canStillPullOver = !IsAboutToLeaveRoad();
        if (context.Random.NextDouble() <= disabledRisk && canStillPullOver)
        {
            Disable(context);
            return;
        }

        // Every stopping rule is evaluated against the junction the vehicle is approaching, so a
        // vehicle far from any junction simply has no reason to stop. Note that there is deliberately
        // no shortcut based on the distance to the stop line here: a car following another car has to
        // be able to stop anywhere along the road, not only in front of a junction.
        if (ShouldStopForThisTick(context))
        {
            SpeedKph = 0;
            return;
        }

        Accelerate(context);

        // Moving has to be the last thing that happens, otherwise a car could be left standing past
        // the end of its road with its tick already over, and it would only be put back on the map
        // on the following tick.
        double positionBeforeMove = Position;
        MoveBy(DistancePerTick);

        // Acceleration happens after the stopping rules were evaluated, so the distance actually
        // covered can exceed the one they predicted. When there is nowhere to go the move is undone,
        // which keeps the invariant "a vehicle is always on its own road" true; a vehicle that does
        // have a road to enter is left to cross the junction in the usual way.
        if (NextRoad is null && HasLeftRoad())
        {
            Position = Math.Clamp(positionBeforeMove, 0, CurrentRoad.Length);
        }

        if (context.Random.NextDouble() <= _laneChangeChancePerTick)
        {
            TryChangeLane();
        }

        if (HasLeftRoad())
        {
            EnterNextRoad(context);
        }

        // The tick is over, so the vehicle has to be somewhere it can actually be: on its road. It
        // may have crossed the junction and been moved onto the next road, in which case this does
        // nothing; it may also have been unable to enter anywhere, in which case it is put back on
        // the stretch of road it came from.
        Position = Math.Clamp(Position, 0, CurrentRoad.Length);
    }

    /// <summary>
    /// Recomputes <see cref="NextRoad"/> from the current position, lane and traffic light state.
    /// </summary>
    /// <remarks>
    /// Called whenever something changes that could invalidate the previously chosen road: the
    /// vehicle changed lane, its road changed type, the map was edited around it.
    /// </remarks>
    public void RefreshNextRoad()
    {
        if (CurrentRoad is null || NextIntersection is null)
        {
            NextRoad = null;
            return;
        }

        NextRoad = ChooseNextRoad();
    }

    /// <summary>Total distance this vehicle has still to cover before the junction, in metres.</summary>
    /// <returns>The distance to the stop line as a positive number.</returns>
    public double DistanceToStopLine()
    {
        double junctionPosition = IsMovingForward ? CurrentRoad.Length : 0;
        return Math.Abs(junctionPosition - Position) - StopDistance;
    }

    /// <summary>Determines whether the vehicle would overshoot the junction during this tick.</summary>
    /// <returns><see langword="true"/> when the stop line is reached within one tick.</returns>
    public bool IsAboutToLeaveRoad()
    {
        double nextPosition = IsMovingForward ? Position + DistancePerTick : Position - DistancePerTick;
        double stopLine = IsMovingForward
            ? CurrentRoad.Length - StopDistance
            : StopDistance;

        return IsMovingForward ? nextPosition >= stopLine : nextPosition <= stopLine;
    }

    /// <summary>Determines whether the vehicle has driven past the end of its road.</summary>
    /// <returns><see langword="true"/> when the vehicle is off the road.</returns>
    public bool HasLeftRoad()
        => Position >= CurrentRoad.Length || Position <= 0;

    /// <summary>
    /// Determines whether the vehicle has to stand still for the current tick.
    /// </summary>
    /// <param name="context">State of the world for this tick.</param>
    /// <returns><see langword="true"/> when any stopping rule applies.</returns>
    public bool ShouldStopForThisTick(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return ShouldStopForTrafficLight(context)
               || ShouldStopForVehicleAhead(context)
               || ShouldYieldToVehicleOnRight(context)
               || IsNextRoadBlocked()
               || IsAtDeadEnd();
    }

    /// <summary>
    /// Determines whether the light controlling this vehicle's road at the junction ahead is red or
    /// yellow and the stop line is within reach.
    /// </summary>
    /// <param name="context">State of the world for this tick.</param>
    /// <returns><see langword="true"/> when the vehicle must stop for the light.</returns>
    public bool ShouldStopForTrafficLight(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        TrafficLight? light = context.TrafficLights.Find(CurrentRoad, ApproachedJunction);
        if (light is null || light.State == TrafficLightState.Green)
        {
            return false;
        }

        // The rule must not depend on how far the vehicle would travel this tick: a car that has
        // already stopped travels nowhere, so a distance based test would stop applying and let it
        // creep across the stop line a few centimetres at a time.
        return !HasCrossedStopLine();
    }

    /// <summary>
    /// Determines whether the vehicle has already passed the point at which it must stop before a
    /// junction.
    /// </summary>
    /// <returns><see langword="true"/> once the front of the vehicle is at or past the stop line.</returns>
    public bool HasCrossedStopLine()
    {
        // The stop line sits half a junction short of the junction centre.
        double stopLine = IsMovingForward
            ? CurrentRoad.Length - ApproachedJunction.HalfWidth
            : ApproachedJunction.HalfWidth;

        // A small tolerance lets a vehicle come to rest right at the stop line instead of inching
        // towards it a tick at a time. Half a metre is far less than a junction is wide, so the
        // vehicle still stops well before the crossing area.
        const double stopLineToleranceMetres = 0.5;

        return IsMovingForward
            ? Position >= stopLine - stopLineToleranceMetres
            : Position <= stopLine + stopLineToleranceMetres;
    }

    /// <summary>
    /// Determines whether another vehicle is too close ahead in the same lane.
    /// </summary>
    /// <param name="context">State of the world for this tick.</param>
    /// <returns><see langword="true"/> when the vehicle must stop or overtake.</returns>
    public bool ShouldStopForVehicleAhead(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Vehicle? ahead = GetVehicleAhead();
        if (ahead is null)
        {
            return false;
        }

        double gap = Math.Abs(ahead.Position - Position);
        if (gap >= SimulationConstants.SafetyDistanceMetres)
        {
            return false;
        }

        // A vehicle stuck in front is an obstacle: pull out around it if there is room, otherwise wait.
        if (ahead.IsDisabled)
        {
            return !TryChangeLane();
        }

        return true;
    }

    /// <summary>
    /// Determines whether a vehicle approaching from the right has priority at the junction.
    /// </summary>
    /// <param name="context">State of the world for this tick.</param>
    /// <returns><see langword="true"/> when the vehicle has to give way.</returns>
    public bool ShouldYieldToVehicleOnRight(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!IsAboutToLeaveRoad())
        {
            return false;
        }

        foreach (Vehicle other in GetVehiclesEnteringFromRight(context))
        {
            if (other.IsDisabled)
            {
                continue;
            }

            // Vehicles that are held by a red light are not a threat.
            TrafficLight? otherLight = context.TrafficLights.Find(other.CurrentRoad, other.ApproachedJunction);
            if (otherLight is not null && otherLight.State != TrafficLightState.Green)
            {
                continue;
            }

            // Only traffic that is actually arriving at the junction is a threat. DistanceToStopLine
            // already measures from the car to the stop line of the junction it is approaching, so
            // the check does not have to reason about which end of its road the car is coming from.
            if (other.DistanceToStopLine() <= SimulationConstants.IntersectionApproachMetres)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether the road this vehicle wants to enter has no room for it.</summary>
    /// <returns><see langword="true"/> when the next road is completely blocked.</returns>
    public bool IsNextRoadBlocked()
        => NextRoad is not null && IsAboutToLeaveRoad() && NextRoad.GetFreeLanes(this).Count == 0;

    /// <summary>
    /// Determines whether the vehicle has reached a dead end and therefore has to stop.
    /// </summary>
    /// <remarks>
    /// Before giving up, the vehicle tries once more from the other end of the lane list: at a dead
    /// end the only way out may be a U-turn, which is legal on a two-way road.
    /// </remarks>
    /// <returns><see langword="true"/> when there is no road to continue on.</returns>
    public bool IsAtDeadEnd()
    {
        if (NextRoad is not null || !IsAboutToLeaveRoad())
        {
            return false;
        }

        int originalLane = LaneNumber;
        LaneNumber = SimulationConstants.MinLanesPerDirection;
        NextRoad = ChooseNextRoad();

        if (NextRoad is null)
        {
            LaneNumber = CurrentRoad.LanesPerDirection;
            NextRoad = ChooseNextRoad();
        }

        if (NextRoad is null)
        {
            LaneNumber = originalLane;
            return true;
        }

        return false;
    }

    /// <summary>Returns the closest vehicle ahead in the same lane and the same direction.</summary>
    /// <returns>The vehicle ahead, or <see langword="null"/> when the lane is clear.</returns>
    public Vehicle? GetVehicleAhead()
    {
        Vehicle? closest = null;
        double closestGap = double.MaxValue;

        foreach (Vehicle other in CurrentRoad.Vehicles)
        {
            if (other == this || other.LaneNumber != LaneNumber || other.IsMovingForward != IsMovingForward)
            {
                continue;
            }

            double gap = IsMovingForward ? other.Position - Position : Position - other.Position;
            if (gap > 0 && gap < closestGap)
            {
                closestGap = gap;
                closest = other;
            }
        }

        return closest;
    }

    /// <summary>Returns the vehicles that are about to enter the same junction from the right.</summary>
    /// <param name="context">State of the world for this tick.</param>
    /// <returns>The vehicles that have priority over this one.</returns>
    public IEnumerable<Vehicle> GetVehiclesEnteringFromRight(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return CurrentRoad
            .GetRoadsInDirection(NextIntersection, RoadDirection.Right, outgoingOnly: false)
            .SelectMany(road => road.Vehicles)
            .Where(other => other != this && other.ApproachedJunction == ApproachedJunction);
    }

    /// <summary>
    /// Moves the vehicle into an adjacent lane when there is enough room.
    /// </summary>
    /// <returns><see langword="true"/> when the lane was changed.</returns>
    public bool TryChangeLane()
    {
        if (CurrentRoad.LanesPerDirection <= 1)
        {
            return false;
        }

        // Prefer pulling out to the left, which is what a driver would do on a two-way road.
        int leftLane = Math.Max(LaneNumber - 1, SimulationConstants.MinLanesPerDirection);
        if (leftLane != LaneNumber && IsLaneFree(leftLane))
        {
            LaneNumber = leftLane;
            RefreshNextRoad();
            return true;
        }

        int rightLane = Math.Min(LaneNumber + 1, CurrentRoad.LanesPerDirection);
        if (rightLane != LaneNumber && IsLaneFree(rightLane))
        {
            LaneNumber = rightLane;
            RefreshNextRoad();
            return true;
        }

        return false;
    }

    /// <summary>Increases the speed of the vehicle, respecting the road and the weather.</summary>
    /// <param name="context">State of the world for this tick.</param>
    public void Accelerate(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        SpeedKph += _accelerationKphPerTick;

        // Poor pavement and rain both reduce the speed the driver is willing to reach.
        double allowed = CurrentRoad.Quality * SpeedLimitKph;
        if (context.IsRaining)
        {
            allowed -= SimulationConstants.RainSpeedPenaltyKph;
        }

        SpeedKph = Math.Max(0, Math.Min(SpeedKph, allowed));
    }

    /// <summary>Instantly stops the vehicle and marks it as disabled.</summary>
    /// <param name="context">State of the world for this tick.</param>
    public void Disable(SimulationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool isAccident = context.Random.NextDouble() < 0.5;
        DisabledReason = isAccident ? VehicleDisabledReason.CarAccident : VehicleDisabledReason.Breakdown;
        _remainingDisabledTicks = isAccident ? _accidentDurationTicks : _breakdownDurationTicks;
        SpeedKph = 0;
    }

    /// <summary>Clears an accident or a breakdown, making the vehicle mobile again.</summary>
    public void Repair()
    {
        DisabledReason = VehicleDisabledReason.None;
        _remainingDisabledTicks = 0;
    }

    /// <summary>Moves the vehicle along its road by the given distance.</summary>
    /// <param name="distance">Distance to travel, in metres; applies in the current direction.</param>
    public void MoveBy(double distance)
        => Position += IsMovingForward ? distance : -distance;

    /// <summary>Chooses a road to take at <see cref="NextIntersection"/>.</summary>
    /// <returns>
    /// The chosen road, or <see langword="null"/> when the junction has no road this vehicle can
    /// enter right now.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Which turns are available depends on the lane: the rightmost lane may only go straight on or
    /// turn right, the leftmost lane may go straight on, turn left or make a U-turn on a two-way road,
    /// and a single lane road may go anywhere.
    /// </para>
    /// <para>
    /// A road that is already full is never chosen. A vehicle that committed to a blocked road would
    /// drive up to the junction and stop there for good, and the queue behind it would never dissolve;
    /// answering <see langword="null"/> instead makes the vehicle wait at the stop line, where traffic
    /// can still flow past it. The choice is recomputed on the next tick, by which time a place may
    /// have become free.
    /// </para>
    /// </remarks>
    public Road? ChooseNextRoad()
    {
        List<Road> candidates = [];

        if (CurrentRoad.LanesPerDirection == SimulationConstants.MinLanesPerDirection)
        {
            candidates.AddRange(NextIntersection.OutgoingRoads);
        }
        else
        {
            candidates.AddRange(CurrentRoad.GetRoadsInDirection(NextIntersection, RoadDirection.Forward));

            // Lane numbering starts at the right, and the direction labels are geometric, so the
            // rightmost lane takes the right turn and the leftmost lane takes the left one.
            if (LaneNumber == SimulationConstants.MinLanesPerDirection)
            {
                candidates.AddRange(CurrentRoad.GetRoadsInDirection(NextIntersection, RoadDirection.Right));
            }
            else if (LaneNumber == CurrentRoad.LanesPerDirection)
            {
                // A U-turn belongs with the left turn, and is only legal where oncoming traffic
                // exists at all, which is what a two-way road means.
                candidates.AddRange(CurrentRoad.GetRoadsInDirection(NextIntersection, RoadDirection.Left));

                if (CurrentRoad.Type != RoadType.OneWay)
                {
                    candidates.Add(CurrentRoad);
                }
            }
        }

        // Only roads with room for this vehicle are worth considering.
        candidates = [.. candidates.Distinct().Where(road => road.GetFreeLanes(this).Count > 0)];

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[_random.NextInt(candidates.Count)];
    }

    /// <summary>Moves the vehicle onto <see cref="NextRoad"/> once it has left the current road.</summary>
    /// <param name="context">State of the world for this tick.</param>
    private void EnterNextRoad(SimulationContext context)
    {
        Road? nextRoad = NextRoad;
        if (nextRoad is null)
        {
            return;
        }

        // Crossing a road wears it out a little; the wear is worse on a wet surface.
        CurrentRoad.Quality -= context.IsRaining
            ? SimulationConstants.WetWearPerPass
            : SimulationConstants.DryWearPerPass;

        IReadOnlyList<int> freeLanes = nextRoad.GetFreeLanes(this);
        if (freeLanes.Count == 0)
        {
            // The check in IsNextRoadBlocked should prevent this, but a vehicle may also be blocked
            // by a car that entered during the very same tick, so wait rather than throw.
            Position = IsMovingForward ? CurrentRoad.Length : 0;
            SpeedKph = 0;
            return;
        }

        int targetLane = freeLanes[context.Random.NextInt(freeLanes.Count)];

        if (nextRoad == CurrentRoad)
        {
            // A U-turn: stay on the same road but swap direction and start from the other end.
            LaneNumber = targetLane;
            IsMovingForward = !IsMovingForward;
            Position = IsMovingForward ? 0 : nextRoad.Length;
        }
        else
        {
            CurrentRoad.RemoveVehicle(this);
            nextRoad.AddVehicle(this);
            CurrentRoad = nextRoad;
            LaneNumber = targetLane;
            IsMovingForward = nextRoad.StartIntersection == NextIntersection;
            Position = IsMovingForward ? 0 : nextRoad.Length;
        }

        NextIntersection = IsMovingForward ? CurrentRoad.EndIntersection : CurrentRoad.StartIntersection;
        NextRoad = ChooseNextRoad();
    }

    /// <summary>Determines whether a lane is clear enough to merge into.</summary>
    /// <param name="lane">Lane to inspect.</param>
    /// <returns><see langword="true"/> when no vehicle occupies the lane within the merge gap.</returns>
    private bool IsLaneFree(int lane)
        => !CurrentRoad.Vehicles.Any(other =>
            other != this
            && other.LaneNumber == lane
            && Math.Abs(other.Position - Position) <= SimulationConstants.LaneChangeGapMetres);
}
