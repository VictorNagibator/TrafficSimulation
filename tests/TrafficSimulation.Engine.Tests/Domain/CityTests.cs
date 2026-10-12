using System.Globalization;
using System.Text;
using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;
using TrafficSimulation.Engine.Randomness;
using TrafficSimulation.Engine.Simulation;
using TrafficSimulation.Tests.Helpers;

namespace TrafficSimulation.Tests.Domain;

/// <summary>
/// End to end tests of the city: the tick loop, change notifications and the guarantee that a
/// running simulation never leaves the model in an inconsistent state.
/// </summary>
/// <remarks>
/// The invariant checks here matter more than any single assertion: a simulation that keeps running
/// for thousands of ticks without a vehicle ending up off its road, in a lane that does not exist or
/// on a road that is not part of the city, is the strongest evidence that the model holds together.
/// </remarks>
public class CityTests
{
    [Fact]
    public void AnEmptyCityCanBeUpdatedWithoutDoingAnything()
    {
        TestCity city = new();

        city.City.Update(10);

        Assert.Empty(city.City.Roads);
        Assert.Empty(city.City.Vehicles);
    }

    [Fact]
    public void AddRoad_InheritsSpeedLimitQualityAndRegistersBothEndPoints()
    {
        TestCity city = new();
        city.Settings.SpeedLimitKph = 70;
        city.Settings.RoadQuality = 0.8;

        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);
        Road road = city.AddTwoWayRoad(start, end);

        Assert.Contains(road, city.City.Roads);
        Assert.Contains(road, start.OutgoingRoads);
        Assert.Contains(road, end.IncomingRoads);
        Assert.Equal(70, road.SpeedLimitKph);
        Assert.Equal(0.8, road.Quality, 10);
    }

    [Fact]
    public void AddRoad_RejectsTheSameRoadTwice()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));

        Assert.Throws<InvalidOperationException>(() => city.City.AddRoad(road));
    }

    [Fact]
    public void SettingTheCitySpeedLimitUpdatesEveryExistingRoad()
    {
        TestCity city = new();
        Road first = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Road second = city.AddTwoWayRoad(city.Node(0, 50), city.Node(100, 50));

        city.City.SpeedLimitKph = 90;

        Assert.Equal(90, first.SpeedLimitKph);
        Assert.Equal(90, second.SpeedLimitKph);
        Assert.Equal(90, city.Settings.SpeedLimitKph);
    }

    [Fact]
    public void SettingTheCityRoadQualityUpdatesEveryExistingRoad()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));

        city.City.RoadQuality = 0.4;

        Assert.Equal(0.4, road.Quality, 10);
    }

    [Fact]
    public void RemoveRoad_TakesItsVehiclesOffTheMapWithIt()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        Vehicle vehicle = city.PlaceVehicle(road, 50);

        city.City.RemoveRoad(road);

        // A vehicle cannot exist on a road that is no longer there.
        Assert.DoesNotContain(road, city.City.Roads);
        Assert.Empty(city.City.Vehicles);
        Assert.Empty(road.Vehicles);
        Assert.DoesNotContain(vehicle, city.City.Vehicles);
    }

    [Fact]
    public void RemoveRoad_DetachesItFromItsJunctions()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);
        Road road = city.AddTwoWayRoad(start, end);

        city.City.RemoveRoad(road);

        Assert.True(start.HasNoRoads());
        Assert.True(end.HasNoRoads());
    }

    [Fact]
    public void SetRoadType_ToTwoWayMakesTheRoadDrivableInBothDirections()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);
        Road road = city.AddOneWayRoad(start, end);

        city.City.SetRoadType(road, RoadType.TwoWay);

        Assert.Contains(road, start.IncomingRoads);
        Assert.Contains(road, end.OutgoingRoads);
    }

    [Fact]
    public void SetRoadType_ToOneWayRemovesVehiclesDrivingAgainstIt()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);
        Road road = city.AddTwoWayRoad(start, end);

        Vehicle drivingForward = city.PlaceVehicle(road, 20, isMovingForward: true);
        Vehicle drivingBackward = city.PlaceVehicle(road, 80, isMovingForward: false);

        city.City.SetRoadType(road, RoadType.OneWay);

        // The backward vehicle would be driving the wrong way on a one-way road, so it is removed.
        Assert.Contains(drivingForward, city.City.Vehicles);
        Assert.DoesNotContain(drivingBackward, city.City.Vehicles);
    }

    [Fact]
    public void SetRoadType_ReplansTheRoutesOfTheRemainingVehicles()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);
        Vehicle vehicle = city.PlaceVehicle(cross.West, 50);

        city.City.SetRoadType(cross.North, RoadType.OneWay);

        // Whatever the vehicle now intends to do, the plan must refer to a road that exists and can
        // be entered; it must never be left pointing at a road it cannot use.
        if (vehicle.NextRoad is not null)
        {
            Assert.Contains(vehicle.NextRoad, city.City.Roads);
        }
    }

    [Fact]
    public void SetLanesPerDirection_MovesVehiclesOutOfLanesThatDisappear()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0), lanesPerDirection: 4);
        Vehicle vehicle = city.PlaceVehicle(road, 50, lane: 4);

        city.City.SetLanesPerDirection(road, 2);

        Assert.Equal(2, road.LanesPerDirection);
        Assert.Equal(2, vehicle.LaneNumber);
    }

    [Fact]
    public void AddVehicle_RejectsAVehicleThatIsNotOnARoadOfThisCity()
    {
        TestCity city = new();
        TestCity otherCity = new();
        Road foreignRoad = otherCity.AddTwoWayRoad(otherCity.Node(0, 0), otherCity.Node(100, 0));

        Vehicle vehicle = Vehicle.Place(foreignRoad, 10);

        Assert.Throws<InvalidOperationException>(() => city.City.AddVehicle(vehicle));
    }

    [Fact]
    public void CreateVehicle_ReturnsNullWhenThereAreNoRoads()
    {
        TestCity city = new();

        Assert.Null(city.City.CreateVehicle());
    }

    [Fact]
    public void CreateVehicle_PlacesAVehicleOnAnExistingRoad()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));

        Vehicle? vehicle = city.City.CreateVehicle();

        Assert.NotNull(vehicle);
        Assert.Same(road, vehicle!.CurrentRoad);
        Assert.Contains(vehicle, road.Vehicles);
        Assert.Contains(vehicle, city.City.Vehicles);
    }

    [Fact]
    public void RemoveAllVehicles_LeavesTheRoadsInPlace()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        city.PlaceVehicle(road, 10);
        city.PlaceVehicle(road, 50);

        city.City.RemoveAllVehicles();

        Assert.Empty(city.City.Vehicles);
        Assert.Empty(road.Vehicles);
        Assert.Single(city.City.Roads);
    }

    [Fact]
    public void Clear_RemovesRoadsVehiclesJunctionsAndLights()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);
        city.PlaceVehicle(cross.West, 10);
        city.City.TrafficLights.Add(cross.West, cross.Centre);

        city.City.Clear();

        Assert.Empty(city.City.Roads);
        Assert.Empty(city.City.Vehicles);
        Assert.Empty(city.City.Intersections);
        Assert.Equal(0, city.City.TrafficLights.Count);
        Assert.Equal(0, city.City.IntersectionIndex.Count);
    }

    [Fact]
    public void Intersections_AreReturnedWithoutDuplicates()
    {
        TestCity city = new();
        RoadLayouts.CreateCross(city);

        // Four arms and a centre: five junctions, each reported once even though every road adds
        // both of its end points.
        Assert.Equal(5, city.City.Intersections.Count);
        Assert.Equal(city.City.Intersections.Count, city.City.Intersections.Distinct().Count());
    }

    [Fact]
    public void GetOrCreateIntersection_ReturnsTheSameJunctionForTheSamePosition()
    {
        TestCity city = new();

        Intersection first = city.City.GetOrCreateIntersection(new Vector2(10, 20));
        Intersection second = city.City.GetOrCreateIntersection(new Vector2(10, 20));

        Assert.Same(first, second);
    }

    [Fact]
    public void Changed_IsRaisedWhenTheCityIsEdited()
    {
        TestCity city = new();
        int notifications = 0;
        city.City.Changed += (_, _) => notifications++;

        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        city.PlaceVehicle(road, 10);
        city.City.RemoveVehicle(road.Vehicles[0]);

        Assert.True(notifications >= 3, $"Expected at least three notifications, got {notifications}.");
    }

    [Fact]
    public void Update_DoesNotNotifySubscribers()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(100, 0));
        city.PlaceVehicle(road, 10, speedKph: 40);

        int notifications = 0;
        city.City.Changed += (_, _) => notifications++;

        // Running the simulation changes vehicle positions, but not the structure of the city, so
        // the view redraws on its own timer instead of through a change notification.
        city.City.Update(20);

        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Update_AcceleratesAndMovesAVehicleAlongItsRoad()
    {
        TestCity city = new();
        Road road = city.AddTwoWayRoad(city.Node(0, 0), city.Node(200, 0));
        Vehicle vehicle = city.PlaceVehicle(road, 10);

        city.City.Update(20);

        Assert.True(vehicle.SpeedKph > 0, "The vehicle should have accelerated.");
        Assert.True(vehicle.Position > 10, "The vehicle should have advanced along its road.");
    }

    [Fact]
    public void RainStartsAndStopsAccordingToTheConfiguredChances()
    {
        TestCity city = new();

        // Force rain on the first draw, then let the period expire.
        city.Settings.RainChancePerTick = 1;
        city.Settings.RainDurationTicks = 10;
        city.ThenRandomDouble(0);

        city.City.Update();
        Assert.True(city.City.IsRaining);

        city.City.Update(10);
        Assert.False(city.City.IsRaining);
    }

    [Fact]
    public void RainNeverStartsWhenTheChanceIsZero()
    {
        TestCity city = new();
        city.Settings.RainChancePerTick = 0;

        city.City.Update(500);

        Assert.False(city.City.IsRaining);
    }

    [Fact]
    public void TrafficLightsAdvanceWithTheSimulation()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);
        TrafficLight light = city.City.TrafficLights.Add(cross.West, cross.Centre, TrafficLightState.Green);

        // Green runs for thirty seconds of simulated time, that is three hundred ticks.
        city.City.Update(300);

        Assert.NotEqual(TrafficLightState.Green, light.State);
    }

    [Fact]
    public void ASeededCityProducesExactlyTheSameRunTwice()
    {
        // The same seed has to reproduce the run exactly. The comparison uses a fingerprint of every
        // tick rather than only the final state, because the final state can coincide by accident
        // once most vehicles have come to a halt.
        Assert.Equal(
            FingerprintOfRun(seed: 12345),
            FingerprintOfRun(seed: 12345));
    }

    [Fact]
    public void TwoDifferentSeedsProduceDifferentRuns()
    {
        Assert.NotEqual(
            FingerprintOfRun(seed: 1),
            FingerprintOfRun(seed: 2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(99)]
    public void ASimulationRunKeepsTheModelConsistent(int seed)
    {
        // This is the safety net for the whole engine: whatever the traffic does, every vehicle must
        // stay on a road of the city, in a lane that exists, at a sane speed, and every junction must
        // remain attached to roads that point back at it.
        RunAndAssertInvariants(seed, ticks: 4000);
    }

    [Fact]
    public void ASimulationRunKeepsTheModelConsistentDuringHeavyRainAndBreakdowns()
    {
        RunAndAssertInvariants(
            seed: 42,
            ticks: 4000,
            configure: settings =>
            {
                settings.RainChancePerTick = 0.05;
                settings.AccidentChancePerTick = 0.02;
                settings.BreakdownChancePerTick = 0.02;
                settings.LaneChangeChancePerTick = 0.3;
            });
    }

    /// <summary>Runs a simulation and asserts that every model invariant still holds afterwards.</summary>
    /// <param name="seed">Seed of the city, so a failure can be reproduced.</param>
    /// <param name="ticks">Number of ticks to simulate.</param>
    /// <param name="configure">Optional tweak of the traffic settings.</param>
    private static void RunAndAssertInvariants(int seed, int ticks, Action<CitySettings>? configure = null)
    {
        TestCity city = new(seed);
        configure?.Invoke(city.Settings);

        BuildSmallNetwork(city);

        for (int i = 0; i < 40; i++)
        {
            city.City.CreateVehicle();
        }

        city.City.Update(ticks);

        foreach (Vehicle vehicle in city.City.Vehicles)
        {
            Assert.Contains(vehicle.CurrentRoad, city.City.Roads);
            Assert.Contains(vehicle, vehicle.CurrentRoad.Vehicles);
            Assert.InRange(vehicle.LaneNumber, 1, vehicle.CurrentRoad.LanesPerDirection);
            Assert.True(
                double.IsFinite(vehicle.Position),
                $"Vehicle position is not a finite number after {ticks} ticks.");
            Assert.True(
                vehicle.SpeedKph >= 0,
                $"Vehicle speed became negative: {vehicle.SpeedKph}.");
            Assert.True(
                vehicle.SpeedKph <= SimulationConstants.MaxSpeedLimitKph + 1e-6,
                $"Vehicle exceeded the absolute speed limit: {vehicle.SpeedKph}.");

            // A vehicle may only ever be observed standing on its own road: crossing the junction end
            // of it is a state that exists inside a tick, never between two of them.
            Assert.InRange(vehicle.Position, 0, vehicle.CurrentRoad.Length);
        }

        foreach (Road road in city.City.Roads)
        {
            foreach (Vehicle vehicle in road.Vehicles)
            {
                Assert.Same(road, vehicle.CurrentRoad);
            }
        }

        foreach (Intersection intersection in city.City.Intersections)
        {
            foreach (Road road in intersection.AllRoads)
            {
                Assert.True(
                    road.StartIntersection == intersection || road.EndIntersection == intersection,
                    "A junction lists a road that does not have it as an end point.");

                Assert.Contains(road, city.City.Roads);
            }
        }

        foreach (TrafficLight light in city.City.TrafficLights.All)
        {
            Assert.Contains(light.ControlledRoad, city.City.Roads);
            Assert.Contains(light.ControlledIntersection, city.City.Intersections);
        }
    }

    /// <summary>Builds a small grid so that vehicles meet junctions, lights and each other.</summary>
    /// <param name="city">City to build in.</param>
    private static void BuildSmallNetwork(TestCity city)
    {
        // A square of four junctions with a cross road through the middle.
        Intersection northWest = city.Node(0, 0);
        Intersection northEast = city.Node(200, 0);
        Intersection southWest = city.Node(0, 200);
        Intersection southEast = city.Node(200, 200);

        city.AddTwoWayRoad(northWest, northEast);
        city.AddTwoWayRoad(southWest, southEast);
        city.AddTwoWayRoad(northWest, southWest);
        city.AddTwoWayRoad(northEast, southEast);

        // A two-lane road so that lane changes are exercised as well.
        Road middle = city.AddTwoWayRoad(northWest, southEast, lanesPerDirection: 2);

        city.City.TrafficLights.Add(middle, northWest);
        city.City.TrafficLights.Add(middle, southEast);

        // Two lanes on the ring as well, so that lane changes are exercised around the whole grid.
        foreach (Road road in city.City.Roads)
        {
            road.LanesPerDirection = 2;
        }
    }

    /// <summary>
    /// Runs a fixed simulation and returns a value that changes whenever any vehicle state changes.
    /// </summary>
    /// <remarks>
    /// Sampling every tick turns the whole trajectory into the fingerprint. Sampling only the end
    /// state would be much weaker: a run can easily arrive at a similar-looking end state by a
    /// different route, especially once the vehicles have settled.
    /// </remarks>
    /// <param name="seed">Seed of the city.</param>
    /// <param name="ticks">Number of ticks to simulate.</param>
    /// <param name="vehicleCount">Number of vehicles to spawn.</param>
    /// <returns>A hash of every vehicle state on every tick.</returns>
    private static string FingerprintOfRun(int seed, int ticks = 1500, int vehicleCount = 15)
    {
        TestCity city = new(seed);

        // Every random branch of the tick has to be reachable, otherwise two seeds can produce an
        // identical run simply because nothing random ever happened: with quiet traffic the only
        // draw is the accident risk, and a scenario without lane changes or weather never diverges.
        city.Settings.LaneChangeChancePerTick = 0.2;
        city.Settings.AccidentChancePerTick = 0.005;
        city.Settings.BreakdownChancePerTick = 0.005;
        city.Settings.RainChancePerTick = 0.02;
        city.Settings.RainAccidentChancePerTick = 0.005;

        RoadLayouts.CreateCross(city);

        for (int i = 0; i < vehicleCount; i++)
        {
            city.City.CreateVehicle();
        }

        StringBuilder fingerprint = new();
        for (int tick = 0; tick < ticks; tick++)
        {
            city.City.Update();

            foreach (Vehicle vehicle in city.City.Vehicles)
            {
                fingerprint
                    .Append(vehicle.CurrentRoad.Name).Append('|')
                    .Append(vehicle.Position.ToString("F6", CultureInfo.InvariantCulture)).Append('|')
                    .Append(vehicle.SpeedKph.ToString("F6", CultureInfo.InvariantCulture)).Append('|')
                    .Append(vehicle.LaneNumber).Append('|')
                    .Append(vehicle.IsMovingForward ? 'F' : 'B').Append('|')
                    .Append(vehicle.DisabledReason).Append(';');
            }

            fingerprint.Append('\n');
        }

        return fingerprint.ToString();
    }
}
