using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Randomness;

namespace TrafficSimulation.Engine.Simulation;

/// <summary>
/// Everything a vehicle needs to know about the world while it is being updated.
/// </summary>
/// <remarks>
/// The update of a single tick happens in two clearly separated phases, and this type is what makes
/// that separation possible:
/// <list type="number">
/// <item>first the city describes the state of the environment and of every traffic light for this
/// tick and builds a <see cref="SimulationContext"/>;</item>
/// <item>then every vehicle is updated against that frozen context.</item>
/// </list>
/// Vehicles therefore never mutate shared state that other vehicles of the same tick would read,
/// which keeps the simulation deterministic and makes it trivial to build a context by hand in a
/// unit test.
/// </remarks>
public sealed class SimulationContext
{
    /// <summary>Creates a context for a single tick.</summary>
    /// <param name="city">City the vehicles belong to.</param>
    /// <param name="trafficLights">Traffic light state for this tick.</param>
    /// <param name="isRaining">Whether it currently rains.</param>
    /// <param name="random">Source of randomness used for this tick.</param>
    public SimulationContext(City city, ITrafficLightQuery trafficLights, bool isRaining, IRandomSource random)
    {
        City = city ?? throw new ArgumentNullException(nameof(city));
        TrafficLights = trafficLights ?? throw new ArgumentNullException(nameof(trafficLights));
        IsRaining = isRaining;
        Random = random ?? throw new ArgumentNullException(nameof(random));
        Settings = city.Settings.ToVehicleSettings();
    }

    /// <summary>City the vehicles belong to.</summary>
    public City City { get; }

    /// <summary>Read-only view of the traffic lights for this tick.</summary>
    public ITrafficLightQuery TrafficLights { get; }

    /// <summary>Whether it rains during this tick.</summary>
    public bool IsRaining { get; }

    /// <summary>Source of randomness for this tick.</summary>
    public IRandomSource Random { get; }

    /// <summary>Behavioural settings shared by all vehicles of this city.</summary>
    public VehicleBehaviourSettings Settings { get; }

    /// <summary>Length of the current tick, in seconds.</summary>
    public double TickSeconds => Settings.TickSeconds;
}
