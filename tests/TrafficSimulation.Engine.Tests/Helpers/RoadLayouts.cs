using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;

namespace TrafficSimulation.Tests.Helpers;

/// <summary>
/// The road geometries the tests use most often.
/// </summary>
/// <remarks>
/// Building a cross or a T junction by hand in every test hides what the test is really about, so the
/// common shapes are named here. Every helper returns the roads and the junctions they created.
/// </remarks>
public static class RoadLayouts
{
    /// <summary>
    /// Builds a four way crossing: two straight roads that meet in the middle.
    /// </summary>
    /// <param name="city">City to build in.</param>
    /// <param name="sizeMetres">Distance from the crossing to each end, in metres.</param>
    /// <returns>The crossing and its four roads.</returns>
    /// <remarks>
    /// The roads are named by the direction from which a vehicle travelling the horizontal road
    /// approaches the junction: <c>West</c> leads into the crossing from the left, <c>East</c> leads
    /// away from it to the right, and so on. The vertical road runs from north to south.
    /// </remarks>
    public static CrossLayout CreateCross(TestCity city, double sizeMetres = 100)
    {
        Intersection centre = city.Node(0, 0);
        Intersection west = city.Node(-sizeMetres, 0);
        Intersection east = city.Node(sizeMetres, 0);
        Intersection north = city.Node(0, -sizeMetres);
        Intersection south = city.Node(0, sizeMetres);

        Road westToCentre = city.AddTwoWayRoad(west, centre, name: "West arm");
        Road centreToEast = city.AddTwoWayRoad(centre, east, name: "East arm");
        Road northToCentre = city.AddTwoWayRoad(north, centre, name: "North arm");
        Road centreToSouth = city.AddTwoWayRoad(centre, south, name: "South arm");

        return new CrossLayout(centre, westToCentre, centreToEast, northToCentre, centreToSouth);
    }

    /// <summary>
    /// Builds a T junction: a straight road with a single branch leaving it to the north of its end.
    /// </summary>
    /// <param name="city">City to build in.</param>
    /// <param name="sizeMetres">Length of each arm, in metres.</param>
    /// <returns>The T junction and its roads.</returns>
    /// <remarks>
    /// The main road runs from west to east; the branch leaves its eastern end and heads north.
    /// </remarks>
    public static TJunctionLayout CreateTJunction(TestCity city, double sizeMetres = 100)
    {
        Intersection west = city.Node(-sizeMetres, 0);
        Intersection junction = city.Node(0, 0);
        Intersection north = city.Node(0, -sizeMetres);

        Road mainRoad = city.AddTwoWayRoad(west, junction, name: "Main");
        Road branch = city.AddTwoWayRoad(junction, north, name: "Branch");

        return new TJunctionLayout(junction, mainRoad, branch);
    }

    /// <summary>The junction and roads of a four way crossing.</summary>
    /// <param name="Centre">The crossing itself.</param>
    /// <param name="West">Road approaching the crossing from the west.</param>
    /// <param name="East">Road leaving the crossing to the east.</param>
    /// <param name="North">Road approaching the crossing from the north.</param>
    /// <param name="South">Road leaving the crossing to the south.</param>
    public readonly record struct CrossLayout(
        Intersection Centre,
        Road West,
        Road East,
        Road North,
        Road South);

    /// <summary>The junction and roads of a T junction.</summary>
    /// <param name="Junction">The junction itself.</param>
    /// <param name="MainRoad">Road running from west to east.</param>
    /// <param name="Branch">Road leaving the junction to the north.</param>
    public readonly record struct TJunctionLayout(Intersection Junction, Road MainRoad, Road Branch);

    /// <summary>Creates a vehicle travelling the given road towards its end.</summary>
    /// <param name="city">City the vehicle is added to.</param>
    /// <param name="road">Road to drive on.</param>
    /// <param name="position">Distance from the start of the road, in metres.</param>
    /// <param name="lane">Lane number.</param>
    /// <returns>The vehicle.</returns>
    public static Vehicle VehicleDrivingForward(TestCity city, Road road, double position = 0, int lane = 1)
        => city.PlaceVehicle(road, position, lane, isMovingForward: true);

    /// <summary>Creates a vehicle travelling the given road towards its start.</summary>
    /// <param name="city">City the vehicle is added to.</param>
    /// <param name="road">Road to drive on.</param>
    /// <param name="position">Distance from the start of the road, in metres.</param>
    /// <param name="lane">Lane number.</param>
    /// <returns>The vehicle.</returns>
    public static Vehicle VehicleDrivingBackward(TestCity city, Road road, double? position = null, int lane = 1)
        => city.PlaceVehicle(road, position ?? road.Length, lane, isMovingForward: false);
}
