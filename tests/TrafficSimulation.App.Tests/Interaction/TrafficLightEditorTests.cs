using TrafficSimulation.App.Interaction;
using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Randomness;
using TrafficSimulation.Engine.Simulation;

namespace TrafficSimulation.Tests.Interaction;

/// <summary>
/// Tests for <see cref="TrafficLightEditor"/>, the logic behind the intersection settings window.
/// </summary>
public class TrafficLightEditorTests
{
    [Fact]
    public void NoRoadSelectedMeansNothingCanBeEdited()
    {
        (City city, Intersection junction, Road _) = CreateJunction();

        TrafficLightEditor editor = new(city, junction);

        Assert.Null(editor.SelectedRoad);
        Assert.Null(editor.SelectedLight);
        Assert.False(editor.HasSelectedLight);
        Assert.False(editor.SetLightEnabled(true));
        Assert.Equal(0, city.TrafficLights.Count);
    }

    [Fact]
    public void EnablingALightCreatesItForTheSelectedRoad()
    {
        (City city, Intersection junction, Road road) = CreateJunction();
        TrafficLightEditor editor = new(city, junction);
        editor.SelectRoad(road);

        bool changed = editor.SetLightEnabled(true);

        Assert.True(changed);
        Assert.True(editor.HasSelectedLight);
        Assert.NotNull(editor.SelectedLight);
        Assert.Same(road, editor.SelectedLight!.ControlledRoad);
        Assert.Same(junction, editor.SelectedLight.ControlledIntersection);
    }

    [Fact]
    public void EnablingALightTwiceIsANoOp()
    {
        (City city, Intersection junction, Road road) = CreateJunction();
        TrafficLightEditor editor = new(city, junction);
        editor.SelectRoad(road);

        editor.SetLightEnabled(true);
        bool changedAgain = editor.SetLightEnabled(true);

        // Reporting "nothing changed" is what keeps the caller from redrawing the preview needlessly.
        Assert.False(changedAgain);
        Assert.Equal(1, city.TrafficLights.Count);
    }

    [Fact]
    public void DisablingALightRemovesIt()
    {
        (City city, Intersection junction, Road road) = CreateJunction();
        TrafficLightEditor editor = new(city, junction);
        editor.SelectRoad(road);
        editor.SetLightEnabled(true);

        bool changed = editor.SetLightEnabled(false);

        Assert.True(changed);
        Assert.False(editor.HasSelectedLight);
        Assert.Equal(0, city.TrafficLights.Count);
    }

    [Fact]
    public void SelectingAnotherRoadSwitchesWhichLightIsEdited()
    {
        (City city, Intersection junction, Road first) = CreateJunction();
        Road second = city.CreateRoad(junction, city.GetOrCreateIntersection(new Engine.Geometry.Vector2(0, 200)));

        TrafficLightEditor editor = new(city, junction);
        editor.SelectRoad(first);
        editor.SetLightEnabled(true);

        editor.SelectRoad(second);

        Assert.Same(second, editor.SelectedRoad);
        Assert.False(editor.HasSelectedLight);

        editor.SetLightEnabled(true);

        // Each road at a junction carries its own light, so the junction now has two.
        Assert.Equal(2, city.TrafficLights.Count);
        Assert.True(editor.HasSelectedLight);
    }

    [Fact]
    public void PhaseDurationsAreAppliedToTheSelectedLight()
    {
        (City city, Intersection junction, Road road) = CreateJunction();
        TrafficLightEditor editor = new(city, junction);
        editor.SelectRoad(road);
        editor.SetLightEnabled(true);

        Assert.True(editor.SetGreenDuration(45));
        Assert.True(editor.SetRedDuration(60));

        Assert.Equal(45, editor.SelectedLight!.GreenDurationSeconds, 10);
        Assert.Equal(60, editor.SelectedLight.RedDurationSeconds, 10);
    }

    [Fact]
    public void PhaseDurationsBelowTheMinimumAreIgnoredByTheModel()
    {
        (City city, Intersection junction, Road road) = CreateJunction();
        TrafficLightEditor editor = new(city, junction);
        editor.SelectRoad(road);
        editor.SetLightEnabled(true);

        double original = editor.SelectedLight!.GreenDurationSeconds;
        editor.SetGreenDuration(1);

        Assert.Equal(original, editor.SelectedLight.GreenDurationSeconds, 10);
    }

    [Fact]
    public void ChangingTheSignalUpdatesTheLight()
    {
        (City city, Intersection junction, Road road) = CreateJunction();
        TrafficLightEditor editor = new(city, junction);
        editor.SelectRoad(road);
        editor.SetLightEnabled(true);
        editor.SetLightEnabled(true);

        Assert.True(editor.SetState(TrafficLightState.Green));
        Assert.Equal(TrafficLightState.Green, editor.SelectedLight!.State);

        // Setting the state it is already in changes nothing.
        Assert.False(editor.SetState(TrafficLightState.Green));
    }

    [Fact]
    public void CommandsWithoutASelectedRoadOrLightReportThatNothingChanged()
    {
        (City city, Intersection junction, Road road) = CreateJunction();
        TrafficLightEditor editor = new(city, junction);

        Assert.False(editor.SetGreenDuration(30));
        Assert.False(editor.SetRedDuration(30));
        Assert.False(editor.SetState(TrafficLightState.Green));

        editor.SelectRoad(road);
        Assert.False(editor.SetGreenDuration(30));
    }

    [Fact]
    public void ChangedIsRaisedWheneverSomethingIsEdited()
    {
        (City city, Intersection junction, Road road) = CreateJunction();
        TrafficLightEditor editor = new(city, junction);

        int notifications = 0;
        editor.Changed += (_, _) => notifications++;

        editor.SelectRoad(road);
        editor.SetLightEnabled(true);
        editor.SetGreenDuration(20);
        editor.SetLightEnabled(true);

        Assert.Equal(3, notifications);
    }

    /// <summary>Builds a city with a single road, which is enough to hang a traffic light on.</summary>
    /// <returns>The city, the junction and the road, in that order.</returns>
    private static (City City, Intersection Junction, Road Road) CreateJunction()
    {
        CitySettings settings = new() { RainChancePerTick = 0 };
        City city = new(settings, new ConstantRandomSource(0.5));

        Intersection junction = city.GetOrCreateIntersection(new Engine.Geometry.Vector2(0, 0));
        Intersection far = city.GetOrCreateIntersection(new Engine.Geometry.Vector2(100, 0));

        Road road = city.CreateRoad(junction, far);

        return (city, junction, road);
    }
}
