using TrafficSimulation.Engine.Domain;

namespace TrafficSimulation.App.Interaction;

/// <summary>
/// Drives the traffic-light part of the intersection settings window.
/// </summary>
/// <remarks>
/// <para>
/// All the rules about traffic lights live here rather than in the window, which keeps the window's
/// code-behind to a single line per control and makes the behaviour testable without a user
/// interface.
/// </para>
/// <para>
/// A light is identified by the pair (road, intersection), and this class guarantees that a junction
/// never ends up with two lights for the same road.
/// </para>
/// </remarks>
public sealed class TrafficLightEditor
{
    private readonly City _city;
    private readonly Intersection _intersection;

    /// <summary>Creates an editor for the lights of one intersection.</summary>
    /// <param name="city">City the intersection belongs to.</param>
    /// <param name="intersection">Intersection whose lights are edited.</param>
    public TrafficLightEditor(City city, Intersection intersection)
    {
        _city = city ?? throw new ArgumentNullException(nameof(city));
        _intersection = intersection ?? throw new ArgumentNullException(nameof(intersection));
    }

    /// <summary>Raised whenever a light was added, removed or modified.</summary>
    public event EventHandler? Changed;

    /// <summary>Road whose light is currently being edited, if any.</summary>
    public Road? SelectedRoad { get; private set; }

    /// <summary>The light controlling the selected road at the edited intersection, if any.</summary>
    public TrafficLight? SelectedLight
        => SelectedRoad is null ? null : _city.TrafficLights.Find(SelectedRoad, _intersection);

    /// <summary>Whether the selected road already has a light at this intersection.</summary>
    public bool HasSelectedLight => SelectedLight is not null;

    /// <summary>Selects the road to edit, or clears the selection when passed <see langword="null"/>.</summary>
    /// <param name="road">Road to select.</param>
    public void SelectRoad(Road? road)
    {
        SelectedRoad = road;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adds a light to the selected road, or removes the existing one.
    /// </summary>
    /// <param name="isEnabled">Whether the road should have a light.</param>
    /// <returns>
    /// <see langword="true"/> when the state of the model changed, which the caller can use to decide
    /// whether the preview has to be redrawn.
    /// </returns>
    public bool SetLightEnabled(bool isEnabled)
    {
        if (SelectedRoad is null)
        {
            return false;
        }

        if (isEnabled == HasSelectedLight)
        {
            return false;
        }

        if (isEnabled)
        {
            _city.TrafficLights.Add(SelectedRoad, _intersection);
        }
        else
        {
            _city.TrafficLights.Remove(SelectedRoad, _intersection);
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Sets the duration of the green phase of the selected light.</summary>
    /// <param name="seconds">Duration in seconds.</param>
    /// <returns><see langword="true"/> when a light was updated.</returns>
    public bool SetGreenDuration(double seconds)
    {
        TrafficLight? light = SelectedLight;
        if (light is null)
        {
            return false;
        }

        light.GreenDurationSeconds = seconds;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Sets the duration of the red phase of the selected light.</summary>
    /// <param name="seconds">Duration in seconds.</param>
    /// <returns><see langword="true"/> when a light was updated.</returns>
    public bool SetRedDuration(double seconds)
    {
        TrafficLight? light = SelectedLight;
        if (light is null)
        {
            return false;
        }

        light.RedDurationSeconds = seconds;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Forces the signal of the selected light, for example to test a congested junction.</summary>
    /// <param name="state">Signal to show.</param>
    /// <returns><see langword="true"/> when a light was updated.</returns>
    public bool SetState(TrafficLightState state)
    {
        TrafficLight? light = SelectedLight;
        if (light is null || light.State == state)
        {
            return false;
        }

        light.State = state;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
