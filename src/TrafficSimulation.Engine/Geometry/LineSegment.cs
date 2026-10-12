namespace TrafficSimulation.Engine.Geometry;

/// <summary>
/// A straight line segment defined by its two end points.
/// </summary>
/// <param name="Start">First end point.</param>
/// <param name="End">Second end point.</param>
public readonly record struct LineSegment(Vector2 Start, Vector2 End)
{
    /// <summary>Length of the segment.</summary>
    public double Length => Start.DistanceTo(End);

    /// <summary>Direction of the segment, in radians.</summary>
    public double Angle => Math.Atan2(End.Y - Start.Y, End.X - Start.X);

    /// <summary>Point located at <paramref name="distance"/> from <see cref="Start"/>.</summary>
    /// <param name="distance">Distance along the segment.</param>
    public Vector2 PointAt(double distance) => Start + Vector2.FromAngle(Angle, distance);

    /// <summary>
    /// Determines whether this segment properly crosses another one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parallel segments and segments that merely touch at an end point are not considered crossings,
    /// because the map editor uses this test to refuse roads that would cut through an existing
    /// street: roads that share a junction are allowed, roads that overlap are not.
    /// </para>
    /// <para>
    /// Because segments are compared exactly, "touching" means the two end points are bitwise equal.
    /// The map editor places junctions from snapped positions, so that is the case in practice; a
    /// click in the middle of a road is turned into a real junction by splitting the road first.
    /// </para>
    /// </remarks>
    /// <param name="other">The segment to test against.</param>
    /// <returns><see langword="true"/> when the two segments cross.</returns>
    public bool Intersects(LineSegment other)
    {
        if (SharesEndPointWith(other))
        {
            return false;
        }

        Vector2 p = Start;
        Vector2 q = other.Start;
        Vector2 r = End - Start;
        Vector2 s = other.End - other.Start;

        double denominator = Cross(r, s);

        // Parallel (or degenerate) segments never count as an intersection.
        if (Math.Abs(denominator) < double.Epsilon)
        {
            return false;
        }

        Vector2 qp = q - p;
        double t = Cross(qp, s) / denominator;
        double u = Cross(qp, r) / denominator;

        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    /// <summary>Determines whether the two segments have an end point in common.</summary>
    /// <param name="other">The segment to test against.</param>
    /// <returns><see langword="true"/> when at least one end point is shared.</returns>
    public bool SharesEndPointWith(LineSegment other)
        => Start == other.Start || Start == other.End || End == other.Start || End == other.End;

    /// <summary>
    /// Projects <paramref name="point"/> onto the infinite line that contains this segment.
    /// </summary>
    /// <param name="point">Point to project.</param>
    /// <returns>The foot of the perpendicular from <paramref name="point"/> to the line.</returns>
    public Vector2 ClosestPointOnLine(Vector2 point)
    {
        Vector2 direction = End - Start;
        double lengthSquared = (direction.X * direction.X) + (direction.Y * direction.Y);

        if (lengthSquared < double.Epsilon)
        {
            return Start;
        }

        double t = (((point.X - Start.X) * direction.X) + ((point.Y - Start.Y) * direction.Y)) / lengthSquared;
        return Start + (direction * t);
    }

    /// <summary>Two-dimensional cross product of two vectors.</summary>
    private static double Cross(Vector2 left, Vector2 right) => (left.X * right.Y) - (left.Y * right.X);
}
