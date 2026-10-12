using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using TrafficSimulation.App.Interaction;
using TrafficSimulation.App.Rendering;
using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.App;

/// <summary>
/// Main window of the simulator: hosts the map and wires user input to the simulation engine.
/// </summary>
/// <remarks>
/// <para>
/// The window is deliberately thin. It owns three collaborators and delegates everything to them:
/// </para>
/// <list type="bullet">
/// <item><see cref="CityMapRenderer"/> turns the city into WPF shapes;</item>
/// <item><see cref="MapInputController"/> turns mouse gestures into edits of the city;</item>
/// <item><see cref="DispatcherTimer"/> drives the simulation, one tick per interval.</item>
/// </list>
/// <para>
/// What remains here is event plumbing and the small amount of window state that only the user
/// interface cares about, such as whether the traffic-jam overlay is on.
/// </para>
/// </remarks>
public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new();
    private readonly MapViewport _viewport = new();
    private readonly CityMapRenderer _renderer;

    private MapInputController _input;
    private City _city;
    private bool _isWatchingTrafficJams;
    private bool _isSettingsWindowOpen;

    /// <summary>Creates the window and starts with an empty city.</summary>
    public MainWindow()
    {
        InitializeComponent();

        _city = new City();
        _input = new MapInputController(_city);
        _renderer = new CityMapRenderer(CityCanvas);

        WireUpEvents();
        ConfigureTimer();

        Redraw();
    }

    /// <summary>Connects the renderer and the input controller to the window.</summary>
    private void WireUpEvents()
    {
        _renderer.IntersectionClicked += OnIntersectionClicked;
        _renderer.VehicleClicked += OnVehicleClicked;

        AttachInput(_city);
    }

    /// <summary>Points the input controller and the change notifications at a city.</summary>
    /// <param name="city">City that becomes the model of the window.</param>
    /// <remarks>
    /// The controller holds a reference to the city it edits, so replacing the city means rebuilding
    /// that pair.
    /// </remarks>
    private void AttachInput(City city)
    {
        _input.RedrawRequested -= OnRedrawRequested;
        _input.RoadSelected -= OnRoadSelected;
        _input.IntersectionSelected -= OnIntersectionClicked;
        city.Changed -= OnCityChanged;

        _input = new MapInputController(city);
        _input.RedrawRequested += OnRedrawRequested;
        _input.RoadSelected += OnRoadSelected;
        _input.IntersectionSelected += OnIntersectionClicked;
        city.Changed += OnCityChanged;
    }

    /// <summary>Configures the simulation loop.</summary>
    private void ConfigureTimer()
    {
        _timer.Interval = TimeSpan.FromSeconds(SimulationConstants.TickSeconds);
        _timer.Tick += OnTimerTick;
    }

    /// <summary>Handles the redraw request raised by the input controller.</summary>
    private void OnRedrawRequested(object? sender, EventArgs e) => Redraw();

    /// <summary>Handles a structural change of the city.</summary>
    private void OnCityChanged(object? sender, EventArgs e) => Redraw();

    /// <summary>Handles the request to open the settings of a road.</summary>
    private void OnRoadSelected(object? sender, Road road)
        => OpenWindow(new RoadSettingsWindow(road, _city));

    /// <summary>Advances the simulation by one tick and redraws the map.</summary>
    private void OnTimerTick(object? sender, EventArgs e)
    {
        _city.Update();
        Redraw();
    }

    /// <summary>Draws the current state of the city and updates the rain indicator.</summary>
    /// <remarks>
    /// The toolbar is synchronised here as well, because the road tool can finish on its own: after
    /// the second click of a road the controller returns to the selection tool, so the "add road"
    /// button has to come back without anybody pressing Cancel.
    /// </remarks>
    private void Redraw()
    {
        _renderer.Render(new MapRenderState(
            _city,
            _viewport,
            _isWatchingTrafficJams,
            _input.Preview));

        RainImage.Visibility = _city.IsRaining ? Visibility.Visible : Visibility.Hidden;

        bool isPlacingRoad = _input.Tool != MapTool.Select;
        AddRoadButton.Visibility = isPlacingRoad ? Visibility.Collapsed : Visibility.Visible;
        CancelAddRoadButton.Visibility = isPlacingRoad ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Opens a settings window and pauses the simulation while it is open.</summary>
    /// <param name="window">Window to open.</param>
    /// <remarks>
    /// Running the simulation while a settings window is open would let the user edit a road that a
    /// vehicle is in the middle of using, so the simulation is suspended for the duration.
    /// </remarks>
    private void OpenWindow(Window window)
    {
        if (_isSettingsWindowOpen)
        {
            return;
        }

        bool wasRunning = _timer.IsEnabled;
        if (wasRunning)
        {
            StopSimulation();
        }

        _isSettingsWindowOpen = true;
        IsEnabled = false;

        window.Owner = this;
        window.Closed += (_, _) =>
        {
            _isSettingsWindowOpen = false;
            IsEnabled = true;
            Redraw();

            if (wasRunning)
            {
                StartSimulation();
            }
        };

        window.Show();
    }

    /// <summary>Handles a click on an intersection: opens its settings.</summary>
    private void OnIntersectionClicked(object? sender, Intersection intersection)
        => OpenWindow(new IntersectionSettingsWindow(intersection, _city));

    /// <summary>Handles a click on a vehicle: offers to remove it.</summary>
    private void OnVehicleClicked(object? sender, Vehicle vehicle)
    {
        bool wasRunning = _timer.IsEnabled;
        if (wasRunning)
        {
            StopSimulation();
        }

        if (MessageBox.Show(
                "Do you want to remove this vehicle?",
                "Remove vehicle",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _city.RemoveVehicle(vehicle);
            Redraw();
        }

        if (wasRunning)
        {
            StartSimulation();
        }
    }

    private void AddVehicleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_city.Roads.Count != 0)
        {
            _city.CreateVehicle();
        }

        Redraw();
    }

    private void DeleteAllVehiclesButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Do you want to remove every vehicle?",
                "Remove vehicles",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _city.RemoveAllVehicles();
            Redraw();
        }
    }

    private void StartSimulationButton_Click(object sender, RoutedEventArgs e) => StartSimulation();

    private void StopSimulationButton_Click(object sender, RoutedEventArgs e) => StopSimulation();

    /// <summary>Starts the simulation loop.</summary>
    private void StartSimulation()
    {
        _timer.Start();
        StartSimulationButton.Visibility = Visibility.Collapsed;
        StopSimulationButton.Visibility = Visibility.Visible;
    }

    /// <summary>Stops the simulation loop.</summary>
    private void StopSimulation()
    {
        _timer.Stop();
        StartSimulationButton.Visibility = Visibility.Visible;
        StopSimulationButton.Visibility = Visibility.Collapsed;
    }

    private void CityCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Clip the canvas so that shapes outside the visible area are not drawn.
        CityCanvas.Clip = new System.Windows.Media.RectangleGeometry(
            new Rect(0, 0, CityCanvas.ActualWidth, CityCanvas.ActualHeight));

        Redraw();
    }

    private void CityCanvas_LeftMouseDown(object sender, MouseButtonEventArgs e)
    {
        Point position = e.GetPosition(CityCanvas);

        if (_input.Tool == MapTool.Select)
        {
            // In selection mode a press over an existing object opens it instead of starting a drag.
            if (TryOpenObjectAt(position))
            {
                return;
            }
        }

        _input.OnLeftButtonDown(position, _viewport);
        CityCanvas.CaptureMouse();
    }

    /// <summary>Opens the settings of whatever object lies under a point of the canvas.</summary>
    /// <param name="position">Cursor position in canvas pixels.</param>
    /// <returns><see langword="true"/> when an object was found and opened.</returns>
    private bool TryOpenObjectAt(Point position)
    {
        Intersection? intersection = _city.Editor.FindIntersectionAt(
            _viewport.ToWorld(position),
            _viewport.ToMetres(MapInputController.IntersectionHitRadiusPixels));

        if (intersection is not null)
        {
            OnIntersectionClicked(this, intersection);
            return true;
        }

        Road? road = _city.Editor.FindRoadAt(
            _viewport.ToWorld(position),
            _viewport.ToMetres(MapInputController.RoadHitTolerancePixels));

        if (road is null)
        {
            Vehicle? vehicle = FindVehicleAt(position);
            if (vehicle is not null)
            {
                OnVehicleClicked(this, vehicle);
                return true;
            }

            return false;
        }

        // A click that lands on a road belongs to the road, unless a vehicle is closer to the cursor.
        Vehicle? underCursor = FindVehicleAt(position);
        if (underCursor is not null)
        {
            OnVehicleClicked(this, underCursor);
            return true;
        }

        OnRoadSelected(this, road);
        return true;
    }

    private void CityCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        bool isPressed = e.LeftButton == MouseButtonState.Pressed;

        if (_input.OnMouseMove(e.GetPosition(CityCanvas), _viewport, isPressed))
        {
            Redraw();
        }
    }

    private void CityCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _input.OnLeftButtonUp();
        CityCanvas.ReleaseMouseCapture();
    }

    private void CityCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_input.IsPanning)
        {
            return;
        }

        _input.OnLeftButtonUp();
        CityCanvas.ReleaseMouseCapture();
    }

    private void CityCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
        {
            ZoomInButton_Click(sender, e);
        }
        else
        {
            ZoomOutButton_Click(sender, e);
        }
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewport.ZoomIn())
        {
            Redraw();
        }
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewport.ZoomOut())
        {
            Redraw();
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _viewport.Reset();
        Redraw();
    }

    private void TrafficJamButton_Click(object sender, RoutedEventArgs e)
    {
        _isWatchingTrafficJams = !_isWatchingTrafficJams;
        Redraw();
    }

    private void AddRoadButton_Click(object sender, RoutedEventArgs e) => _input.BeginRoadPlacement();

    private void CancelAddRoadButton_Click(object sender, RoutedEventArgs e) => _input.CancelRoadPlacement();

    private void CitySettingsButton_Click(object sender, RoutedEventArgs e)
        => OpenWindow(new CitySettingsWindow(_city));

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
                "Do you want to clear the whole map?",
                "Clear city",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        _input.CancelRoadPlacement();
        _viewport.Reset();

        City empty = new();
        AttachInput(empty);
        _city = empty;

        Redraw();
    }

    /// <summary>Finds the vehicle drawn under a point of the canvas.</summary>
    /// <param name="screenPosition">Cursor position in canvas pixels.</param>
    /// <returns>The vehicle under the cursor, or <see langword="null"/>.</returns>
    private Vehicle? FindVehicleAt(Point screenPosition)
    {
        double tolerance = _viewport.ToMetres(MapInputController.RoadHitTolerancePixels)
            + (SimulationConstants.VehicleLengthMetres / 2);

        Vector2 point = _viewport.ToWorld(screenPosition);
        Vehicle? closest = null;
        double closestDistance = double.MaxValue;

        foreach (Vehicle vehicle in _city.Vehicles)
        {
            double distance = point.DistanceTo(vehicle.CurrentRoad.GetPositionOnRoad(vehicle));
            if (distance <= tolerance && distance < closestDistance)
            {
                closestDistance = distance;
                closest = vehicle;
            }
        }

        return closest;
    }
}
