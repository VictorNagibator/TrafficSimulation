using TrafficSimulation.Engine.Domain;

namespace TrafficSimulation.Engine.Simulation;

/// <summary>
/// Read-only view of the traffic lights of a city.
/// </summary>
/// <remarks>
/// Vehicles only ever need to ask "which light regulates my road at the junction I am approaching",
/// so the engine exposes this narrow interface instead of the whole mutable collection. Tests can
/// implement it in a line or two.
/// </remarks>
public interface ITrafficLightQuery
{
    /// <summary>All traffic lights, in no particular order.</summary>
    IReadOnlyCollection<TrafficLight> All { get; }

    /// <summary>
    /// Finds the light that regulates <paramref name="road"/> at <paramref name="intersection"/>.
    /// </summary>
    /// <param name="road">Road a vehicle travels on.</param>
    /// <param name="intersection">Intersection the vehicle is approaching.</param>
    /// <returns>The matching light, or <see langword="null"/> when the junction is uncontrolled.</returns>
    TrafficLight? Find(Road road, Intersection intersection);
}
