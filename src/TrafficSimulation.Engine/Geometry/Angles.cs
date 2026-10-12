namespace TrafficSimulation.Engine.Geometry;

/// <summary>
/// Helpers for working with angles expressed in radians.
/// </summary>
/// <remarks>
/// The simulation uses the screen convention: the X axis points right, the Y axis points
/// <em>down</em>, and angles grow clockwise. Because of that a positive turn is a turn to the
/// driver's left, which is exactly how the road-direction rules in <c>Road</c> read it.
/// </remarks>
public static class Angles
{
    /// <summary>
    /// Wraps an angle into the half-open interval <c>(-pi, pi]</c>.
    /// </summary>
    /// <param name="radians">Angle to wrap.</param>
    /// <returns>The equivalent angle closest to zero.</returns>
    public static double Normalize(double radians)
    {
        const double fullTurn = 2 * Math.PI;
        double normalized = radians % fullTurn;
        if (normalized > Math.PI)
        {
            normalized -= fullTurn;
        }
        else if (normalized <= -Math.PI)
        {
            normalized += fullTurn;
        }

        return normalized;
    }

    /// <summary>
    /// Returns the signed smallest rotation that takes <paramref name="from"/> to
    /// <paramref name="to"/>, in the interval <c>(-pi, pi]</c>.
    /// </summary>
    /// <param name="from">Reference direction.</param>
    /// <param name="to">Target direction.</param>
    public static double Difference(double from, double to) => Normalize(to - from);

    /// <summary>
    /// Determines whether two directions point along the same axis, that is whether they are
    /// parallel or anti-parallel, within a tolerance.
    /// </summary>
    /// <param name="first">First direction, in radians.</param>
    /// <param name="second">Second direction, in radians.</param>
    /// <param name="tolerance">Maximum deviation from the axis, in radians.</param>
    /// <returns><see langword="true"/> when both directions share a common axis.</returns>
    public static bool ShareAxis(double first, double second, double tolerance)
    {
        double difference = Math.Abs(Difference(first, second));

        // |d| near 0  -> parallel, |d| near pi -> anti-parallel (the same street, opposite ends).
        return difference <= tolerance || Math.Abs(difference - Math.PI) <= tolerance;
    }

    /// <summary>Converts radians to degrees.</summary>
    /// <param name="radians">Angle in radians.</param>
    public static double ToDegrees(double radians) => radians * 180.0 / Math.PI;

    /// <summary>Converts degrees to radians.</summary>
    /// <param name="degrees">Angle in degrees.</param>
    public static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
