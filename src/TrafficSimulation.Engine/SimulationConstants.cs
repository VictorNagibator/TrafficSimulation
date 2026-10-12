namespace TrafficSimulation.Engine;

/// <summary>
/// Physical and behavioural constants of the simulation.
/// </summary>
/// <remarks>
/// All numeric values describe the <em>real world</em>: lengths are expressed in metres,
/// durations in seconds and speeds in kilometres per hour. Nothing in the engine is
/// expressed in pixels or in any other unit that belongs to a particular rendering device;
/// the conversion into screen coordinates is the responsibility of the view layer.
/// </remarks>
public static class SimulationConstants
{
    /// <summary>Width of a single traffic lane, in metres.</summary>
    public const double LaneWidthMetres = 3.75;

    /// <summary>Length of a vehicle, in metres.</summary>
    public const double VehicleLengthMetres = 4.5;

    /// <summary>Width of a vehicle, in metres.</summary>
    public const double VehicleWidthMetres = 1.8;

    /// <summary>Diameter of the traffic light bulb, in metres.</summary>
    public const double TrafficLightDiameterMetres = 1.5;

    /// <summary>
    /// Gap a vehicle keeps to the vehicle in front of it, in metres.
    /// </summary>
    /// <remarks>
    /// The original prototype used a multiple of the vehicle length as a rough stand-in for a
    /// safe following distance; the value is kept so that traffic behaviour does not change.
    /// </remarks>
    public const double SafetyDistanceMetres = VehicleLengthMetres * 1.5;

    /// <summary>Number of seconds represented by one simulation tick.</summary>
    public const double TickSeconds = 0.1;

    /// <summary>
    /// Angular tolerance used when classifying road directions at an intersection, in radians
    /// (10 degrees). Two directions closer than this are treated as "the same direction".
    /// </summary>
    public const double AngleToleranceRadians = Math.PI / 18;

    /// <summary>Lowest speed limit the user is allowed to configure, in km/h.</summary>
    public const double MinSpeedLimitKph = 20;

    /// <summary>Highest speed limit the user is allowed to configure, in km/h.</summary>
    public const double MaxSpeedLimitKph = 100;

    /// <summary>Default speed limit of a newly created road, in km/h.</summary>
    public const double DefaultSpeedLimitKph = 60;

    /// <summary>Lowest pavement quality the user is allowed to configure (1 = perfect).</summary>
    public const double MinRoadQuality = 0.05;

    /// <summary>Highest pavement quality the user is allowed to configure (1 = perfect).</summary>
    public const double MaxRoadQuality = 1.0;

    /// <summary>Default pavement quality of a newly created road (1 = perfect).</summary>
    public const double DefaultRoadQuality = 1.0;

    /// <summary>Shortest phase a traffic light may stay in, in seconds.</summary>
    public const double MinTrafficLightPhaseSeconds = 5;

    /// <summary>Default duration of the red phase, in seconds.</summary>
    public const double DefaultRedPhaseSeconds = 30;

    /// <summary>Default duration of the green phase, in seconds.</summary>
    public const double DefaultGreenPhaseSeconds = 30;

    /// <summary>Duration of the yellow phase, in seconds. Fixed on purpose, see <c>TrafficLight</c>.</summary>
    public const double DefaultYellowPhaseSeconds = 2;

    /// <summary>Speed penalty applied while it rains, in km/h.</summary>
    public const double RainSpeedPenaltyKph = 15;

    /// <summary>Quality lost by a road each time a vehicle crosses it, on a dry surface.</summary>
    public const double DryWearPerPass = 0.0002;

    /// <summary>Quality lost by a road each time a vehicle crosses it, on a wet surface.</summary>
    public const double WetWearPerPass = 0.0005;

    /// <summary>Minimum number of lanes a road can have in each direction.</summary>
    public const int MinLanesPerDirection = 1;

    /// <summary>Maximum number of lanes a road can have in each direction.</summary>
    public const int MaxLanesPerDirection = 10;

    /// <summary>
    /// Distance from an intersection, in metres, within which a vehicle is considered to be
    /// "already entering" it. Used by the right-of-way rule.
    /// </summary>
    public const double IntersectionApproachMetres = 10;

    /// <summary>
    /// Extra gap, in metres, required before a vehicle merges into an adjacent lane.
    /// </summary>
    public const double LaneChangeGapMetres = VehicleLengthMetres + SafetyDistanceMetres;
}
