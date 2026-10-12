namespace TrafficSimulation.Engine.Domain;

/// <summary>Signal currently shown by a traffic light.</summary>
public enum TrafficLightState
{
    /// <summary>Traffic must stop.</summary>
    Red,

    /// <summary>Traffic must prepare to stop.</summary>
    Yellow,

    /// <summary>Traffic may proceed.</summary>
    Green,
}

/// <summary>
/// A traffic light that regulates one road at one intersection.
/// </summary>
/// <remarks>
/// <para>
/// A light belongs to exactly one <see cref="Road"/> and one <see cref="Intersection"/>: the pair
/// identifies it uniquely inside a <see cref="City"/>. Vehicles look up "the light that controls my
/// road at the junction I am approaching" and stop on red or yellow.
/// </para>
/// <para>
/// The cycle of the prototype runs red, then yellow, then green, then red again, and only the red
/// and green durations are configurable. That order was preserved deliberately so the behaviour of
/// the original simulation stays recognisable; <see cref="DefaultYellowSeconds"/> is fixed.
/// </para>
/// </remarks>
public sealed class TrafficLight
{
    /// <summary>Smallest permitted duration of a configurable phase, in seconds.</summary>
    public const double MinPhaseSeconds = SimulationConstants.MinTrafficLightPhaseSeconds;

    /// <summary>Duration of the yellow phase, in seconds. Not configurable, see the class remarks.</summary>
    public const double DefaultYellowSeconds = SimulationConstants.DefaultYellowPhaseSeconds;

    private double _tickSeconds = SimulationConstants.TickSeconds;
    private double _remainingTicks;
    private double _greenDurationSeconds = SimulationConstants.DefaultGreenPhaseSeconds;
    private double _redDurationSeconds = SimulationConstants.DefaultRedPhaseSeconds;

    /// <summary>Creates a light in the <see cref="TrafficLightState.Red"/> state.</summary>
    /// <remarks>
    /// The red phase starts running immediately, exactly as it does for the
    /// <see cref="TrafficLight(Road, Intersection, TrafficLightState)"/> constructor, so that the
    /// signal does not change on the very first tick.
    /// </remarks>
    public TrafficLight() => RestartPhase();

    /// <summary>Creates a light that controls a road at an intersection.</summary>
    /// <param name="road">Road whose traffic the light regulates.</param>
    /// <param name="intersection">Intersection the light stands at.</param>
    /// <param name="initialState">Signal shown initially.</param>
    public TrafficLight(Road road, Intersection intersection, TrafficLightState initialState = TrafficLightState.Red)
    {
        ControlledRoad = road;
        ControlledIntersection = intersection;
        _state = initialState;
        RestartPhase();
    }

    /// <summary>Road whose traffic this light regulates.</summary>
    public Road ControlledRoad { get; internal set; } = null!;

    /// <summary>Intersection at which this light stands.</summary>
    public Intersection ControlledIntersection { get; internal set; } = null!;

    /// <summary>Signal currently shown. Assigning a value restarts the phase timer.</summary>
    public TrafficLightState State
    {
        get => _state;
        set
        {
            _state = value;
            RestartPhase();
        }
    }

    /// <summary>Duration of the green phase, in seconds.</summary>
    /// <remarks>Values below <see cref="MinPhaseSeconds"/> are ignored. Applying the value restarts the phase.</remarks>
    public double GreenDurationSeconds
    {
        get => _greenDurationSeconds;
        set
        {
            if (value < MinPhaseSeconds)
            {
                return;
            }

            _greenDurationSeconds = value;
            RestartPhaseIfCurrent(TrafficLightState.Green);
        }
    }

    /// <summary>Duration of the red phase, in seconds.</summary>
    /// <remarks>Values below <see cref="MinPhaseSeconds"/> are ignored. Applying the value restarts the phase.</remarks>
    public double RedDurationSeconds
    {
        get => _redDurationSeconds;
        set
        {
            if (value < MinPhaseSeconds)
            {
                return;
            }

            _redDurationSeconds = value;
            RestartPhaseIfCurrent(TrafficLightState.Red);
        }
    }

    /// <summary>Duration of the yellow phase, in seconds.</summary>
    /// <remarks>Fixed by design, so it does not depend on any instance data.</remarks>
    public static double YellowDurationSeconds => DefaultYellowSeconds;

    /// <summary>
    /// Length of one simulation tick, in seconds.
    /// </summary>
    /// <remarks>Kept explicit rather than hard coded so that the timer can be changed in one place.</remarks>
    public double TickSeconds
    {
        get => _tickSeconds;
        set
        {
            if (value > 0)
            {
                _tickSeconds = value;
            }
        }
    }

    /// <summary>Time left before the next phase change, in seconds.</summary>
    public double RemainingSeconds => _remainingTicks * _tickSeconds;

    /// <summary>Duration of the phase the light is currently in, in seconds.</summary>
    public double PhaseDurationSeconds => _state switch
    {
        TrafficLightState.Red => RedDurationSeconds,
        TrafficLightState.Yellow => YellowDurationSeconds,
        _ => GreenDurationSeconds,
    };

    private TrafficLightState _state = TrafficLightState.Red;

    /// <summary>
    /// Determines whether a vehicle standing on <paramref name="road"/> at
    /// <paramref name="intersection"/> has to stop because of this light.
    /// </summary>
    /// <param name="road">Road the vehicle is on.</param>
    /// <param name="intersection">Intersection the vehicle is approaching.</param>
    /// <returns><see langword="true"/> when the light controls that road and shows red or yellow.</returns>
    public bool RequiresStop(Road road, Intersection intersection)
        => ControlledRoad == road
           && ControlledIntersection == intersection
           && State != TrafficLightState.Green;

    /// <summary>Advances the light by one tick, changing the phase when the timer expires.</summary>
    public void Tick()
    {
        _remainingTicks--;
        if (_remainingTicks > 0)
        {
            return;
        }

        State = _state switch
        {
            TrafficLightState.Red => TrafficLightState.Yellow,
            TrafficLightState.Yellow => TrafficLightState.Green,
            _ => TrafficLightState.Red,
        };
    }

    /// <summary>Restarts the timer of the current phase.</summary>
    private void RestartPhase() => _remainingTicks = Math.Max(1, Math.Round(PhaseDurationSeconds / _tickSeconds));

    /// <summary>Restarts the timer, but only when the light currently shows the given signal.</summary>
    /// <param name="state">Signal that must be active for the timer to be reset.</param>
    private void RestartPhaseIfCurrent(TrafficLightState state)
    {
        if (_state == state)
        {
            RestartPhase();
        }
    }
}
