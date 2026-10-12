using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Simulation;
using TrafficSimulation.Tests.Helpers;

namespace TrafficSimulation.Tests.Domain;

/// <summary>
/// Tests for the signal cycle of <see cref="TrafficLight"/> and for
/// <see cref="TrafficLightController"/>, which owns the lights of a city.
/// </summary>
public class TrafficLightTests
{
    [Fact]
    public void NewLightStartsRedForAFullRedPhase()
    {
        var (_, road, junction, _) = BuildRoad();
        TrafficLight light = new(road, junction);

        // A signal that controls a junction always starts in its red phase, so the first vehicles to
        // arrive are stopped instead of being waved through by an uninitialised light. The phase timer
        // is started by the constructor, so the light waits a whole red phase (30 s by default).
        Assert.Equal(TrafficLightState.Red, light.State);
        Assert.Equal(SimulationConstants.DefaultRedPhaseSeconds, light.RemainingSeconds, 10);
    }

    [Fact]
    public void TickCountsDownAndChangesPhaseExactlyWhenTheTimerExpires()
    {
        var (_, road, junction, _) = BuildRoad();
        TrafficLight light = new(road, junction);
        int ticksToExpiry = TicksFor(light, light.RedDurationSeconds);

        Advance(light, ticksToExpiry - 1);

        // One tick before the deadline the light is still red; switching earlier would cut the phase
        // short and give drivers less than the configured time.
        Assert.Equal(TrafficLightState.Red, light.State);
        Assert.Equal(light.TickSeconds, light.RemainingSeconds, 10);

        light.Tick();

        // The tick that empties the timer is the tick that changes the signal.
        Assert.Equal(TrafficLightState.Yellow, light.State);
    }

    [Fact]
    public void YellowPhaseLastsTheFixedYellowDuration()
    {
        var (_, road, junction, _) = BuildRoad();
        TrafficLight light = new(road, junction);
        Advance(light, TicksFor(light, light.RedDurationSeconds));

        // Amber is the warning phase and is fixed at two seconds, not configurable like red and green.
        Assert.Equal(2, TrafficLight.YellowDurationSeconds, 10);
        Assert.Equal(TrafficLight.YellowDurationSeconds, light.RemainingSeconds, 10);

        Advance(light, TicksFor(light, TrafficLight.YellowDurationSeconds) - 1);

        // It is never skipped: the signal still shows yellow just before the phase expires.
        Assert.Equal(TrafficLightState.Yellow, light.State);

        light.Tick();
        Assert.Equal(TrafficLightState.Green, light.State);
    }

    [Fact]
    public void TheCycleTurnsRedThenYellowThenGreenThenRedAgain()
    {
        var (_, road, junction, _) = BuildRoad();
        TrafficLight light = new(road, junction);

        Advance(light, TicksFor(light, light.RedDurationSeconds));
        Assert.Equal(TrafficLightState.Yellow, light.State);

        Advance(light, TicksFor(light, TrafficLight.YellowDurationSeconds));
        Assert.Equal(TrafficLightState.Green, light.State);

        Advance(light, TicksFor(light, light.GreenDurationSeconds));
        Assert.Equal(TrafficLightState.Red, light.State);
    }

    [Fact]
    public void AssigningStateRestartsThePhaseTimer()
    {
        var (_, road, junction, _) = BuildRoad();
        TrafficLight light = new(road, junction);
        Advance(light, TicksFor(light, light.RedDurationSeconds) + TicksFor(light, TrafficLight.YellowDurationSeconds));
        Advance(light, 100);

        Assert.Equal(TrafficLightState.Green, light.State);

        light.State = TrafficLightState.Green;

        // Re-selecting the signal that is already showing restarts its countdown rather than leaving
        // the stale timer behind, which is how the editor keeps a phase alive.
        Assert.Equal(light.GreenDurationSeconds, light.RemainingSeconds, 10);

        light.Tick();

        // After one tick the freshly restarted phase is still green and has only lost one tick.
        Assert.Equal(TrafficLightState.Green, light.State);
        Assert.Equal(light.GreenDurationSeconds - light.TickSeconds, light.RemainingSeconds, 10);
    }

    [Fact]
    public void DurationsBelowTheMinimumAreIgnored()
    {
        var (_, road, junction, _) = BuildRoad();
        TrafficLight light = new(road, junction);

        light.GreenDurationSeconds = TrafficLight.MinPhaseSeconds - 1;
        light.RedDurationSeconds = 0;

        // A phase of only a few ticks would make the signal flash; such a request is refused and the
        // previous duration stays in force.
        Assert.Equal(SimulationConstants.DefaultGreenPhaseSeconds, light.GreenDurationSeconds, 10);
        Assert.Equal(SimulationConstants.DefaultRedPhaseSeconds, light.RedDurationSeconds, 10);

        // Exactly the minimum is a legal, if short, phase.
        light.GreenDurationSeconds = TrafficLight.MinPhaseSeconds;
        Assert.Equal(TrafficLight.MinPhaseSeconds, light.GreenDurationSeconds, 10);
    }

    [Fact]
    public void ChangingTheActivePhaseDurationRestartsItsTimer()
    {
        var (_, road, junction, _) = BuildRoad();
        TrafficLight light = new(road, junction);

        light.RedDurationSeconds = 45;

        // Red is the running phase, so the longer red starts counting immediately; otherwise the old,
        // shorter deadline would cut the new phase short.
        Assert.Equal(45, light.RedDurationSeconds, 10);
        Assert.Equal(45, light.RemainingSeconds, 10);
    }

    [Fact]
    public void ChangingAnInactivePhaseDurationLeavesTheRunningPhaseAlone()
    {
        var (_, road, junction, _) = BuildRoad();
        TrafficLight light = new(road, junction);
        light.RedDurationSeconds = 45;
        light.Tick();

        Assert.Equal(44.9, light.RemainingSeconds, 10);

        light.GreenDurationSeconds = 20;

        // Green is not showing, so the red countdown must not jump; the new green length is stored
        // for when the cycle reaches the green phase.
        Assert.Equal(44.9, light.RemainingSeconds, 10);
        Assert.Equal(20, light.GreenDurationSeconds, 10);
    }

    [Fact]
    public void RequiresStopAppliesOnlyToTheControlledRoadAndJunction()
    {
        var (city, road, junction, far) = BuildRoad();
        Road other = city.AddTwoWayRoad(far, city.Node(200, 0));
        TrafficLight light = new(road, junction);

        // Red and yellow both mean stop; only green lets traffic through.
        Assert.True(light.RequiresStop(road, junction));
        light.State = TrafficLightState.Yellow;
        Assert.True(light.RequiresStop(road, junction));
        light.State = TrafficLightState.Green;
        Assert.False(light.RequiresStop(road, junction));

        // A light speaks only for its own (road, junction) pair: a different road or a different
        // junction is never stopped by it.
        Assert.False(light.RequiresStop(other, junction));
        Assert.False(light.RequiresStop(road, far));
    }

    [Fact]
    public void PhaseDurationSecondsMatchesTheCurrentState()
    {
        var (_, road, junction, _) = BuildRoad();
        TrafficLight light = new(road, junction);

        Assert.Equal(light.RedDurationSeconds, light.PhaseDurationSeconds, 10);

        light.State = TrafficLightState.Yellow;
        Assert.Equal(TrafficLight.YellowDurationSeconds, light.PhaseDurationSeconds, 10);

        light.State = TrafficLightState.Green;
        Assert.Equal(light.GreenDurationSeconds, light.PhaseDurationSeconds, 10);

        light.GreenDurationSeconds = 12;

        // The reported length follows the configured length, so a timer built from it never disagrees
        // with the phase that is actually running.
        Assert.Equal(12, light.PhaseDurationSeconds, 10);
    }

    [Fact]
    public void AddReturnsALightAndKeepsOnePerRoadAndJunctionPair()
    {
        var (controller, first, second, _, left, right) = BuildNetwork();

        TrafficLight light = controller.Add(first, left);

        // The (road, junction) pair is the identity of a light, so it can be found again by that pair.
        Assert.NotNull(light);
        Assert.Same(light, controller.Find(first, left));

        // An uncontrolled pair has no light; callers rely on null to mean "no signal here".
        Assert.Null(controller.Find(second, left));
        Assert.Null(controller.Find(first, right));

        TrafficLight replacement = controller.Add(first, left, TrafficLightState.Green);

        // Adding a light for a pair that already has one replaces it, so a junction never shows two
        // contradictory signals for the same approach.
        Assert.Equal(1, controller.Count);
        Assert.Same(replacement, controller.Find(first, left));
        Assert.DoesNotContain(light, controller.All);
    }

    [Fact]
    public void RemoveDropsALightByInstanceAndByPair()
    {
        var (controller, first, _, _, left, _) = BuildNetwork();

        TrafficLight light = controller.Add(first, left);

        Assert.True(controller.Remove(light));
        Assert.Equal(0, controller.Count);
        Assert.Null(controller.Find(first, left));

        controller.Add(first, left);

        Assert.True(controller.Remove(first, left));

        // Removing the pair again has nothing left to remove, so it must report failure rather than
        // silently claiming success.
        Assert.False(controller.Remove(first, left));
    }

    [Fact]
    public void RemoveForRoadsDropsOnlyTheLightsOfTheNamedRoads()
    {
        var (controller, first, second, _, left, right) = BuildNetwork();

        controller.Add(first, left);
        controller.Add(first, right);
        TrafficLight kept = controller.Add(second, right);

        controller.RemoveForRoads([first]);

        // Only the lights of the removed road disappear; the neighbouring road keeps its signal.
        Assert.Equal(1, controller.Count);
        Assert.Null(controller.Find(first, left));
        Assert.Null(controller.Find(first, right));
        Assert.Same(kept, controller.Find(second, right));
    }

    [Fact]
    public void RedirectRoadMovesEveryLightToTheReplacementRoad()
    {
        var (controller, first, _, replacement, left, right) = BuildNetwork();

        TrafficLight atLeft = controller.Add(first, left);
        TrafficLight atRight = controller.Add(first, right);

        int moved = controller.RedirectRoad(first, replacement);

        // Both junction lights now belong to the replacement road, and none is left pointing at the
        // old road that is about to disappear.
        Assert.Equal(2, moved);
        Assert.Same(atLeft, controller.Find(replacement, left));
        Assert.Same(atRight, controller.Find(replacement, right));
        Assert.Null(controller.Find(first, left));
    }

    [Fact]
    public void RedirectRoadCanBeLimitedToOneJunction()
    {
        var (controller, first, _, replacement, left, right) = BuildNetwork();

        TrafficLight atLeft = controller.Add(first, left);
        TrafficLight atRight = controller.Add(first, right);

        int moved = controller.RedirectRoad(first, replacement, left);

        // Splitting a road only concerns the junction the two pieces still share; the light at the
        // other end belongs to the piece that kept that junction.
        Assert.Equal(1, moved);
        Assert.Same(atLeft, controller.Find(replacement, left));
        Assert.Same(atRight, controller.Find(first, right));
    }

    [Fact]
    public void TickAdvancesEveryLightOfTheCity()
    {
        var (controller, first, second, _, left, right) = BuildNetwork();

        TrafficLight firstLight = controller.Add(first, left);
        TrafficLight secondLight = controller.Add(second, right);
        double firstBefore = firstLight.RemainingSeconds;
        double secondBefore = secondLight.RemainingSeconds;

        controller.Tick();

        // One tick of the city is one tick for every signal; leaving a light behind would let two
        // approaches drift out of phase over time.
        Assert.Equal(firstBefore - firstLight.TickSeconds, firstLight.RemainingSeconds, 10);
        Assert.Equal(secondBefore - secondLight.TickSeconds, secondLight.RemainingSeconds, 10);
    }

    [Fact]
    public void AtReturnsOnlyTheLightsOfTheGivenJunction()
    {
        var (controller, first, second, _, left, right) = BuildNetwork();

        controller.Add(first, left);
        TrafficLight atRight = controller.Add(first, right);
        controller.Add(second, right);

        // This view is what the junction settings window shows, so it must not leak the lights of
        // other junctions.
        Assert.Single(controller.At(left));
        Assert.Equal(2, controller.At(right).Count);
        Assert.Same(atRight, controller.At(right)[0]);
    }

    [Fact]
    public void ClearRemovesEveryLight()
    {
        var (controller, first, second, _, left, right) = BuildNetwork();

        controller.Add(first, left);
        controller.Add(second, right);

        controller.Clear();

        Assert.Equal(0, controller.Count);
        Assert.Empty(controller.All);
        Assert.Null(controller.Find(first, left));
    }

    /// <summary>Creates the smallest network a light can control: one road between two junctions.</summary>
    /// <returns>The city, the road and its two end points.</returns>
    private static (TestCity City, Road Road, Intersection Junction, Intersection Far) BuildRoad()
    {
        TestCity city = new();
        Intersection junction = city.Node(0, 0);
        Intersection far = city.Node(100, 0);
        Road road = city.AddTwoWayRoad(junction, far);
        return (city, road, junction, far);
    }

    /// <summary>
    /// Creates a network for the controller tests: a road, a parallel replacement over the same two
    /// junctions, and a second road leaving the far junction.
    /// </summary>
    /// <returns>A fresh controller together with the roads and junctions its lights control.</returns>
    private static (
        TrafficLightController Controller,
        Road First,
        Road Second,
        Road Replacement,
        Intersection Left,
        Intersection Right) BuildNetwork()
    {
        TestCity city = new();
        Intersection left = city.Node(0, 0);
        Intersection right = city.Node(100, 0);
        Road first = city.AddTwoWayRoad(left, right);
        Road second = city.AddTwoWayRoad(right, city.Node(200, 0));
        Road replacement = city.AddTwoWayRoad(left, right);
        return (new TrafficLightController(), first, second, replacement, left, right);
    }

    /// <summary>Runs a light for a whole number of simulation ticks.</summary>
    private static void Advance(TrafficLight light, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            light.Tick();
        }
    }

    /// <summary>Number of whole ticks a phase of the given length lasts for this light.</summary>
    private static int TicksFor(TrafficLight light, double seconds)
        => (int)Math.Round(seconds / light.TickSeconds);
}
