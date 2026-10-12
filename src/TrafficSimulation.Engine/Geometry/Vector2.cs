namespace TrafficSimulation.Engine.Geometry;

/// <summary>
/// A point in the two-dimensional plane.
/// </summary>
/// <remarks>
/// The engine deliberately avoids <c>System.Windows.Point</c> (and every other WPF type) so that
/// it can be compiled and unit tested on any platform. Two-dimensional geometry here is a plain
/// value type with the handful of helpers the simulation actually needs.
/// </remarks>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate.</param>
public readonly record struct Vector2(double X, double Y)
{
    /// <summary>The origin of the coordinate system.</summary>
    public static Vector2 Zero => new(0, 0);

    /// <summary>Adds two vectors component-wise.</summary>
    public static Vector2 operator +(Vector2 left, Vector2 right) => new(left.X + right.X, left.Y + right.Y);

    /// <summary>Subtracts two vectors component-wise.</summary>
    public static Vector2 operator -(Vector2 left, Vector2 right) => new(left.X - right.X, left.Y - right.Y);

    /// <summary>Multiplies a vector by a scalar.</summary>
    public static Vector2 operator *(Vector2 vector, double factor) => new(vector.X * factor, vector.Y * factor);

    /// <summary>Multiplies a vector by a scalar.</summary>
    public static Vector2 operator *(double factor, Vector2 vector) => vector * factor;

    /// <summary>Creates a vector pointing in <paramref name="radians"/> with the given length.</summary>
    /// <param name="radians">Direction of the vector, in radians, measured clockwise from the +X axis.</param>
    /// <param name="length">Length of the resulting vector.</param>
    public static Vector2 FromAngle(double radians, double length = 1)
        => new(Math.Cos(radians) * length, Math.Sin(radians) * length);

    /// <summary>Returns the vector rotated by +90 degrees (clockwise on screen).</summary>
    public Vector2 Perpendicular() => new(-Y, X);

    /// <summary>Euclidean distance between two points.</summary>
    /// <param name="other">The other point.</param>
    public double DistanceTo(Vector2 other)
    {
        double dx = other.X - X;
        double dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
