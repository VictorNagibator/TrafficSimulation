using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.Engine.Domain;

/// <summary>
/// A node of the road network: the place where one or more roads meet.
/// </summary>
/// <remarks>
/// <para>
/// Intersections own the relations between the roads that touch them. A road stores its two
/// end points and simply tells both intersections that it is attached to them; the intersections
/// keep the roads in <see cref="IncomingRoads"/> (roads that lead <em>into</em> this node) and
/// <see cref="OutgoingRoads"/> (roads that lead <em>out of</em> it). One-way roads appear in only
/// one of the two sets, two-way roads appear in both.
/// </para>
/// <para>
/// The node is drawn as a black square whose side equals the width of the widest attached road,
/// so the geometry of the square is derived, never stored.
/// </para>
/// </remarks>
public sealed class Intersection
{
    /// <summary>Creates an intersection at the origin.</summary>
    public Intersection()
        : this(Vector2.Zero)
    {
    }

    /// <summary>Creates an intersection at the given position.</summary>
    /// <param name="position">Position of the node, in metres.</param>
    public Intersection(Vector2 position) => Position = position;

    /// <summary>Position of the node, in metres.</summary>
    public Vector2 Position { get; internal set; }

    /// <summary>Horizontal coordinate of the node, in metres.</summary>
    public double X
    {
        get => Position.X;
        internal set => Position = new Vector2(value, Position.Y);
    }

    /// <summary>Vertical coordinate of the node, in metres.</summary>
    public double Y
    {
        get => Position.Y;
        internal set => Position = new Vector2(Position.X, value);
    }

    /// <summary>Roads that lead into this intersection.</summary>
    public HashSet<Road> IncomingRoads { get; } = [];

    /// <summary>Roads that lead out of this intersection.</summary>
    public HashSet<Road> OutgoingRoads { get; } = [];

    /// <summary>All roads attached to this intersection, in no particular order.</summary>
    public IEnumerable<Road> AllRoads => IncomingRoads.Concat(OutgoingRoads).Distinct();

    /// <summary>
    /// Half of the width of the widest road attached to this intersection, in metres.
    /// </summary>
    /// <remarks>
    /// This is the distance from the centre of the intersection to the edge of the widest road and
    /// is used to shorten drawn roads so that they stop at the intersection instead of crossing it.
    /// </remarks>
    public double HalfWidth => WidestRoadWidth / 2;

    /// <summary>Width of the widest road attached to this intersection, in metres.</summary>
    public double WidestRoadWidth
        => AllRoads.Aggregate(0d, (widest, road) => Math.Max(widest, road.Width));

    /// <summary>
    /// Representative direction of the intersection, in radians.
    /// </summary>
    /// <remarks>
    /// Intersections are rendered as squares aligned with the streets they join. When the attached
    /// roads all share a single axis (a straight street, a T junction, a cross) that axis is used.
    /// When the roads form several unrelated axes there is no meaningful orientation, so the
    /// direction of the very first road is used instead.
    /// </remarks>
    public double Angle
    {
        get
        {
            // Only the widest roads take part: they define the visual footprint of the node.
            List<double> axes = WidestRoads.Select(road => road.Angle).ToList();
            if (axes.Count == 0)
            {
                return 0;
            }

            double tolerance = SimulationConstants.AngleToleranceRadians;
            for (int i = 0; i < axes.Count; i++)
            {
                bool allShareAxis = true;
                for (int j = 0; j < axes.Count; j++)
                {
                    if (i != j && !Angles.ShareAxis(axes[i], axes[j], tolerance))
                    {
                        allShareAxis = false;
                        break;
                    }
                }

                if (allShareAxis)
                {
                    return axes[i];
                }
            }

            // The roads point in several unrelated directions: fall back to a stable reference.
            return axes[0];
        }
    }

    /// <summary>Quality of the best road attached to this intersection, in the range (0, 1].</summary>
    /// <remarks>
    /// Used as the opacity of the drawn node, so a degraded junction looks as faded as its roads.
    /// </remarks>
    public double BestRoadQuality
        => AllRoads.Aggregate(0d, (best, road) => Math.Max(best, road.Quality));

    /// <summary>Roads attached to this intersection that are as wide as the intersection itself.</summary>
    private IEnumerable<Road> WidestRoads
    {
        get
        {
            double widest = WidestRoadWidth;
            return AllRoads.Where(road => Math.Abs(road.Width - widest) < 1e-9);
        }
    }

    /// <summary>Determines whether this intersection has no roads attached to it at all.</summary>
    /// <returns><see langword="true"/> when the node is an orphan.</returns>
    public bool HasNoRoads() => IncomingRoads.Count == 0 && OutgoingRoads.Count == 0;

    /// <summary>
    /// Registers a road with this intersection, deciding on which side of the traffic it belongs.
    /// </summary>
    /// <param name="road">Road that has this intersection as one of its end points.</param>
    internal void AddRoad(Road road)
    {
        ArgumentNullException.ThrowIfNull(road);

        if (road.Type == RoadType.TwoWay)
        {
            IncomingRoads.Add(road);
            OutgoingRoads.Add(road);
            return;
        }

        // A one-way road leads out of its start and into its end.
        if (road.StartIntersection == this)
        {
            OutgoingRoads.Add(road);
        }
        else
        {
            IncomingRoads.Add(road);
        }
    }

    /// <summary>Removes a road from both sets of this intersection.</summary>
    /// <param name="road">Road to detach.</param>
    internal void RemoveRoad(Road road)
    {
        ArgumentNullException.ThrowIfNull(road);

        IncomingRoads.Remove(road);
        OutgoingRoads.Remove(road);
    }

    /// <summary>Re-evaluates which set a road belongs to after its type has changed.</summary>
    /// <param name="road">Road whose type changed.</param>
    internal void RefreshRoad(Road road)
    {
        RemoveRoad(road);
        AddRoad(road);
    }
}
