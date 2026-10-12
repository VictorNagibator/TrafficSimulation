using System.Windows;
using TrafficSimulation.Engine.Domain;

namespace TrafficSimulation.App;

/// <summary>
/// Settings that apply to the whole city.
/// </summary>
/// <remarks>
/// The sliders write straight into <see cref="City.Settings"/>. Every handler therefore has to cope
/// with being called while the window is still being constructed, when the sliders report their
/// initial values but the fields behind them are not assigned yet; that is what the
/// <see cref="IsReady"/> guard is for.
/// </remarks>
public partial class CitySettingsWindow : Window
{
    private readonly City _city;

    /// <summary>Whether the constructor has finished and the handlers may write to the model.</summary>
    private bool IsReady { get; set; }

    /// <summary>Creates a settings window for a city.</summary>
    /// <param name="city">City whose settings are edited.</param>
    public CitySettingsWindow(City city)
    {
        InitializeComponent();

        _city = city;

        MaxSpeedSlider.Value = city.Settings.SpeedLimitKph;
        RoadQualitySlider.Value = city.Settings.RoadQuality * 100;
        AccidentChanceSlider.Value = city.Settings.AccidentChancePercent;
        AccidentDurationSlider.Value = city.Settings.AccidentDurationSeconds;
        RainChanceSlider.Value = city.Settings.RainChancePercent;
        RainDurationSlider.Value = city.Settings.RainDurationSeconds;

        IsReady = true;
    }

    private void MaxSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsReady)
        {
            return;
        }

        _city.SpeedLimitKph = Math.Round(MaxSpeedSlider.Value);
    }

    private void RoadQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsReady)
        {
            return;
        }

        _city.RoadQuality = Math.Round(RoadQualitySlider.Value / 100, 2);
    }

    private void RainChanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsReady)
        {
            return;
        }

        _city.RainChancePerTick = Math.Round(RainChanceSlider.Value, 2) / 100;
    }

    private void AccidentChanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsReady)
        {
            return;
        }

        _city.Settings.AccidentChancePercent = Math.Round(AccidentChanceSlider.Value, 2);
    }

    private void AccidentDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsReady)
        {
            return;
        }

        _city.Settings.AccidentDurationSeconds = Math.Round(AccidentDurationSlider.Value);
    }

    private void RainDurationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsReady)
        {
            return;
        }

        _city.RainDurationTicks = Math.Max(1, (int)(Math.Round(RainDurationSlider.Value) / _city.Settings.TickSeconds));
    }
}
