using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;
using TrafficSimulation.Tests.Helpers;

namespace TrafficSimulation.Tests.Domain;

/// <summary>
/// Tests for the junction node: how <see cref="Intersection"/> derives its size and orientation from
/// the roads attached to it, and how <see cref="IntersectionIndex"/> guarantees that one place
/// is represented by one node.
/// </summary>
public class IntersectionTests
{
    [Theory]
    [InlineData(1, 7.5)]
    [InlineData(2, 15)]
    [InlineData(3, 22.5)]
    public void WidestRoadWidthAndHalfWidthFollowTheWidestAttachedRoad(int lanesPerDirection, double expectedWidth)
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        city.AddTwoWayRoad(junction, city.Node(100, 0), lanesPerDirection);

        // A two-way road paves two directions of `lanesPerDirection` lanes at 3.75 m each, so a
        // three-lane road is 22.5 m wide. The node is drawn as a square of exactly that width, and
        // half the width is what shortens the drawn roads so they stop at the pavement edge.
        Assert.Equal(expectedWidth, junction.WidestRoadWidth, 10);
        Assert.Equal(expectedWidth / 2, junction.HalfWidth, 10);
    }

    [Fact]
    public void WidestRoadWidthIgnoresNarrowerAttachedRoads()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        city.AddTwoWayRoad(junction, city.Node(100, 0), lanesPerDirection: 3);
        city.AddOneWayRoad(junction, city.Node(0, 100));

        // The node has to cover its widest pavement; the single-lane arm must not shrink the square,
        // or the drawn junction would sit inside the three-lane road it is supposed to join.
        Assert.Equal(22.5, junction.WidestRoadWidth, 10);
        Assert.Equal(11.25, junction.HalfWidth, 10);
    }

    [Fact]
    public void AllRoadsListsEachAttachedRoadExactlyOnce()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);

        // Every arm of the cross is two-way, so it is stored as incoming and as outgoing at the same
        // time. The node still joins four distinct roads, and each of them must be reported once or
        // the square would be assembled from duplicated pavement.
        Assert.Equal(4, cross.Centre.AllRoads.Count());
        Assert.Contains(cross.West, cross.Centre.IncomingRoads);
        Assert.Contains(cross.West, cross.Centre.OutgoingRoads);
        Assert.Single(cross.Centre.AllRoads.Where(road => road == cross.West));
    }

    [Fact]
    public void AngleOfASingleStraightStreetIsTheDirectionOfThatStreet()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        city.AddTwoWayRoad(city.Node(-100, 0), junction);
        city.AddTwoWayRoad(junction, city.Node(100, 0));

        // Both arms run along the X axis, so the node must be axis aligned with the street instead
        // of taking the direction of whichever arm happens to be enumerated first.
        Assert.Equal(0, junction.Angle, 10);
    }

    [Fact]
    public void AngleOfACrossIsAlignedWithOneOfItsAxes()
    {
        TestCity city = new();
        RoadLayouts.CrossLayout cross = RoadLayouts.CreateCross(city);
        double tolerance = SimulationConstants.AngleToleranceRadians;

        // A cross joins a horizontal and a vertical street, so its square may point along either
        // axis, but never along the diagonal between them.
        Assert.True(
            Angles.ShareAxis(cross.Centre.Angle, cross.West.Angle, tolerance)
            || Angles.ShareAxis(cross.Centre.Angle, cross.North.Angle, tolerance));
        Assert.False(Angles.ShareAxis(cross.Centre.Angle, Math.PI / 4, tolerance));
    }

    [Fact]
    public void AngleOfUnrelatedStreetsIsFiniteAndStable()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);

        // Three streets that do not share a common axis: east, about 50 degrees and about 80 degrees.
        city.AddTwoWayRoad(junction, city.Node(100, 0));
        city.AddTwoWayRoad(junction, city.Node(64, 77));
        city.AddTwoWayRoad(junction, city.Node(17, 98));

        double first = junction.Angle;
        double second = junction.Angle;

        // There is no meaningful orientation here, but the fallback still has to produce a real
        // number that does not change between reads, otherwise the node would jitter on screen.
        Assert.True(double.IsFinite(first));
        Assert.Equal(first, second, 10);
    }

    [Fact]
    public void AngleOfAnOrphanJunctionIsZero()
    {
        TestCity city = new();
        Intersection orphan = city.Node(50, 50);

        // Without a street to align to there is no direction; zero keeps the node from rotating.
        Assert.Equal(0, orphan.Angle, 10);
    }

    [Fact]
    public void BestRoadQualityIsTheQualityOfTheBestAttachedRoad()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        Road fresh = city.AddTwoWayRoad(junction, city.Node(100, 0));
        Road worn = city.AddTwoWayRoad(junction, city.Node(0, 100));

        // A brand new road keeps the node fully opaque.
        Assert.Equal(1.0, junction.BestRoadQuality, 10);

        fresh.Quality = 0.25;
        worn.Quality = 0.6;

        // The best road wins, so a junction looks as good as its best-preserved approach rather than
        // as faded as its worst one.
        Assert.Equal(0.6, junction.BestRoadQuality, 10);
    }

    [Fact]
    public void HasNoRoadsReflectsAttachmentAndRemoval()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);

        Assert.True(junction.HasNoRoads());

        Road road = city.AddOneWayRoad(city.Node(-100, 0), junction);
        Assert.False(junction.HasNoRoads());

        // Losing the last road turns the junction back into an orphan, which the renderer can skip.
        city.City.RemoveRoad(road);
        Assert.True(junction.HasNoRoads());
    }

    [Fact]
    public void OneWayAndTwoWayRoadsBelongToTheCorrectRoadSets()
    {
        TestCity city = new();
        Intersection start = city.Node(0, 0);
        Intersection end = city.Node(100, 0);

        Road twoWay = city.AddTwoWayRoad(start, end);
        Road oneWay = city.AddOneWayRoad(start, end);

        // A two-way road is an approach and a way out at both of its ends.
        Assert.Contains(twoWay, end.IncomingRoads);
        Assert.Contains(twoWay, end.OutgoingRoads);

        // The one-way road only carries traffic from start to end, so it can be entered at the start
        // and left at the end, and the opposite sets must stay empty.
        Assert.Contains(oneWay, start.OutgoingRoads);
        Assert.DoesNotContain(oneWay, start.IncomingRoads);
        Assert.Contains(oneWay, end.IncomingRoads);
        Assert.DoesNotContain(oneWay, end.OutgoingRoads);
    }

    [Fact]
    public void GetOrCreateReturnsOneJunctionPerPosition()
    {
        IntersectionIndex index = new();

        Intersection placed = index.GetOrCreate(new Vector2(10, 20));

        // The very same place must always resolve to the very same instance, because roads recognise
        // their meeting point by reference and would otherwise build a disconnected network.
        Assert.Same(placed, index.GetOrCreate(new Vector2(10, 20)));

        // A place that was never created is unknown; that null answer is the editor's signal to make it.
        Assert.Null(index.Find(new Vector2(20, 10)));

        Assert.NotSame(placed, index.GetOrCreate(new Vector2(20, 10)));
        Assert.Equal(2, index.Count);
    }

    [Fact]
    public void PositionsLessThanAMillimetreApartAreTheSameJunction()
    {
        IntersectionIndex index = new();
        Intersection placed = index.GetOrCreate(new Vector2(10, 20));

        Intersection near = index.GetOrCreate(new Vector2(10.0004, 20.0004));

        // A click is never pixel exact, so positions are compared after rounding to millimetres. Two
        // points inside the same millimetre must snap to one node, not create a barely separated
        // duplicate that roads would fail to share.
        Assert.Same(placed, near);
        Assert.Equal(1, index.Count);
    }

    [Fact]
    public void ClearRemovesEveryIndexedJunction()
    {
        IntersectionIndex index = new();
        index.GetOrCreate(new Vector2(10, 20));
        index.GetOrCreate(new Vector2(30, 40));

        index.Clear();

        // Clearing the city must clear its spatial index too, or stale junctions would survive a new
        // map and keep the roads of the previous one attached to them.
        Assert.Equal(0, index.Count);
        Assert.Null(index.Find(new Vector2(10, 20)));
    }
}
