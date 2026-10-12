using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;
using TrafficSimulation.Engine.Simulation;
using TrafficSimulation.Tests.Helpers;

namespace TrafficSimulation.Tests.Domain;

/// <summary>
/// Tests for <see cref="Vehicle"/>: where it is placed, how fast it may drive, how it follows the
/// car in front, how it obeys lights and right of way, when it changes lane and what it does at a
/// dead end.
/// </summary>
/// <remarks>
/// Every test builds a small, deterministic city from <see cref="TestCity"/> and
/// <see cref="RoadLayouts"/>, so a failure is caused by exactly one rule of the vehicle logic.
/// Turns are named after the <see cref="RoadDirection"/> the engine itself reports for a road, and
/// the tests spell out which physical branch that is, because the screen geometry and the driver's
/// left and right do not always line up in an obvious way.
/// </remarks>
public class VehicleTests
{
    // ---------------------------------------------------------------------------------------------
    // Placement and geometry
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Place_SelectsTheJunctionAheadAccordingToTheDirectionOfTravel()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);
        Road road = city.AddTwoWayRoad(start, end);

        Vehicle forward = Vehicle.Place(road, 10, isMovingForward: true);
        Vehicle backward = Vehicle.Place(road, 10, isMovingForward: false);

        // A forward vehicle runs towards the end of the road, a backward one back to its start:
        // the junction it must watch for the stop line is the one it will actually cross.
        Assert.Same(end, forward.NextIntersection);
        Assert.Same(start, backward.NextIntersection);
        Assert.Equal(RoadTravelDirection.Forward, forward.TravelDirection);
        Assert.Equal(RoadTravelDirection.Backward, backward.TravelDirection);
    }

    [Fact]
    public void Place_ForcesTheForwardDirectionOnAOneWayRoad()
    {
        TestCity city = new();
        Road road = city.AddOneWayRoad(city.Node(0, 0), city.Node(100, 0));

        // Driving the wrong way down a one-way street is impossible, so the requested direction is
        // overruled rather than honoured.
        Vehicle vehicle = Vehicle.Place(road, 10, isMovingForward: false);

        Assert.True(vehicle.IsMovingForward);
        Assert.Equal(RoadTravelDirection.Forward, vehicle.TravelDirection);
    }

    [Fact]
    public void Place_ClampsTheLaneToTheRoadAndTheSpeedToZero()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 2);

        // Lane 7 does not exist, so the vehicle is put down in the outermost lane that does; a
        // negative speed is not a thing, so it becomes a standstill.
        Vehicle tooFarRight = Vehicle.Place(road, 10, lane: 7);
        Vehicle tooFarLeft = Vehicle.Place(road, 10, lane: -3);
        Vehicle reversing = Vehicle.Place(road, 10, speedKph: -20);

        Assert.Equal(2, tooFarRight.LaneNumber);
        Assert.Equal(1, tooFarLeft.LaneNumber);
        Assert.Equal(0, reversing.SpeedKph, 10);
    }

    [Fact]
    public void StopDistance_IsHalfTheJunctionPlusHalfTheVehicle()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);
        Vehicle vehicle = city.PlaceVehicle(cross.West, 50);

        // The crossing is as wide as its widest road: 7.5 m for a two-way single lane street. The
        // car stops one vehicle half length before the edge of that crossing area.
        Assert.Equal((7.5 / 2) + (SimulationConstants.VehicleLengthMetres / 2), vehicle.StopDistance, 10);
    }

    [Fact]
    public void DistanceToStopLine_ShrinksAsTheVehicleAdvances()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);
        Road road = city.AddTwoWayRoad(start, end);
        Vehicle vehicle = city.PlaceVehicle(road, 50);

        double far = vehicle.DistanceToStopLine();
        vehicle = city.PlaceVehicle(road, 90);
        double near = vehicle.DistanceToStopLine();

        // The stop line sits StopDistance before the end of the road, so 50 m out leaves 100 - 50 -
        // 6 = 44 m and 90 m out only 4 m. The value stays positive, it is a distance, not an offset.
        Assert.Equal(44, far, 10);
        Assert.Equal(4, near, 10);
        Assert.True(near < far, "Driving on must bring the stop line closer.");
    }

    [Fact]
    public void IsAboutToLeaveRoad_IsFalseFarAwayAndTrueWithinOneTick()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);
        Road road = city.AddTwoWayRoad(start, end);

        // At 10 km/h a tick covers about 0.28 m, so a car at 50 m has 44 m of slack, while one at
        // 93.9 m is already inside the last 0.1 m before the stop line at 94 m.
        Vehicle far = city.PlaceVehicle(road, 50, speedKph: 10);
        Vehicle near = city.PlaceVehicle(road, 93.9, speedKph: 10);

        Assert.False(far.IsAboutToLeaveRoad());
        Assert.True(near.IsAboutToLeaveRoad());
    }

    [Fact]
    public void HasLeftRoad_IsTrueAtBothEndsOfTheRoad()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);
        Road road = city.AddTwoWayRoad(start, end);

        Vehicle forward = city.PlaceVehicle(road, 100);
        Vehicle backward = city.PlaceVehicle(road, 0, isMovingForward: false);
        Vehicle middle = city.PlaceVehicle(road, 50);

        // The ends of the road are the junctions: reaching either of them means leaving the road,
        // which is what makes the vehicle change onto its next road.
        Assert.True(forward.HasLeftRoad());
        Assert.True(backward.HasLeftRoad());
        Assert.False(middle.HasLeftRoad());
    }

    [Fact]
    public void HeadingRadians_PointsAlongTheRoadAndBackAgain()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));

        Vehicle forward = city.PlaceVehicle(road, 10);
        Vehicle backward = city.PlaceVehicle(road, 10, isMovingForward: false);

        // Heading is a drawing concern: a southbound car on the west-to-east road must be drawn
        // facing the other way, exactly half a turn apart.
        Assert.Equal(road.Angle, forward.HeadingRadians, 10);
        Assert.Equal(road.Angle + Math.PI, backward.HeadingRadians, 10);
    }

    [Fact]
    public void GetPositionOnRoad_MatchesTheDistanceFromTheRoadStart()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(10, 20), city.Node(110, 20));
        Vehicle vehicle = city.PlaceVehicle(road, 42);

        Vector2 position = road.GetPositionOnRoad(vehicle);

        // Position is measured along the centre line, so the absolute point is the road start moved
        // 42 m east, with the y coordinate of the road left alone.
        Assert.Equal(52, position.X, 10);
        Assert.Equal(20, position.Y, 10);
        Assert.Equal(42, road.Segment.Start.DistanceTo(position), 10);
    }

    // ---------------------------------------------------------------------------------------------
    // Speed
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Accelerate_RaisesSpeedByOneStepPerTickUpToTheSpeedLimit()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        road.SpeedLimitKph = 50;
        Vehicle vehicle = city.PlaceVehicle(road, 10);
        SimulationContext context = city.CreateContext();

        vehicle.Accelerate(context);
        Assert.Equal(1, vehicle.SpeedKph, 10);

        // Left alone, the car creeps up one km/h per tick but is not allowed to pass the limit of
        // the road it drives on.
        for (int tick = 0; tick < 200; tick++)
        {
            vehicle.Accelerate(context);
        }

        Assert.Equal(50, vehicle.SpeedKph, 10);
    }

    [Fact]
    public void Accelerate_AppliesTheRainSpeedPenalty()
    {
        TestCity city = new();
        city.Settings.RainChancePerTick = 0.9;
        city.Settings.AccelerationKphPerTick = 10;
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        road.SpeedLimitKph = 60;
        Vehicle vehicle = city.PlaceVehicle(road, 20, speedKph: 40);

        // A dry tick never rains, so the driver may aim for the full 60 km/h limit.
        vehicle.Accelerate(city.CreateContext());
        Assert.Equal(50, vehicle.SpeedKph, 10);

        // The next tick starts a rain period: wet asphalt costs RainSpeedPenaltyKph, so the very
        // same road only permits 45 km/h and the speed already carried is capped down to it.
        city.ThenRandomDouble(0);
        city.City.Update();

        Assert.True(city.City.IsRaining);
        Assert.Equal(45, vehicle.SpeedKph, 10);
    }

    [Fact]
    public void Accelerate_LimitsSpeedToTheQualityOfThePavement()
    {
        TestCity city = new();
        city.Settings.AccelerationKphPerTick = 10;
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        road.SpeedLimitKph = 60;
        road.Quality = 0.5;
        Vehicle vehicle = city.PlaceVehicle(road, 20, speedKph: 25);

        vehicle.Accelerate(city.CreateContext());

        // Potholes halve the speed a driver is prepared to make: 0.5 * 60 = 30 km/h, so the 10 km/h
        // step stops at 30 instead of 35.
        Assert.Equal(30, vehicle.SpeedKph, 10);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(36, 1)]
    [InlineData(72, 2)]
    public void DistancePerTick_ConvertsKphIntoMetresPerTick(double speedKph, double expectedMetres)
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(1000, 0));
        Vehicle vehicle = city.PlaceVehicle(road, 10, speedKph: speedKph);

        // km/h -> m/s is a division by 3.6 and a tick is a tenth of a second, so 36 km/h is one
        // metre per tick.
        Assert.Equal(expectedMetres, vehicle.DistancePerTick, 10);
    }

    // ---------------------------------------------------------------------------------------------
    // Car following
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GetVehicleAhead_FindsTheNearestCarOnlyWithinTheOwnLaneAndDirection()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 2);

        Vehicle subject = city.PlaceVehicle(road, 20, lane: 1);
        Vehicle nearest = city.PlaceVehicle(road, 40, lane: 1);
        city.PlaceVehicle(road, 70, lane: 1);
        city.PlaceVehicle(road, 25, lane: 2);
        city.PlaceVehicle(road, 45, lane: 1, isMovingForward: false);
        city.PlaceVehicle(road, 10, lane: 1);

        // Only the car in front matters: same lane and same direction. The one in the other lane,
        // the one coming the other way and the one behind are all irrelevant here.
        Assert.Same(nearest, subject.GetVehicleAhead());
    }

    [Fact]
    public void GetVehicleAhead_IsNullOnAClearLane()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle subject = city.PlaceVehicle(road, 20);

        // Nothing ahead means the driver is free to accelerate.
        Assert.Null(subject.GetVehicleAhead());
    }

    [Fact]
    public void ShouldStopForVehicleAhead_IsFalseWhenTheGapIsSafe()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle subject = city.PlaceVehicle(road, 20);
        city.PlaceVehicle(road, 40);

        // Twenty metres is far more than the SafetyDistanceMetres a driver keeps, so the car ahead
        // is no reason to slow down.
        Assert.False(subject.ShouldStopForVehicleAhead(city.CreateContext()));
    }

    [Fact]
    public void ShouldStopForVehicleAhead_IsTrueWhenAMovingCarIsTooClose()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle subject = city.PlaceVehicle(road, 20, speedKph: 30);
        city.PlaceVehicle(road, 21);

        // One metre is well inside the safety gap: the follower has to brake rather than drive into
        // the car in front, which is still moving and so cannot be overtaken.
        Assert.True(subject.ShouldStopForVehicleAhead(city.CreateContext()));
    }

    [Fact]
    public void ShouldStopForVehicleAhead_IsTrueForADisabledCarOnASingleLaneRoad()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 1);
        Vehicle subject = city.PlaceVehicle(road, 20, speedKph: 30);
        Vehicle obstacle = city.PlaceVehicle(road, 21);
        obstacle.Disable(city.CreateContext());

        // There is no adjacent lane to pull into, so a broken down car in front is a wall: the only
        // safe thing to do is wait behind it.
        Assert.True(subject.ShouldStopForVehicleAhead(city.CreateContext()));
    }

    [Fact]
    public void ShouldStopForVehicleAhead_IsFalseWhenAnAdjacentLaneLetsTheVehiclePullOut()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 2);
        Vehicle subject = city.PlaceVehicle(road, 20, lane: 1, speedKph: 30);
        Vehicle obstacle = city.PlaceVehicle(road, 21, lane: 1);
        obstacle.Disable(city.CreateContext());

        // On a two-lane road the driver pulls out around the obstacle instead of queueing behind it,
        // so the answer is "do not stop" — the manoeuvre itself is then tested separately.
        Assert.False(subject.ShouldStopForVehicleAhead(city.CreateContext()));
    }

    // ---------------------------------------------------------------------------------------------
    // Traffic lights
    // ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(TrafficLightState.Red, true)]
    [InlineData(TrafficLightState.Yellow, true)]
    [InlineData(TrafficLightState.Green, false)]
    public void ShouldStopForTrafficLight_DependsOnTheSignalOfTheLightAtTheJunctionAhead(
        TrafficLightState state,
        bool expected)
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);
        city.City.TrafficLights.Add(cross.West, cross.Centre, state);

        // Just before the stop line the light decides: red and yellow mean stop, green means go.
        // The car is deliberately placed short of the line, because a vehicle that has already
        // crossed it must clear the junction instead of stopping on top of the crossing.
        Vehicle vehicle = city.PlaceVehicle(cross.West, 88, speedKph: 10);

        Assert.Equal(expected, vehicle.ShouldStopForTrafficLight(city.CreateContext()));
    }

    [Fact]
    public void ShouldStopForTrafficLight_IgnoresLightsOnOtherRoadsOrJunctions()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);
        city.City.TrafficLights.Add(cross.North, cross.Centre, TrafficLightState.Red);
        city.City.TrafficLights.Add(cross.West, city.Node(0, 200), TrafficLightState.Red);

        // Our car drives the west arm towards the crossing. A red light on the northern arm, and a
        // red light standing at an unrelated junction, simply do not control it.
        Vehicle vehicle = city.PlaceVehicle(cross.West, 88, speedKph: 10);

        Assert.False(vehicle.ShouldStopForTrafficLight(city.CreateContext()));
    }

    // ---------------------------------------------------------------------------------------------
    // Right of way
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GetVehiclesEnteringFromRight_ReturnsTrafficCrossingThisJunctionFromTheRight()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);

        // The subject drives east along the western arm. With Y pointing down, the arm on its right
        // is the southern one, so the traffic it has to give way to comes up the southern arm
        // towards the same junction.
        Vehicle subject = city.PlaceVehicle(cross.West, 90);
        Vehicle fromTheRight = city.PlaceVehicle(cross.South, 90, isMovingForward: false);

        List<Vehicle> found = [.. subject.GetVehiclesEnteringFromRight(city.CreateContext())];

        // Both cars must be aiming at the same junction for the rule to apply.
        Assert.Same(fromTheRight, Assert.Single(found));
        Assert.Same(subject.NextIntersection, fromTheRight.NextIntersection);
    }

    [Fact]
    public void GetVehiclesEnteringFromRight_ExcludesTrafficHeadingElsewhere()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);

        // The subject drives east towards the crossing. None of the cars below enters that crossing
        // from the northern arm: one is on the far arm heading away, one is behind the subject coming
        // the other way, and one is on the northern arm driving away from the junction instead of
        // into it, so it never arrives at the same crossing.
        Vehicle subject = city.PlaceVehicle(cross.West, 90);
        city.PlaceVehicle(cross.East, 10);
        city.PlaceVehicle(cross.West, 10, isMovingForward: false);
        city.PlaceVehicle(cross.North, 10, isMovingForward: false);

        Assert.Empty(subject.GetVehiclesEnteringFromRight(city.CreateContext()));
    }

    [Fact]
    public void ShouldYieldToVehicleOnRight_IsFalseWhenNothingIsThere()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);

        // At the stop line of an empty junction there is nothing to give way to.
        Vehicle vehicle = city.PlaceVehicle(cross.West, 93.5, speedKph: 20);

        Assert.True(vehicle.IsAboutToLeaveRoad());
        Assert.False(vehicle.ShouldYieldToVehicleOnRight(city.CreateContext()));
    }

    [Fact]
    public void ShouldYieldToVehicleOnRight_IsTrueForACarWaitingAtTheJunctionOnTheRight()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);

        // Both cars reach the stop line of the same crossing; the one arriving from the driver's
        // right has priority, so the eastbound car must give way before entering.
        Vehicle subject = city.PlaceVehicle(cross.West, 93.5, speedKph: 20);

        // The southern arm is the one on the driver's right; its car drives north towards the same
        // crossing. It is placed inside the approach zone of the junction, because right of way only
        // applies to traffic that is actually arriving.
        city.PlaceVehicle(cross.South, 8, isMovingForward: false);

        Assert.True(subject.IsAboutToLeaveRoad());
        Assert.True(subject.ShouldYieldToVehicleOnRight(city.CreateContext()));
    }

    [Fact]
    public void ShouldYieldToVehicleOnRight_IsFalseWhenTheRightHandCarIsHeldByARedLight()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);
        city.City.TrafficLights.Add(cross.North, cross.Centre, TrafficLightState.Red);

        // The car that would otherwise have priority is the one coming down the arm on the driver's
        // right. The light in its face is red, so it cannot move and stops being traffic.
        Vehicle subject = city.PlaceVehicle(cross.West, 93.5, speedKph: 20);
        city.PlaceVehicle(cross.North, 92, isMovingForward: true);

        Assert.True(subject.IsAboutToLeaveRoad());
        Assert.False(subject.ShouldYieldToVehicleOnRight(city.CreateContext()));
    }

    [Fact]
    public void ShouldYieldToVehicleOnRight_IsFalseFarFromTheJunction()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);

        // Even with a car entering from the right, a vehicle with more than fifty metres to go has
        // no reason to yield: right of way only applies at the junction itself.
        Vehicle subject = city.PlaceVehicle(cross.West, 40, speedKph: 10);
        city.PlaceVehicle(cross.North, 94, isMovingForward: true);

        Assert.False(subject.IsAboutToLeaveRoad());
        Assert.False(subject.ShouldYieldToVehicleOnRight(city.CreateContext()));
    }

    // ---------------------------------------------------------------------------------------------
    // Lane changes
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TryChangeLane_IsRefusedOnASingleLaneRoad()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 1);
        Vehicle vehicle = city.PlaceVehicle(road, 50, speedKph: 30);

        // There is nowhere to go: a one-lane road leaves the driver stuck in the queue.
        Assert.False(vehicle.TryChangeLane());
        Assert.Equal(1, vehicle.LaneNumber);
    }

    [Fact]
    public void TryChangeLane_MovesToAFreeAdjacentLane()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 2);
        Vehicle vehicle = city.PlaceVehicle(road, 50, lane: 2);

        // The inner lane is empty, so the driver may move over to it.
        Assert.True(vehicle.TryChangeLane());
        Assert.Equal(1, vehicle.LaneNumber);
    }

    [Fact]
    public void TryChangeLane_RefusesWhenTheTargetLaneIsOccupied()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 2);
        Vehicle vehicle = city.PlaceVehicle(road, 50, lane: 1);

        // A car three metres away in the inner lane is inside LaneChangeGapMetres, so cutting in
        // front of it would be a collision; the manoeuvre must be refused.
        city.PlaceVehicle(road, 53, lane: 2);

        Assert.False(vehicle.TryChangeLane());
        Assert.Equal(1, vehicle.LaneNumber);
    }

    [Fact]
    public void TryChangeLane_MovesTheVehicleIntoTheOtherLaneAndReplans()
    {
        TestCity city = new();
        Intersection junction = city.Node(500, 500);

        // An isolated crossroad: two lanes approach it from the west, the street continues east and
        // branches off to the north and to the south. Nothing else touches this junction.
        Road approach = city.AddTwoWayRoad(city.Node(400, 500), junction, lanesPerDirection: 2);
        Road toNorth = city.AddTwoWayRoad(junction, city.Node(500, 400));
        Road toEast = city.AddTwoWayRoad(junction, city.Node(600, 500));
        Road toSouth = city.AddTwoWayRoad(junction, city.Node(500, 600));

        // A driver heading east has the northern branch on the left and the southern one on the
        // right, with the street continuing straight on. Those three are the junction's candidates.
        Assert.Contains(toNorth, approach.GetRoadsInDirection(junction, RoadDirection.Left));
        Assert.Contains(toSouth, approach.GetRoadsInDirection(junction, RoadDirection.Right));
        Assert.Contains(toEast, approach.GetRoadsInDirection(junction, RoadDirection.Forward));

        // The rightmost lane may go straight on or turn right, never left: the northern road is not
        // among its candidates, and the queued draw picks the street that continues east.
        Vehicle vehicle = city.PlaceVehicle(approach, 90, lane: 1);
        Assert.Equal(1, vehicle.LaneNumber);

        city.Random.ThenInteger(0);
        Assert.Same(toEast, vehicle.ChooseNextRoad());

        // Moving into the inner lane changes which turn is legal, so the planned road has to be
        // recomputed for the lane the vehicle now occupies. The rightmost lane could not reach the
        // northern branch at all.
        Assert.True(vehicle.TryChangeLane());
        Assert.Equal(2, vehicle.LaneNumber);

        // The inner lane can go straight on or turn left; the queued draw picks the turn.
        city.Random.ThenInteger(1);
        Assert.Same(toNorth, vehicle.ChooseNextRoad());
    }

    // ---------------------------------------------------------------------------------------------
    // Dead ends and route choice
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void IsAtDeadEnd_IsFalseWhileARoadContinues()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        Road approach = city.AddTwoWayRoad(city.Node(-100, 0), junction);
        Road continuation = city.AddTwoWayRoad(junction, city.Node(100, 0));

        // Standing at the stop line with a road carrying on through the junction: the vehicle has
        // somewhere to go, so this is no dead end. The chosen road is recomputed on the spot, and
        // both directions of the two-way street remain available.
        Vehicle vehicle = city.PlaceVehicle(approach, 90, speedKph: 20);

        Assert.False(vehicle.IsAtDeadEnd());
        Assert.NotNull(vehicle.NextRoad);
        Assert.Contains(continuation, junction.OutgoingRoads);
        Assert.Contains(continuation, junction.IncomingRoads);
    }

    [Fact]
    public void IsAtDeadEnd_IsTrueWhenTheJunctionHasNoOutgoingRoad()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);

        // A one-way road that simply stops: nothing leaves its end junction and the street cannot be
        // driven backwards either, so a driver that has reached the stop line has run out of road.
        Road stub = city.AddOneWayRoad(start, end);
        Vehicle vehicle = city.PlaceVehicle(stub, 98, speedKph: 20);

        Assert.True(vehicle.IsAtDeadEnd());
        Assert.Empty(end.OutgoingRoads);
        Assert.Equal(1, vehicle.LaneNumber);

        // IsAtDeadEnd only reports, it does not move the vehicle.
        Assert.Equal(98, vehicle.Position, 10);
        Assert.Same(stub, vehicle.CurrentRoad);
    }

    [Fact]
    public void IsAtDeadEnd_IsTrueForAOneWayStubWhereNoUTurnIsLegal()
    {
        TestCity city = new();
        Road stub = city.AddOneWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle vehicle = city.PlaceVehicle(stub, 98, speedKph: 20);

        // The road ends with nothing beyond it and a one-way street cannot be driven backwards, so
        // the driver really is stuck.
        Assert.True(vehicle.IsAtDeadEnd());
        Assert.Null(vehicle.NextRoad);
        Assert.Equal(1, vehicle.LaneNumber);
    }

    [Fact]
    public void Update_TurnsTheVehicleAroundAtADeadEndOfATwoWayRoad()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(50, 0);
        Road road = city.AddTwoWayRoad(start, end);

        // Rolling to the very end of a road that leads nowhere: the only way out is a U-turn, which
        // is legal here because the road carries oncoming traffic and can be driven both ways.
        Vehicle vehicle = city.PlaceVehicle(road, 49, speedKph: 36);
        Assert.True(vehicle.IsMovingForward);
        Assert.True(vehicle.IsAboutToLeaveRoad());

        vehicle.Update(city.CreateContext());

        // The U-turn happened: the car is still on the same road but now drives back towards the
        // start, so the junction it watches has swapped ends.
        Assert.Equal(RoadTravelDirection.Backward, vehicle.TravelDirection);
        Assert.False(vehicle.IsMovingForward);
        Assert.Same(road, vehicle.CurrentRoad);
        Assert.Same(start, vehicle.NextIntersection);

        // The turn leaves the car on its road, now at the far end and driving the other way.
        Assert.Contains(vehicle, road.Vehicles);
        Assert.Equal(50, vehicle.Position, 10);
    }

    [Fact]
    public void ChooseNextRoad_OnASingleLaneRoadMayPickAnyOutgoingRoad()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        Road approach = city.AddTwoWayRoad(city.Node(-100, 0), junction);
        Road straightOn = city.AddTwoWayRoad(junction, city.Node(100, 0));
        Road turn = city.AddTwoWayRoad(junction, city.Node(0, 100));

        // A single lane serves every direction, so both outgoing continuations are candidates and
        // the chosen one is one of them, never the road the vehicle is already on.
        Vehicle vehicle = city.PlaceVehicle(approach, 90);

        Road? chosen = vehicle.ChooseNextRoad();

        Assert.NotNull(chosen);
        Road[] exits = [.. junction.OutgoingRoads];
        Assert.Contains(chosen, exits);
        Assert.Contains(straightOn, exits);
        Assert.Contains(turn, exits);
        Assert.Equal(1, vehicle.LaneNumber);
    }

    /// <summary>
    /// Pins down which turn each lane of a two-lane approach is offered.
    /// </summary>
    /// <remarks>
    /// This is the rule documented on <c>Vehicle.ChooseNextRoad</c>: the rightmost lane may go
    /// straight on or turn right, the leftmost lane may go straight on or turn left.
    /// </remarks>
    [Fact]
    public void ChooseNextRoad_ServesEachLaneTheTurnOnItsOwnSide()
    {
        TestCity city = new();
        Intersection junction = city.Node(500, 500);

        // Driving east through a crossroad: straight on, a branch to the north (the driver's left)
        // and a branch to the south (the driver's right).
        Road approach = city.AddTwoWayRoad(city.Node(400, 500), junction, lanesPerDirection: 2);
        Road toNorth = city.AddTwoWayRoad(junction, city.Node(500, 400));
        Road toEast = city.AddTwoWayRoad(junction, city.Node(600, 500));
        Road toSouth = city.AddTwoWayRoad(junction, city.Node(500, 600));

        // The direction labels are asserted first: swapping them would silently invert every turn
        // rule in the simulation, so this is the line that protects the whole convention.
        Assert.Contains(toEast, approach.GetRoadsInDirection(junction, RoadDirection.Forward));
        Assert.Contains(toNorth, approach.GetRoadsInDirection(junction, RoadDirection.Left));
        Assert.Contains(toSouth, approach.GetRoadsInDirection(junction, RoadDirection.Right));

        // Blocking the entry of the street straight ahead takes it out of the running, which leaves
        // each lane with exactly one candidate and makes the choice independent of the random draw.
        city.PlaceVehicle(toEast, 1, speedKph: 30);

        // A vehicle only commits to a turn once it is at the stop line; further back it is still
        // free to continue straight on, so the cars are placed where the turn is forced.
        double stopLine = approach.Length - 10;

        Vehicle rightmost = city.PlaceVehicle(approach, stopLine, lane: 1);
        Assert.Same(toSouth, rightmost.ChooseNextRoad());

        Vehicle inner = city.PlaceVehicle(approach, stopLine, lane: 2);
        Assert.Same(toNorth, inner.ChooseNextRoad());
    }

    [Fact]
    public void ChooseNextRoad_ReturnsNullWhenNoOutgoingRoadHasRoom()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        Road approach = city.AddOneWayRoad(city.Node(-100, 0), junction);
        Road onlyExit = city.AddOneWayRoad(junction, city.Node(100, 0));

        Vehicle vehicle = city.PlaceVehicle(approach, 95.5, speedKph: 20);

        // The only way out is occupied right at its entrance by a car that is still driving, so
        // entering it would end with a stopped vehicle in the middle of the junction. A one-way road
        // offers no U-turn, so there is then no candidate left at all.
        city.PlaceVehicle(onlyExit, 1, speedKph: 30);
        Assert.Empty(onlyExit.GetFreeLanes(vehicle));

        // With no road that can physically be entered the vehicle plans nothing and waits.
        Assert.True(vehicle.IsAboutToLeaveRoad());
        Assert.Null(vehicle.ChooseNextRoad());
        Assert.True(vehicle.IsNextRoadBlocked());
        Assert.True(vehicle.ShouldStopForThisTick(city.CreateContext()));
    }

    [Fact]
    public void ChooseNextRoad_PrefersARoadWithAFreeLane()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        Road approach = city.AddOneWayRoad(city.Node(-100, 0), junction);
        Road firstExit = city.AddOneWayRoad(junction, city.Node(100, 0));
        Road secondExit = city.AddOneWayRoad(junction, city.Node(0, 100));

        Vehicle vehicle = city.PlaceVehicle(approach, 90, speedKph: 20);

        // Exit one carries a car right at its entrance, so it has no free lane and is filtered out.
        // Exit two is clear. A one-way approach has no U-turn candidate, so the second exit is the
        // only road left and must be the one that is planned.
        city.PlaceVehicle(firstExit, 1, speedKph: 30);
        Assert.Empty(firstExit.GetFreeLanes(vehicle));
        Assert.NotEmpty(secondExit.GetFreeLanes(vehicle));

        Road? chosen = vehicle.ChooseNextRoad();

        Assert.Same(secondExit, chosen);
        Assert.NotSame(firstExit, chosen);
    }

    // ---------------------------------------------------------------------------------------------
    // Accidents and breakdowns
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Disable_StopsTheVehicleAndMarksAReason()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle vehicle = city.PlaceVehicle(road, 50, speedKph: 40);

        vehicle.Disable(city.CreateContext());

        // A crashed or broken car is standing still and says why.
        Assert.True(vehicle.IsDisabled);
        Assert.NotEqual(VehicleDisabledReason.None, vehicle.DisabledReason);
        Assert.Equal(0, vehicle.SpeedKph, 10);
        Assert.True(vehicle.RemainingDisabledTicks > 0);
    }

    [Fact]
    public void Disable_CanBeForcedIntoAnAccidentOrABreakdown()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle crashing = city.PlaceVehicle(road, 20, speedKph: 40);
        Vehicle breaking = city.PlaceVehicle(road, 60, speedKph: 40);

        // The draw below 0.5 is an accident, the one above it a breakdown.
        city.ThenRandomDouble(0.2);
        crashing.Disable(city.CreateContext());

        city.ThenRandomDouble(0.8);
        breaking.Disable(city.CreateContext());

        Assert.True(crashing.IsInCarAccident);
        Assert.True(breaking.HasBrokenDown);
    }

    [Fact]
    public void Repair_MakesADisabledVehicleMobileAgain()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle vehicle = city.PlaceVehicle(road, 50, speedKph: 40);
        vehicle.Disable(city.CreateContext());

        vehicle.Repair();

        Assert.False(vehicle.IsDisabled);
        Assert.Equal(VehicleDisabledReason.None, vehicle.DisabledReason);
        Assert.Equal(0, vehicle.RemainingDisabledTicks);
    }

    [Fact]
    public void Update_KeepsADisabledVehicleParkedUntilItsTimeIsUp()
    {
        TestCity city = new();
        city.Settings.AccidentDurationTicks = 5;
        city.Settings.BreakdownDurationTicks = 5;

        // No random accidents after the initial one: this test drives the repair timer, not the risk.
        city.Settings.AccidentChancePerTick = 0;
        city.Settings.BreakdownChancePerTick = 0;

        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle vehicle = city.PlaceVehicle(road, 50, speedKph: 30);

        // Force an accident by hand: the drawn value decides between accident and breakdown.
        city.ThenRandomDouble(0.1);
        vehicle.Disable(city.CreateContext());

        Assert.True(vehicle.IsDisabled);
        Assert.True(vehicle.IsInCarAccident);
        double parked = vehicle.Position;

        // While the car is out of action every tick is spent standing still.
        for (int tick = 0; tick < 3; tick++)
        {
            vehicle.Update(city.CreateContext());
            Assert.Equal(parked, vehicle.Position, 10);
            Assert.Equal(0, vehicle.SpeedKph, 10);
        }

        // Two more ticks exhaust the five tick repair time, after which the car is mobile again.
        int ticksToRecover = 0;
        while (vehicle.IsDisabled && ticksToRecover < 20)
        {
            vehicle.Update(city.CreateContext());
            ticksToRecover++;
        }

        Assert.False(vehicle.IsDisabled);
        Assert.Equal(2, ticksToRecover);

        // Mobile again: the car accelerates and moves on instead of standing still.
        vehicle.Update(city.CreateContext());
        Assert.True(vehicle.SpeedKph > 0);
        Assert.True(vehicle.Position > parked);
    }

    [Fact]
    public void GetDisabledRisk_GrowsOnPoorPavement()
    {
        TestCity city = new();

        // One percent base risk per tick, with breakdowns and the rain bonus switched off so that
        // only the pavement matters.
        city.Settings.AccidentChancePerTick = 0.01;
        city.Settings.BreakdownChancePerTick = 0;
        city.Settings.RainAccidentChancePerTick = 0;

        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle vehicle = city.PlaceVehicle(road, 50);
        SimulationContext context = city.CreateContext();

        double onGoodRoad = vehicle.GetDisabledRisk(context);

        // Worn asphalt roughly doubles the accident risk: 0.01 * (1 + (1 - 0.5)) = 0.015.
        road.Quality = 0.5;

        Assert.Equal(0.01, onGoodRoad, 10);
        Assert.Equal(0.015, vehicle.GetDisabledRisk(context), 10);
        Assert.True(vehicle.GetDisabledRisk(context) > onGoodRoad);
    }

    [Fact]
    public void GetDisabledRisk_GrowsWhileItRains()
    {
        TestCity city = new();
        city.Settings.RainChancePerTick = 0.9;
        city.Settings.AccidentChancePerTick = 0.01;
        city.Settings.BreakdownChancePerTick = 0;
        city.Settings.RainAccidentChancePerTick = 0.005;
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle vehicle = city.PlaceVehicle(road, 20);

        double inDryWeather = vehicle.GetDisabledRisk(city.CreateContext());

        // Start a rain period and let the vehicle see the wet world of the next tick.
        city.ThenRandomDouble(0);
        city.City.Update();

        Assert.True(city.City.IsRaining);
        Assert.Equal(0.01, inDryWeather, 10);
        Assert.Equal(0.015, vehicle.GetDisabledRisk(city.CreateContext()), 10);
    }

    // ---------------------------------------------------------------------------------------------
    // Update integration
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Update_AcceleratesAndMovesAVehicleFarFromAnyJunction()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle vehicle = city.PlaceVehicle(road, 20);
        SimulationContext context = city.CreateContext();

        // Nothing to stop for: one tick adds one km/h and the car rolls (1 / 3.6) * 0.1 = 0.0278 m.
        vehicle.Update(context);

        Assert.Equal(1, vehicle.SpeedKph, 10);
        Assert.Equal(20.0277778, vehicle.Position, 6);

        // The speed keeps growing on later ticks, so the vehicle really accelerates away.
        vehicle.Update(context);
        Assert.Equal(2, vehicle.SpeedKph, 10);
        Assert.True(vehicle.Position > 20.0277778);
    }

    [Fact]
    public void Update_DoesNotDriveThroughADisabledCarStandingInTheLane()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 1);
        Vehicle obstacle = city.PlaceVehicle(road, 60, speedKph: 40);
        obstacle.Disable(city.CreateContext());

        Vehicle follower = city.PlaceVehicle(road, 40, speedKph: 40);
        SimulationContext context = city.CreateContext();

        // The follower may close the gap but must never end up at or past the wreck: the safety
        // distance has to hold on every single tick.
        for (int tick = 0; tick < 20; tick++)
        {
            follower.Update(context);
            Assert.True(
                follower.Position <= obstacle.Position,
                $"The follower reached {follower.Position} m and drove into the wreck at {obstacle.Position} m.");
        }

        Assert.True(follower.Position > 40, "The follower should still have crept forward.");

        // The stopping rule is evaluated once per tick, so the car can still close a little of the
        // gap during the tick in which it decides to brake. The invariant is that the gap is never
        // negative and never more than one tick of travel short of the safety distance, even at the
        // highest speed the follower reached.
        double oneTickOfTravel = 52 / 3.6 * SimulationConstants.TickSeconds;
        Assert.True(
            follower.Position <= obstacle.Position - SimulationConstants.SafetyDistanceMetres + oneTickOfTravel,
            $"The follower stands only {obstacle.Position - follower.Position} m behind the wreck.");
        Assert.True(follower.Position < obstacle.Position);
    }

    [Fact]
    public void Update_StopsCompletelyAtARedLightAndStaysPut()
    {
        TestCity city = new();

        // No random breakdowns: this test is only about the light.
        city.Settings.BreakdownChancePerTick = 0;
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);
        city.City.TrafficLights.Add(cross.West, cross.Centre, TrafficLightState.Red);

        // Half a metre short of the stop line at 20 km/h: one more tick would carry the car into the
        // crossing, so this is the tick on which the light has to be obeyed.
        Vehicle vehicle = city.PlaceVehicle(cross.West, 93.5, speedKph: 20);
        Assert.True(vehicle.IsAboutToLeaveRoad());

        vehicle.Update(city.CreateContext());

        // The car is brought to a hard stop: it does not enter the crossing.
        Assert.Equal(0, vehicle.SpeedKph, 10);
        Assert.Same(cross.West, vehicle.CurrentRoad);

        double stoppedAt = vehicle.Position;
        Assert.True(stoppedAt < cross.West.Length - vehicle.StopDistance + 0.5);

        // And it stays there. A car that has stopped travels nowhere, so a stop rule that depended on
        // the distance covered during the tick would switch itself off and let the car creep across
        // the stop line a few centimetres at a time.
        SimulationContext context = city.CreateContext();
        for (int tick = 0; tick < 600; tick++)
        {
            vehicle.Update(context);

            Assert.Equal(stoppedAt, vehicle.Position, 10);
            Assert.Equal(0, vehicle.SpeedKph, 10);
            Assert.Same(cross.West, vehicle.CurrentRoad);
        }

        // Ten seconds of red light must not move the car into the junction.
        Assert.True(
            vehicle.Position < cross.West.Length - vehicle.StopDistance,
            $"The car crept to {vehicle.Position:F3} m, past the stop line.");
    }
}
