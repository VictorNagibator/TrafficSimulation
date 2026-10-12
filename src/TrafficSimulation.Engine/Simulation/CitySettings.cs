using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Randomness;

namespace TrafficSimulation.Engine.Simulation;

/// <summary>
/// Behavioural settings of a whole city: road defaults, accidents and weather.
/// </summary>
/// <remarks>
/// The distinction between the values here matters and was a frequent source of confusion in the
/// original prototype:
/// <list type="bullet">
/// <item>a <em>Chance</em> property is a probability per tick in the range <c>[0, 1]</c>;</item>
/// <item>a <c>*Percent</c> property is the same probability expressed as a percentage, which is
/// what the settings window edits;</item>
/// <item>a duration is expressed both in seconds (for the user interface) and in ticks (for the
/// simulation loop).</item>
/// </list>
/// The conversions live next to the properties so the mapping is defined in exactly one place.
/// </remarks>
public sealed class CitySettings
{
    private double _speedLimitKph = SimulationConstants.DefaultSpeedLimitKph;
    private double _roadQuality = SimulationConstants.DefaultRoadQuality;
    private int _defaultLanesPerDirection = SimulationConstants.MinLanesPerDirection;

    /// <summary>
    /// Speed limit applied to the whole city in km/h. Assigning it also updates every existing road.
    /// </summary>
    public double SpeedLimitKph
    {
        get => _speedLimitKph;
        set => _speedLimitKph = Math.Clamp(value, SimulationConstants.MinSpeedLimitKph, SimulationConstants.MaxSpeedLimitKph);
    }

    /// <summary>
    /// Pavement quality applied to the whole city. Assigning it also updates every existing road.
    /// </summary>
    public double RoadQuality
    {
        get => _roadQuality;
        set => _roadQuality = Math.Clamp(value, SimulationConstants.MinRoadQuality, SimulationConstants.MaxRoadQuality);
    }

    /// <summary>Number of lanes a road created by the map editor starts with.</summary>
    public int DefaultLanesPerDirection
    {
        get => _defaultLanesPerDirection;
        set => _defaultLanesPerDirection = Math.Clamp(
            value,
            SimulationConstants.MinLanesPerDirection,
            SimulationConstants.MaxLanesPerDirection);
    }

    /// <summary>Probability per tick that a moving vehicle ends up in an accident.</summary>
    public double AccidentChancePerTick { get; set; } = 0.003;

    /// <summary>Probability per tick that a moving vehicle breaks down.</summary>
    public double BreakdownChancePerTick { get; set; } = 0.003;

    /// <summary>Additional accident probability per tick while it rains.</summary>
    public double RainAccidentChancePerTick { get; set; } = 0.005;

    /// <summary>How long a vehicle stays put after an accident, in ticks.</summary>
    public int AccidentDurationTicks { get; set; } = (int)(30 / SimulationConstants.TickSeconds);

    /// <summary>How long a vehicle stays put after a breakdown, in ticks.</summary>
    public int BreakdownDurationTicks { get; set; } = (int)(30 / SimulationConstants.TickSeconds);

    /// <summary>Probability per tick that rain starts while the weather is dry.</summary>
    public double RainChancePerTick { get; set; } = 0.01;

    /// <summary>How long a rain period lasts, in ticks.</summary>
    public int RainDurationTicks { get; set; } = (int)(60 / SimulationConstants.TickSeconds);

    /// <summary>Probability per tick that a lane change is attempted.</summary>
    public double LaneChangeChancePerTick { get; set; } = 0.05;

    /// <summary>Speed gained each tick, in km/h per tick.</summary>
    public double AccelerationKphPerTick { get; set; } = 1;

    /// <summary>Length of one simulation tick, in seconds.</summary>
    public double TickSeconds { get; set; } = SimulationConstants.TickSeconds;

    /// <summary>Accident probability expressed as a percentage, as edited in the settings window.</summary>
    public double AccidentChancePercent
    {
        get => AccidentChancePerTick * 100;
        set => AccidentChancePerTick = Math.Max(0, value) / 100;
    }

    /// <summary>Rain probability expressed as a percentage, as edited in the settings window.</summary>
    public double RainChancePercent
    {
        get => RainChancePerTick * 100;
        set => RainChancePerTick = Math.Max(0, value) / 100;
    }

    /// <summary>Accident duration in seconds, as edited in the settings window.</summary>
    public double AccidentDurationSeconds
    {
        get => AccidentDurationTicks * TickSeconds;
        set => AccidentDurationTicks = Math.Max(1, (int)Math.Round(value / TickSeconds));
    }

    /// <summary>Rain duration in seconds, as edited in the settings window.</summary>
    public double RainDurationSeconds
    {
        get => RainDurationTicks * TickSeconds;
        set => RainDurationTicks = Math.Max(1, (int)Math.Round(value / TickSeconds));
    }

    /// <summary>Copies the settings into an independent instance.</summary>
    /// <returns>The copy.</returns>
    public CitySettings Clone()
    {
        CitySettings clone = (CitySettings)MemberwiseClone();
        clone.OnTickSecondsChanged();
        return clone;
    }

    /// <summary>Keeps the tick based durations consistent after <see cref="TickSeconds"/> changed.</summary>
    internal void OnTickSecondsChanged()
    {
        AccidentDurationTicks = Math.Max(1, (int)Math.Round(AccidentDurationSeconds / TickSeconds));
        RainDurationTicks = Math.Max(1, (int)Math.Round(RainDurationSeconds / TickSeconds));
    }

    /// <summary>Builds the vehicle behaviour settings implied by this city configuration.</summary>
    /// <returns>A fresh settings object for the vehicles.</returns>
    public VehicleBehaviourSettings ToVehicleSettings() => new()
    {
        TickSeconds = TickSeconds,
        AccelerationKphPerTick = AccelerationKphPerTick,
        LaneChangeChancePerTick = LaneChangeChancePerTick,
        AccidentChancePerTick = AccidentChancePerTick,
        BreakdownChancePerTick = BreakdownChancePerTick,
        RainAccidentChancePerTick = RainAccidentChancePerTick,
        AccidentDurationTicks = AccidentDurationTicks,
        BreakdownDurationTicks = BreakdownDurationTicks,
    };
}

/// <summary>
/// The weather of a city and the traffic-light registry.
/// </summary>
/// <remarks>
/// Weather is intentionally modelled as a tiny state machine driven by the same random source as
/// everything else, so a simulation run can be replayed exactly.
/// </remarks>
public sealed class SimulationEnvironment
{
    private readonly IRandomSource _random;
    private int _remainingTicks;

    /// <summary>Creates an environment with dry weather.</summary>
    /// <param name="settings">City configuration the environment reacts to.</param>
    /// <param name="random">Source of randomness for weather changes.</param>
    public SimulationEnvironment(CitySettings settings, IRandomSource random)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>City configuration the environment reacts to.</summary>
    public CitySettings Settings { get; }

    /// <summary>Traffic lights of the city.</summary>
    public TrafficLightController TrafficLights { get; } = new();

    /// <summary>Whether it currently rains.</summary>
    public bool IsRaining { get; private set; }

    /// <summary>Ticks left before the current rain period ends.</summary>
    public int RemainingRainTicks => _remainingTicks;

    /// <summary>Advances the weather by one tick.</summary>
    public void Update()
    {
        if (Settings.RainChancePerTick <= 0)
        {
            IsRaining = false;
            _remainingTicks = 0;
            return;
        }

        if (IsRaining)
        {
            _remainingTicks--;
            if (_remainingTicks <= 0)
            {
                IsRaining = false;
                _remainingTicks = 0;
            }

            return;
        }

        if (_random.NextDouble() < Settings.RainChancePerTick)
        {
            IsRaining = true;
            _remainingTicks = Settings.RainDurationTicks;
        }
    }

    /// <summary>Stops the rain immediately.</summary>
    public void ClearWeather()
    {
        IsRaining = false;
        _remainingTicks = 0;
    }
}
