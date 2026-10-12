using TrafficSimulation.Engine.Domain;

namespace TrafficSimulation.Engine.Simulation;

/// <summary>
/// Owns the traffic lights of a city and answers lookups by road and intersection.
/// </summary>
/// <remarks>
/// A light is uniquely identified by the pair (road, intersection), which is why the manager can
/// keep a dictionary and guarantee that no junction ends up with two lights for the same road.
/// </remarks>
public sealed class TrafficLightController : ITrafficLightQuery
{
    private readonly Dictionary<(Road Road, Intersection Intersection), TrafficLight> _lights = [];
    private readonly List<TrafficLight> _ordered = [];

    /// <summary>All traffic lights of the city.</summary>
    public IReadOnlyCollection<TrafficLight> All => _ordered;

    /// <summary>Number of lights in the city.</summary>
    public int Count => _ordered.Count;

    /// <summary>
    /// Adds a light that regulates <paramref name="road"/> at <paramref name="intersection"/>.
    /// </summary>
    /// <param name="road">Road to regulate.</param>
    /// <param name="intersection">Intersection the light stands at.</param>
    /// <param name="initialState">Signal to start from.</param>
    /// <returns>The newly created light.</returns>
    public TrafficLight Add(Road road, Intersection intersection, TrafficLightState initialState = TrafficLightState.Red)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(intersection);

        TrafficLight light = new(road, intersection, initialState);
        Add(light);
        return light;
    }

    /// <summary>Adds an existing light. Replaces any light already controlling the same pair.</summary>
    /// <param name="light">Light to add.</param>
    public void Add(TrafficLight light)
    {
        ArgumentNullException.ThrowIfNull(light);

        (Road, Intersection) key = (light.ControlledRoad, light.ControlledIntersection);
        if (_lights.TryGetValue(key, out TrafficLight? existing))
        {
            _ordered.Remove(existing);
        }

        _lights[key] = light;
        _ordered.Add(light);
    }

    /// <summary>Removes a light from the city.</summary>
    /// <param name="light">Light to remove.</param>
    /// <returns><see langword="true"/> when the light was present.</returns>
    /// <remarks>
    /// A light that has already been replaced by a newer one for the same (road, intersection) pair
    /// is no longer registered, so removing it must not touch the replacement.
    /// </remarks>
    public bool Remove(TrafficLight light)
    {
        ArgumentNullException.ThrowIfNull(light);

        if (!_ordered.Remove(light))
        {
            return false;
        }

        // Only drop the dictionary entry when it still points at this very light.
        (Road, Intersection) key = (light.ControlledRoad, light.ControlledIntersection);
        if (_lights.TryGetValue(key, out TrafficLight? current) && ReferenceEquals(current, light))
        {
            _lights.Remove(key);
        }

        return true;
    }

    /// <summary>Removes the light regulating a road at an intersection, if there is one.</summary>
    /// <param name="road">Road the light regulates.</param>
    /// <param name="intersection">Intersection the light stands at.</param>
    /// <returns><see langword="true"/> when a light was removed.</returns>
    public bool Remove(Road road, Intersection intersection)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(intersection);

        if (_lights.Remove((road, intersection), out TrafficLight? light))
        {
            _ordered.Remove(light);
            return true;
        }

        return false;
    }

    /// <summary>Removes every light that regulates any of the given roads.</summary>
    /// <param name="roads">Roads whose lights should be dropped.</param>
    public void RemoveForRoads(IEnumerable<Road> roads)
    {
        ArgumentNullException.ThrowIfNull(roads);

        HashSet<Road> doomed = [.. roads];
        foreach (TrafficLight light in _ordered.Where(light => doomed.Contains(light.ControlledRoad)).ToList())
        {
            Remove(light);
        }
    }

    /// <summary>
    /// Re-points every light that used to regulate <paramref name="oldRoad"/> at
    /// <paramref name="newRoad"/>, but only for the ends the old road had.
    /// </summary>
    /// <param name="oldRoad">Road that is being replaced.</param>
    /// <param name="newRoad">Road that takes over.</param>
    /// <param name="intersection">Intersection the replacement concerns, or <see langword="null"/> for all.</param>
    /// <returns>The number of lights that were re-pointed.</returns>
    /// <remarks>
    /// Used when a road is split in two: the light that stood at the junction which is still an end
    /// point of the new piece must keep controlling that junction.
    /// </remarks>
    public int RedirectRoad(Road oldRoad, Road newRoad, Intersection? intersection = null)
    {
        ArgumentNullException.ThrowIfNull(oldRoad);
        ArgumentNullException.ThrowIfNull(newRoad);

        List<TrafficLight> affected = _ordered
            .Where(light => light.ControlledRoad == oldRoad
                            && (intersection is null || light.ControlledIntersection == intersection))
            .ToList();

        foreach (TrafficLight light in affected)
        {
            Remove(light);
            light.ControlledRoad = newRoad;
            Add(light);
        }

        return affected.Count;
    }

    /// <summary>Removes every light.</summary>
    public void Clear()
    {
        _lights.Clear();
        _ordered.Clear();
    }

    /// <inheritdoc />
    public TrafficLight? Find(Road road, Intersection intersection)
    {
        if (road is null || intersection is null)
        {
            return null;
        }

        return _lights.GetValueOrDefault((road, intersection));
    }

    /// <inheritdoc />
    /// <remarks>
    /// Also covers the case where a light controls a road that is about to be entered: the check
    /// uses the pair carried by the light itself, so a call with a road that has no light simply
    /// returns <see langword="null"/>.
    /// </remarks>
    public bool HasLight(Road road, Intersection intersection) => Find(road, intersection) is not null;

    /// <summary>Advances every light by one tick.</summary>
    public void Tick()
    {
        foreach (TrafficLight light in _ordered)
        {
            light.Tick();
        }
    }

    /// <summary>
    /// Returns the lights relevant for a junction: every light standing at it.
    /// </summary>
    /// <param name="intersection">Intersection to look at.</param>
    public IReadOnlyList<TrafficLight> At(Intersection intersection)
    {
        ArgumentNullException.ThrowIfNull(intersection);
        return _ordered.Where(light => light.ControlledIntersection == intersection).ToList();
    }
}
