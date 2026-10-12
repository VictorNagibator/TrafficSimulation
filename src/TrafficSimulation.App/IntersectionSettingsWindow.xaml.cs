using System.Windows;
using TrafficSimulation.App.Interaction;
using TrafficSimulation.App.Rendering;
using TrafficSimulation.Engine.Domain;

namespace TrafficSimulation.App;

/// <summary>
/// Close-up settings of a single intersection, including the traffic lights that control it.
/// </summary>
/// <remarks>
/// The window is a thin shell over <see cref="TrafficLightEditor"/>: it forwards slider and radio
/// button changes to the editor and re-renders the preview whenever the editor reports a change.
/// </remarks>
public partial class IntersectionSettingsWindow : Window
{
    private readonly City _city;
    private readonly Intersection _intersection;
    private readonly TrafficLightEditor _editor;
    private readonly IntersectionPreviewRenderer _renderer;

    /// <summary>
    /// Guards the control handlers while the window writes into the controls itself.
    /// </summary>
    /// <remarks>
    /// Assigning <c>IsChecked = true</c> raises <c>Checked</c>, which would otherwise be mistaken for
    /// a click of the user and create a second traffic light.
    /// </remarks>
    private bool _isUpdatingControls;

    /// <summary>Creates a settings window for an intersection.</summary>
    /// <param name="intersection">Intersection to edit.</param>
    /// <param name="city">City the intersection belongs to.</param>
    public IntersectionSettingsWindow(Intersection intersection, City city)
    {
        InitializeComponent();

        _intersection = intersection;
        _city = city;
        _editor = new TrafficLightEditor(city, intersection);
        _renderer = new IntersectionPreviewRenderer(IntersectionCanvas);

        _renderer.RoadClicked += (_, road) => _editor.SelectRoad(road);
        _editor.Changed += (_, _) => RefreshView();

        RefreshView();
    }

    /// <summary>Coordinates redraws the preview and pushes the model into the controls.</summary>
    private void RefreshView()
    {
        RoadNameTextBlock.Text = _editor.SelectedRoad is null
            ? "Select a road to configure it"
            : $"Road: {_editor.SelectedRoad.Name ?? "(unnamed)"}";

        _isUpdatingControls = true;
        try
        {
            TrafficLight? light = _editor.SelectedLight;

            IsEnabledTrafficLightCheckBox.IsEnabled = _editor.SelectedRoad is not null;
            IsEnabledTrafficLightCheckBox.IsChecked = light is not null;
            TrafficLightSettingsPanel.Visibility = light is null ? Visibility.Collapsed : Visibility.Visible;

            if (light is not null)
            {
                GreenTimeSlider.Value = light.GreenDurationSeconds;
                RedTimeSlider.Value = light.RedDurationSeconds;

                RedLightRadioButton.IsChecked = light.State == TrafficLightState.Red;
                YellowLightRadioButton.IsChecked = light.State == TrafficLightState.Yellow;
                GreenLightRadioButton.IsChecked = light.State == TrafficLightState.Green;
            }
        }
        finally
        {
            _isUpdatingControls = false;
        }

        RenderPreview();
    }

    /// <summary>Draws the close-up of the intersection.</summary>
    private void RenderPreview()
        => _renderer.Render(new IntersectionRenderState(_city, _intersection, _editor.SelectedRoad));

    private void IntersectionSettingsWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        IntersectionCanvas.Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(0, 0, IntersectionCanvas.ActualWidth, IntersectionCanvas.ActualHeight));

        RenderPreview();
    }

    private void IsEnabledTrafficLightCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingControls)
        {
            return;
        }

        _editor.SetLightEnabled(true);
    }

    private void IsEnabledTrafficLightCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingControls)
        {
            return;
        }

        _editor.SetLightEnabled(false);
    }

    private void GreenTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingControls)
        {
            _editor.SetGreenDuration(e.NewValue);
        }
    }

    private void RedTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingControls)
        {
            _editor.SetRedDuration(e.NewValue);
        }
    }

    private void RedLightRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isUpdatingControls)
        {
            _editor.SetState(TrafficLightState.Red);
        }
    }

    private void YellowLightRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isUpdatingControls)
        {
            _editor.SetState(TrafficLightState.Yellow);
        }
    }

    private void GreenLightRadioButton_Checked(object sender, RoutedEventArgs e)
    {
        if (!_isUpdatingControls)
        {
            _editor.SetState(TrafficLightState.Green);
        }
    }
}
