using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>Tests for <see cref="Vector2"/> and <see cref="LineSegment"/>.</summary>
public class GeometryTests
{
    [Fact]
    public void FromAngle_ProducesAUnitVectorInTheGivenDirection()
    {
        Vector2 east = Vector2.FromAngle(0);
        Vector2 south = Vector2.FromAngle(Math.PI / 2);
        Vector2 north = Vector2.FromAngle(-Math.PI / 2);

        Assert.Equal(1, east.X, 10);
        Assert.Equal(0, east.Y, 10);

        // The Y axis points down, so a positive angle moves downwards.
        Assert.Equal(0, south.X, 10);
        Assert.Equal(1, south.Y, 10);

        Assert.Equal(0, north.X, 10);
        Assert.Equal(-1, north.Y, 10);
    }

    [Fact]
    public void FromAngle_ScalesToTheRequestedLength()
    {
        Vector2 vector = Vector2.FromAngle(Math.PI / 3, 10);

        Assert.Equal(10, vector.DistanceTo(Vector2.Zero), 10);
    }

    [Fact]
    public void DistanceTo_ComputesEuclideanDistance()
    {
        Vector2 origin = new(1, 2);

        Assert.Equal(5, origin.DistanceTo(new Vector2(4, 6)), 10);
        Assert.Equal(0, origin.DistanceTo(origin), 10);
    }

    [Fact]
    public void ArithmeticOperatorsWorkComponentWise()
    {
        Vector2 a = new(1, 2);
        Vector2 b = new(3, 4);

        Assert.Equal(new Vector2(4, 6), a + b);
        Assert.Equal(new Vector2(-2, -2), a - b);
        Assert.Equal(new Vector2(2, 4), a * 2);
        Assert.Equal(new Vector2(2, 4), 2 * a);
    }

    [Fact]
    public void Perpendicular_RotatesTheVectorByNinetyDegrees()
    {
        // Rotating east by +90 degrees points south, which is (0, 1) because Y grows downwards.
        Assert.Equal(new Vector2(0, 1), new Vector2(1, 0).Perpendicular());
    }

    [Fact]
    public void Segment_ReportsItsLengthAndAngle()
    {
        LineSegment segment = new(Vector2.Zero, new Vector2(3, 4));

        Assert.Equal(5, segment.Length, 10);
        Assert.Equal(Math.Atan2(4, 3), segment.Angle, 10);
    }

    [Fact]
    public void PointAt_WalksAlongTheSegment()
    {
        LineSegment segment = new(new Vector2(0, 0), new Vector2(10, 0));

        Vector2 middle = segment.PointAt(5);

        Assert.Equal(5, middle.X, 10);
        Assert.Equal(0, middle.Y, 10);
    }

    [Fact]
    public void Intersects_DetectsACrossing()
    {
        LineSegment horizontal = new(new Vector2(-10, 0), new Vector2(10, 0));
        LineSegment vertical = new(new Vector2(0, -10), new Vector2(0, 10));

        Assert.True(horizontal.Intersects(vertical));
        Assert.True(vertical.Intersects(horizontal));
    }

    [Fact]
    public void Intersects_ReturnsFalseWhenTheSegmentsDoNotReachEachOther()
    {
        LineSegment horizontal = new(new Vector2(-10, 0), new Vector2(-5, 0));
        LineSegment vertical = new(new Vector2(5, -10), new Vector2(5, 10));

        Assert.False(horizontal.Intersects(vertical));
    }

    [Fact]
    public void Intersects_ReturnsFalseForParallelSegments()
    {
        LineSegment first = new(new Vector2(0, 0), new Vector2(10, 0));
        LineSegment second = new(new Vector2(0, 5), new Vector2(10, 5));

        Assert.False(first.Intersects(second));
    }

    [Fact]
    public void Intersects_ReturnsFalseForSegmentsThatOnlyShareAnEndPoint()
    {
        // Two roads that meet at a junction must not count as crossing each other, otherwise the
        // map editor would refuse to build a network at all.
        LineSegment first = new(new Vector2(0, 0), new Vector2(10, 0));
        LineSegment second = new(new Vector2(10, 0), new Vector2(10, 10));

        Assert.False(first.Intersects(second));
    }

    [Fact]
    public void ClosestPointOnLine_ProjectsPerpendicularly()
    {
        LineSegment segment = new(new Vector2(0, 0), new Vector2(10, 0));

        Vector2 projected = segment.ClosestPointOnLine(new Vector2(4, 7));

        Assert.Equal(4, projected.X, 10);
        Assert.Equal(0, projected.Y, 10);
    }

    [Fact]
    public void ClosestPointOnLine_ExtendsBeyondTheSegmentWhenNeeded()
    {
        LineSegment segment = new(new Vector2(0, 0), new Vector2(10, 0));

        // The projection is onto the infinite line, which is what the map editor needs in order to
        // find the road nearest to a click.
        Vector2 projected = segment.ClosestPointOnLine(new Vector2(15, 3));

        Assert.Equal(15, projected.X, 10);
    }

    [Fact]
    public void ClosestPointOnLine_HandlesADegenerateSegment()
    {
        LineSegment pointLike = new(new Vector2(3, 3), new Vector2(3, 3));

        Assert.Equal(new Vector2(3, 3), pointLike.ClosestPointOnLine(new Vector2(9, 9)));
    }
}
