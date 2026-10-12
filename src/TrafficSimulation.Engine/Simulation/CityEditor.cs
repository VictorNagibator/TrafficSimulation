using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.Engine.Simulation;

/// <summary>Result of splitting a road in two.</summary>
/// <param name="Original">The road that was replaced.</param>
/// <param name="First">The piece that runs from the original start to the split point.</param>
/// <param name="Second">The piece that runs from the split point to the original end.</param>
/// <param name="Intersection">The junction created between the two pieces.</param>
public readonly record struct RoadSplit(Road Original, Road First, Road Second, Intersection Intersection);

/// <summary>
/// Structural edits performed by the map editor.
/// </summary>
/// <remarks>
/// <para>
/// The editor knows how to turn a user gesture into a consistent change of the road network. Keeping
/// this logic here rather than in the window's event handlers is what makes it possible to unit test
/// map editing: the services below only need a <see cref="City"/>, never a canvas.
/// </para>
/// <para>
/// Every method keeps the invariants of the network intact: a road is always registered with both of
/// its end points, a light always controls a road that exists, and a vehicle always stands on a road
/// that exists and drives in a direction that road allows.
/// </para>
/// </remarks>
public sealed class CityEditor
{
    private readonly City _city;

    /// <summary>Creates an editor for a city.</summary>
    /// <param name="city">City to edit.</param>
    public CityEditor(City city) => _city = city ?? throw new ArgumentNullException(nameof(city));

    /// <summary>
    /// Determines whether a new road may be built along the given segment.
    /// </summary>
    /// <param name="start">Start of the proposed road.</param>
    /// <param name="end">End of the proposed road.</param>
    /// <returns>
    /// <see langword="true"/> when the segment does not cross an existing road. Roads that merely
    /// share an end point with the new one are fine; roads that would be cut through are not.
    /// </returns>
    public bool CanCreateRoad(Intersection start, Intersection end)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(end);

        if (start == end)
        {
            return false;
        }

        LineSegment candidate = new(start.Position, end.Position);
        return !_city.Roads.Any(road => Crosses(road, candidate));
    }

    /// <summary>
    /// Creates a road between two points and adds it to the city.
    /// </summary>
    /// <param name="start">Intersection where the road begins.</param>
    /// <param name="end">Intersection where the road ends.</param>
    /// <returns>The new road.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the road would cross an existing one.</exception>
    public Road CreateRoad(Intersection start, Intersection end)
    {
        if (!CanCreateRoad(start, end))
        {
            throw new InvalidOperationException("The road would cross an existing road or has no length.");
        }

        Road road = new(start, end)
        {
            LanesPerDirection = _city.Settings.DefaultLanesPerDirection,
        };

        _city.AddRoad(road);
        return road;
    }

    /// <summary>Removes a road from the city.</summary>
    /// <param name="road">Road to remove.</param>
    /// <returns><see langword="true"/> when the road was part of the city.</returns>
    public bool RemoveRoad(Road road) => _city.RemoveRoad(road);

    /// <summary>
    /// Splits a road in two at the given junction, so that the map can grow new branches anywhere
    /// along an existing street.
    /// </summary>
    /// <param name="road">Road to split.</param>
    /// <param name="splitPoint">Junction that becomes the meeting point of the two new pieces.</param>
    /// <returns>The two new roads and the junction between them.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the split point does not lie strictly inside the road, or when the road is not part
    /// of the city.
    /// </exception>
    /// <remarks>
    /// Vehicles and traffic lights are redistributed over the two pieces: a vehicle keeps its
    /// absolute position on the ground, so its coordinate is recomputed relative to the piece it
    /// ends up on, and a light stays at the junction it was standing at.
    /// </remarks>
    public RoadSplit SplitRoad(Road road, Intersection splitPoint)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(splitPoint);

        if (!_city.Roads.Contains(road))
        {
            throw new InvalidOperationException("The road is not part of the city.");
        }

        if (road.Length <= double.Epsilon)
        {
            throw new InvalidOperationException("A road of zero length cannot be split.");
        }

        double splitDistance = road.StartIntersection.Position.DistanceTo(splitPoint.Position);
        if (splitDistance <= double.Epsilon || splitDistance >= road.Length - double.Epsilon)
        {
            throw new InvalidOperationException("The split point must lie strictly inside the road.");
        }

        Road first = CloneSegment(road, road.StartIntersection, splitPoint);
        Road second = CloneSegment(road, splitPoint, road.EndIntersection);

        // Snapshot first: the original road is about to be taken off the map, together with the
        // vehicles standing on it.
        List<Vehicle> vehicles = [.. road.Vehicles];

        _city.RemoveRoad(road);

        // Vehicles keep their absolute position on the ground, so their coordinate is recomputed
        // relative to the piece they end up on.
        foreach (Vehicle vehicle in vehicles)
        {
            bool onFirstPiece = vehicle.Position <= splitDistance;
            Road target = onFirstPiece ? first : second;

            if (!onFirstPiece)
            {
                vehicle.Position -= splitDistance;
            }

            vehicle.CurrentRoad = target;
            vehicle.NextIntersection = vehicle.IsMovingForward ? target.EndIntersection : target.StartIntersection;
            _city.AddVehicle(vehicle);
            vehicle.RefreshNextRoad();
        }

        _city.AddRoad(first);
        _city.AddRoad(second);

        // A light that stood at an end point of the original road keeps controlling that end point.
        _city.TrafficLights.RedirectRoad(road, first, road.StartIntersection);
        _city.TrafficLights.RedirectRoad(road, second, road.EndIntersection);

        return new RoadSplit(road, first, second, splitPoint);
    }

    /// <summary>
    /// Finds the road that passes through a point, which is what a click on the map resolves to.
    /// </summary>
    /// <param name="point">Point in metres.</param>
    /// <param name="toleranceMetres">Maximum distance from the road centre line that still counts.</param>
    /// <returns>The closest matching road, or <see langword="null"/> when the point hits nothing.</returns>
    public Road? FindRoadAt(Vector2 point, double toleranceMetres)
    {
        Road? closest = null;
        double closestDistance = double.MaxValue;

        foreach (Road road in _city.Roads)
        {
            Vector2 projection = road.Segment.ClosestPointOnLine(point);
            double distance = projection.DistanceTo(point);

            if (distance <= toleranceMetres && distance < closestDistance)
            {
                closestDistance = distance;
                closest = road;
            }
        }

        return closest;
    }

    /// <summary>
    /// Finds the intersection located at a point, which is what a click on a junction resolves to.
    /// </summary>
    /// <param name="point">Point in metres.</param>
    /// <param name="toleranceMetres">Maximum distance that still counts as a hit.</param>
    /// <returns>The matching intersection, or <see langword="null"/> when the point hits nothing.</returns>
    public Intersection? FindIntersectionAt(Vector2 point, double toleranceMetres)
    {
        Intersection? closest = null;
        double closestDistance = double.MaxValue;

        foreach (Intersection intersection in _city.Intersections)
        {
            double distance = intersection.Position.DistanceTo(point);
            if (distance <= toleranceMetres && distance < closestDistance)
            {
                closestDistance = distance;
                closest = intersection;
            }
        }

        return closest;
    }

    /// <summary>
    /// Returns the closest point on a road to the given position, used when the user clicks on the
    /// body of a road to start a new branch there.
    /// </summary>
    /// <param name="road">Road to project onto.</param>
    /// <param name="point">Point in metres.</param>
    /// <returns>The projection of <paramref name="point"/> onto the centre line of the road.</returns>
    public static Vector2 ProjectOntoRoad(Road road, Vector2 point)
    {
        ArgumentNullException.ThrowIfNull(road);

        Vector2 projected = road.Segment.ClosestPointOnLine(point);

        // Keep the point strictly inside the road, so that splitting it produces two real pieces.
        double distance = road.StartIntersection.Position.DistanceTo(projected);
        double margin = Math.Min(1e-3, road.Length / 3);
        distance = Math.Clamp(distance, margin, road.Length - margin);

        return road.StartIntersection.Position + Vector2.FromAngle(road.Angle, distance);
    }

    /// <summary>Builds a new road that copies the settings of an existing one.</summary>
    /// <param name="source">Road to copy settings from.</param>
    /// <param name="start">Start of the new road.</param>
    /// <param name="end">End of the new road.</param>
    /// <returns>The new, unregistered road.</returns>
    private static Road CloneSegment(Road source, Intersection start, Intersection end) => new(start, end)
    {
        Name = source.Name,
        Type = source.Type,
        LanesPerDirection = source.LanesPerDirection,
        SpeedLimitKph = source.SpeedLimitKph,
        Quality = source.Quality,
    };

    /// <summary>
    /// Determines whether a proposed road would cut through an existing one.
    /// </summary>
    /// <remarks>
    /// Roads that share an end point with the proposal are allowed, because that is exactly what
    /// joining two roads means; only genuine crossings are rejected. The check is delegated to the
    /// geometry, which applies the same rule.
    /// </remarks>
    private static bool Crosses(Road existing, LineSegment candidate)
        => existing.Segment.Intersects(candidate);
}
