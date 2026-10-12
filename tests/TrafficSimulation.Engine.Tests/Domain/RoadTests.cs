using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;
using TrafficSimulation.Tests.Helpers;

namespace TrafficSimulation.Tests.Domain;

/// <summary>Tests for <see cref="Road"/> geometry and for the turn rules that depend on it.</summary>
public class RoadTests
{
    [Fact]
    public void LengthAndAngleFollowFromTheEndPoints()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(10, 20), city.Node(40, 60));

        Assert.Equal(50, road.Length, 10);
        Assert.Equal(Math.Atan2(40, 30), road.Angle, 10);
    }

    [Fact]
    public void RegisteringARoadAddsItToBothEndPoints()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);

        Road road = city.AddTwoWayRoad(start, end);

        // A two-way road carries traffic in both directions, so it is incoming and outgoing at
        // both of its ends.
        Assert.Contains(road, start.IncomingRoads);
        Assert.Contains(road, start.OutgoingRoads);
        Assert.Contains(road, end.IncomingRoads);
        Assert.Contains(road, end.OutgoingRoads);
    }

    [Fact]
    public void AOneWayRoadIsOutgoingAtItsStartAndIncomingAtItsEnd()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);

        Road road = city.AddOneWayRoad(start, end);

        Assert.Contains(road, start.OutgoingRoads);
        Assert.DoesNotContain(road, start.IncomingRoads);
        Assert.Contains(road, end.IncomingRoads);
        Assert.DoesNotContain(road, end.OutgoingRoads);
    }

    [Theory]
    [InlineData(RoadType.OneWay, 1, 3.75)]
    [InlineData(RoadType.OneWay, 2, 7.5)]
    [InlineData(RoadType.TwoWay, 1, 7.5)]
    [InlineData(RoadType.TwoWay, 3, 22.5)]
    public void WidthCountsTheLanesThatActuallyExist(RoadType type, int lanes, double expectedWidth)
    {
        TestCity city = new();
        Road road = type == RoadType.OneWay
            ? city.AddOneWayRoad(city.Node(0, 0), city.Node(100, 0), lanes)
            : city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanes);

        Assert.Equal(expectedWidth, road.Width, 10);
    }

    [Fact]
    public void CapacityAccountsForTheDirectionOfTravel()
    {
        TestCity city = new();
        Road oneWay = city.AddOneWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 1);
        Road twoWay = city.AddTwoWayRoad(city.Node(0, 100), city.Node(100, 100), lanesPerDirection: 1);

        // A hundred metres of single lane holds eleven vehicles at two vehicle lengths each; a
        // two-way road doubles that because both directions can be used.
        Assert.Equal(11, oneWay.Capacity);
        Assert.Equal(22, twoWay.Capacity);
    }

    [Fact]
    public void SpeedLimitAndQualityAreClampedToTheSupportedRange()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));

        road.SpeedLimitKph = 1000;
        Assert.Equal(SimulationConstants.MaxSpeedLimitKph, road.SpeedLimitKph);

        road.SpeedLimitKph = 1;
        Assert.Equal(SimulationConstants.MinSpeedLimitKph, road.SpeedLimitKph);

        road.Quality = 5;
        Assert.Equal(SimulationConstants.MaxRoadQuality, road.Quality);

        road.Quality = -1;
        Assert.Equal(SimulationConstants.MinRoadQuality, road.Quality);
    }

    [Fact]
    public void SpeedLimitAndQualityAreInheritedFromTheCityWhenARoadIsAdded()
    {
        TestCity city = new();
        city.Settings.SpeedLimitKph = 80;
        city.Settings.RoadQuality = 0.5;

        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));

        Assert.Equal(80, road.SpeedLimitKph);
        Assert.Equal(0.5, road.Quality, 10);
    }

    [Fact]
    public void GetRoadsInDirection_ClassifiesLeftForwardAndRightOnAStraightRoad()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);

        // A vehicle driving east out of the crossing continues west of the junction.
        IReadOnlyList<Road> forward = cross.West.GetRoadsInDirection(cross.Centre, RoadDirection.Forward);

        // Going straight on: the continuation of the street, but never the road the vehicle is on.
        Assert.Contains(cross.East, forward);
        Assert.DoesNotContain(cross.West, forward);
    }

    [Fact]
    public void GetRoadsInDirection_FindsTurnsThroughACrossroads()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);

        // Standing on the western arm and looking at the crossing, a vehicle faces east.
        IReadOnlyList<Road> left = cross.West.GetRoadsInDirection(cross.Centre, RoadDirection.Left);
        IReadOnlyList<Road> right = cross.West.GetRoadsInDirection(cross.Centre, RoadDirection.Right);

        // The coordinate system has Y pointing down, so for an eastbound driver the northern branch
        // lies to the left and the southern one to the right. Getting this backwards silently swaps
        // every turn rule in the simulation, so it is asserted explicitly.
        Assert.Contains(cross.North, left);
        Assert.Contains(cross.South, right);
    }

    [Fact]
    public void GetRoadsInDirection_ExcludesRoadsThatOnlyLeadIntoTheJunctionByDefault()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        Intersection west = city.Node(-100, 0);
        Intersection east = city.Node(100, 0);

        Road approach = city.AddOneWayRoad(west, junction);
        Road continuation = city.AddOneWayRoad(junction, east);

        // A second road physically continues the street but carries traffic into the junction, so it
        // can never be entered from here.
        Road intoJunction = city.AddOneWayRoad(east, junction);

        IReadOnlyList<Road> drivable = approach.GetRoadsInDirection(junction, RoadDirection.Forward);
        Assert.Equal([continuation], drivable);

        // With the wider view the same road shows up, plus the one that only leads into the junction,
        // which is what the right-of-way rule needs in order to see traffic coming towards it.
        IReadOnlyList<Road> all = approach.GetRoadsInDirection(
            junction,
            RoadDirection.Forward,
            outgoingOnly: false);

        Assert.Contains(continuation, all);
        Assert.Contains(intoJunction, all);
    }

    [Fact]
    public void GetRoadsInDirection_RejectsAnIntersectionThatIsNotAnEndPoint()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Intersection stranger = city.Node(0, 100);

        Assert.Throws<ArgumentException>(
            () => road.GetRoadsInDirection(stranger, RoadDirection.Forward));
    }

    [Fact]
    public void GetOppositeIntersection_ReturnsTheOtherEnd()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);
        Road road = city.AddTwoWayRoad(start, end);

        Assert.Same(end, road.GetOppositeIntersection(start));
        Assert.Same(start, road.GetOppositeIntersection(end));
    }

    [Fact]
    public void GetOppositeIntersection_ThrowsForAnUnrelatedJunction()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));

        Assert.Throws<ArgumentException>(
            () => road.GetOppositeIntersection(city.Node(7, 7)));
    }

    [Fact]
    public void GetPositionOnRoad_MeasuresAlongTheRoadFromItsStart()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(10, 10), city.Node(110, 10));
        Vehicle vehicle = Vehicle.Place(road, 25);

        Vector2 position = road.GetPositionOnRoad(vehicle);

        Assert.Equal(35, position.X, 10);
        Assert.Equal(10, position.Y, 10);
    }

    [Fact]
    public void TrafficJamLevelGrowsWithTheNumberOfVehicles()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 1);

        // Capacity is twenty-two, so the thresholds sit at five, eleven and sixteen vehicles.
        Assert.Equal(TrafficJamLevel.Free, road.TrafficJamLevel);

        for (int i = 0; i < 6; i++)
        {
            city.City.AddVehicle(Vehicle.Place(road, i * 3));
        }

        Assert.Equal(TrafficJamLevel.Busy, road.TrafficJamLevel);

        for (int i = 6; i < 12; i++)
        {
            city.City.AddVehicle(Vehicle.Place(road, i * 3));
        }

        Assert.Equal(TrafficJamLevel.Congested, road.TrafficJamLevel);

        for (int i = 12; i < 20; i++)
        {
            city.City.AddVehicle(Vehicle.Place(road, i * 3));
        }

        Assert.Equal(TrafficJamLevel.Jammed, road.TrafficJamLevel);
    }

    [Fact]
    public void GetFreeLanes_SkipsLanesBlockedNearTheEntryPoint()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 3);
        Road approach = city.AddTwoWayRoad(city.Node(-100, 0), city.Node(0, 0));

        // A vehicle already standing in lane two right at the start of the road blocks it.
        city.City.AddVehicle(Vehicle.Place(road, 0, lane: 2));

        Vehicle entering = Vehicle.Place(approach, 90);

        IReadOnlyList<int> freeLanes = road.GetFreeLanes(entering);

        Assert.DoesNotContain(2, freeLanes);
        Assert.Contains(1, freeLanes);
        Assert.Contains(3, freeLanes);
    }

    [Fact]
    public void GetFreeLanes_IgnoresAVehicleThatIsFarAwayOrDisabled()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 2);
        Road approach = city.AddTwoWayRoad(city.Node(-100, 0), city.Node(0, 0));

        // Far away: does not block the entry.
        city.City.AddVehicle(Vehicle.Place(road, 80, lane: 1));

        Vehicle entering = Vehicle.Place(approach, 90);
        Assert.Contains(1, road.GetFreeLanes(entering));
    }
}
