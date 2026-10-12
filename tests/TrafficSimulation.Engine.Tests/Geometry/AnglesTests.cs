using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.Tests.Geometry;

/// <summary>Tests for <see cref="Angles"/>.</summary>
public class AnglesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(Math.PI / 4, Math.PI / 4)]
    [InlineData(-Math.PI / 4, -Math.PI / 4)]
    [InlineData(Math.PI, Math.PI)]
    public void Normalize_LeavesAnglesInsideTheRangeUntouched(double input, double expected)
        => Assert.Equal(expected, Angles.Normalize(input), 10);

    [Theory]
    [InlineData(3 * Math.PI / 2, -Math.PI / 2)]
    [InlineData(-3 * Math.PI / 2, Math.PI / 2)]
    [InlineData(2 * Math.PI, 0)]
    [InlineData(5 * Math.PI / 2, Math.PI / 2)]
    public void Normalize_WrapsAnglesOutsideTheRange(double input, double expected)
        => Assert.Equal(expected, Angles.Normalize(input), 10);

    [Fact]
    public void Difference_MeasuresTheShortestSignedTurn()
    {
        // Turning clockwise from east (0) to south (pi/2) is a positive quarter turn.
        Assert.Equal(Math.PI / 2, Angles.Difference(0, Math.PI / 2), 10);

        // Turning anticlockwise is negative.
        Assert.Equal(-Math.PI / 2, Angles.Difference(0, -Math.PI / 2), 10);
    }

    [Fact]
    public void Difference_WrapsAroundTheFullTurn()
    {
        // From just below a full turn to just above zero is a small positive step, not a large
        // negative one.
        double difference = Angles.Difference(2 * Math.PI - 0.1, 0.1);

        Assert.Equal(0.2, difference, 10);
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, Math.PI, true)]
    [InlineData(0, 0.05, true)]
    [InlineData(0, Math.PI / 2, false)]
    [InlineData(0, -Math.PI / 2, false)]
    public void ShareAxis_TreatsParallelAndOppositeDirectionsAsOneAxis(
        double first,
        double second,
        bool expected)
        => Assert.Equal(expected, Angles.ShareAxis(first, second, tolerance: 0.1));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(Math.PI / 2, 90)]
    [InlineData(Math.PI, 180)]
    [InlineData(-Math.PI / 2, -90)]
    public void DegreeConversionsAreInverses(double radians, double degrees)
    {
        Assert.Equal(degrees, Angles.ToDegrees(radians), 10);
        Assert.Equal(radians, Angles.ToRadians(degrees), 10);
    }
}
