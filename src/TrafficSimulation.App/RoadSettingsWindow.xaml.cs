using System.Windows;
using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;

namespace TrafficSimulation.App;

/// <summary>
/// Settings of a single road.
/// </summary>
/// <remarks>
/// The window edits the road through <see cref="City"/> rather than by assigning properties
/// directly, because changing the type or the number of lanes changes the topology of the network
/// and has to be repaired centrally: vehicles may have to be removed or re-routed, and the
/// intersections have to be told which side of the traffic the road now belongs to.
/// </remarks>
public partial class RoadSettingsWindow : Window
{
    private readonly Road _road;
    private readonly City _city;

    /// <summary>Whether the constructor has finished and the handlers may write to the model.</summary>
    private bool IsReady { get; set; }

    /// <summary>Creates a settings window for a road.</summary>
    /// <param name="road">Road to edit.</param>
    /// <param name="city">City the road belongs to.</param>
    public RoadSettingsWindow(Road road, City city)
    {
        InitializeComponent();

        _road = road;
        _city = city;

        RoadNameTextBox.Text = road.Name;
        MaxSpeedSlider.Value = road.SpeedLimitKph;
        RoadQualitySlider.Value = road.Quality * 100;
        LanesSlider.Value = road.LanesPerDirection;

        if (road.Type == RoadType.OneWay)
        {
            OneWayRadioButton.IsChecked = true;
        }
        else
        {
            TwoWayRadioButton.IsChecked = true;
        }

        IsReady = true;
    }

    private void RoadNameTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsReady)
        {
            _road.Name = RoadNameTextBox.Text;
        }
    }

    private void MaxSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsReady)
        {
            _road.SpeedLimitKph = Math.Round(MaxSpeedSlider.Value);
        }
    }

    private void LanesSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsReady)
        {
            _city.SetLanesPerDirection(_road, (int)Math.Round(LanesSlider.Value));
        }
    }

    private void RoadQualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (IsReady)
        {
            _road.Quality = Math.Round(RoadQualitySlider.Value / 100, 2);
        }
    }

    private void OneWayRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (IsReady)
        {
            _city.SetRoadType(_road, RoadType.OneWay);
        }
    }

    private void TwoWayRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (IsReady)
        {
            _city.SetRoadType(_road, RoadType.TwoWay);
        }
    }

    private void DeleteRoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Are you sure you want to delete this road?",
                "Delete road",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _city.RemoveRoad(_road);
        Close();
    }
}
