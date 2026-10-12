using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Randomness;

namespace TrafficSimulation.Engine.Simulation;

/// <summary>
/// Behavioural settings shared by every vehicle of a city.
/// </summary>
/// <remarks>
/// These values used to be private constants hidden inside <c>Vehicle</c>; they are collected here
/// so that they can be inspected, tuned and, above all, overridden by tests.
/// </remarks>
public sealed class VehicleBehaviourSettings
{
    /// <summary>Length of one simulation tick, in seconds.</summary>
    public double TickSeconds { get; set; } = SimulationConstants.TickSeconds;

    /// <summary>Speed gained by a vehicle each tick, in km/h per tick.</summary>
    public double AccelerationKphPerTick { get; set; } = 1;

    /// <summary>Probability that a vehicle tries to change lane during one tick, in <c>[0, 1]</c>.</summary>
    public double LaneChangeChancePerTick { get; set; } = 0.05;

    /// <summary>
    /// Probability that a vehicle gets into an accident during one tick, in <c>[0, 1]</c>.
    /// </summary>
    /// <remarks>
    /// This is the *base* value used on a perfect road in dry weather; the effective probability is
    /// raised by poor pavement quality and by rain, see <see cref="Vehicle.GetDisabledRisk"/>.
    /// </remarks>
    public double AccidentChancePerTick { get; set; } = 0.003;

    /// <summary>Probability that a vehicle breaks down during one tick, in <c>[0, 1]</c>.</summary>
    public double BreakdownChancePerTick { get; set; } = 0.003;

    /// <summary>Additional accident probability per tick while it rains, in <c>[0, 1]</c>.</summary>
    public double RainAccidentChancePerTick { get; set; } = 0.005;

    /// <summary>Time a vehicle stays disabled after an accident, in ticks.</summary>
    public int AccidentDurationTicks { get; set; } = (int)(30 / SimulationConstants.TickSeconds);

    /// <summary>Time a vehicle stays disabled after a breakdown, in ticks.</summary>
    public int BreakdownDurationTicks { get; set; } = (int)(30 / SimulationConstants.TickSeconds);

    /// <summary>Creates a copy of these settings.</summary>
    /// <returns>An independent copy.</returns>
    public VehicleBehaviourSettings Clone() => (VehicleBehaviourSettings)MemberwiseClone();

    /// <summary>
    /// Converts a probability expressed per second into the equivalent probability per tick.
    /// </summary>
    /// <param name="chancePerSecond">Probability of the event during one second.</param>
    /// <returns>Probability of the event during one tick.</returns>
    public double PerTick(double chancePerSecond) => chancePerSecond * TickSeconds;

    /// <summary>
    /// Converts a number of seconds into a number of whole ticks.
    /// </summary>
    /// <param name="seconds">Duration in seconds.</param>
    /// <returns>The equivalent duration in ticks, never below one.</returns>
    public int TicksFromSeconds(double seconds) => Math.Max(1, (int)(seconds / TickSeconds));

    /// <summary>Builds the settings a <see cref="City"/> hands to its vehicles by default.</summary>
    /// <returns>Settings that reproduce the behaviour of the original prototype.</returns>
    public static VehicleBehaviourSettings CreateDefault() => new();

    /// <summary>Creates settings with a specific tick length.</summary>
    /// <param name="tickSeconds">Length of one tick, in seconds.</param>
    /// <returns>The new settings.</returns>
    public static VehicleBehaviourSettings WithTickSeconds(double tickSeconds)
    {
        VehicleBehaviourSettings settings = new() { TickSeconds = tickSeconds };
        settings.AccidentDurationTicks = settings.TicksFromSeconds(30);
        settings.BreakdownDurationTicks = settings.TicksFromSeconds(30);
        return settings;
    }

    /// <summary>
    /// Creates a settings object whose random components never fire, which is what most unit tests
    /// want: vehicles then move purely because of the rules under test.
    /// </summary>
    /// <returns>Deterministic settings.</returns>
    public static VehicleBehaviourSettings CreateDeterministic()
        => new()
        {
            LaneChangeChancePerTick = 0,
            AccidentChancePerTick = 0,
            BreakdownChancePerTick = 0,
            RainAccidentChancePerTick = 0,
        };
}
