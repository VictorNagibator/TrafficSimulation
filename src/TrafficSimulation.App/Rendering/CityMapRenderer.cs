using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.App.Rendering;

/// <summary>
/// A road that the user is currently drawing, before it is added to the city.
/// </summary>
/// <param name="Segment">Geometry of the proposed road, in metres.</param>
/// <param name="WidthMetres">Width the road would have.</param>
/// <param name="IsAllowed">Whether the road could legally be created where it currently ends.</param>
/// <remarks>
/// The editor draws a road before it owns it, so the preview cannot be a <see cref="Road"/> yet: it
/// has no registered end points and would corrupt the intersections if it were added to them. This
/// small value type carries everything the renderer needs instead.
/// </remarks>
public readonly record struct RoadPreview(Engine.Geometry.LineSegment Segment, double WidthMetres, bool IsAllowed);

/// <summary>
/// The state of the map that has to be drawn during one render pass.
/// </summary>
/// <param name="City">City to draw.</param>
/// <param name="Viewport">Current zoom and panning.</param>
/// <param name="IsWatchingTrafficJams">Whether roads should be coloured by their traffic density.</param>
/// <param name="Preview">Road the user is currently drawing, if any.</param>
public readonly record struct MapRenderState(
    City City,
    MapViewport Viewport,
    bool IsWatchingTrafficJams = false,
    RoadPreview? Preview = null);

/// <summary>
/// Draws a <see cref="City"/> onto a WPF <see cref="Canvas"/>.
/// </summary>
/// <remarks>
/// <para>
/// All drawing code lives here, so the window's code-behind only has to react to events and ask for a
/// redraw. The renderer holds no state of its own: everything it needs arrives with each
/// <see cref="MapRenderState"/>.
/// </para>
/// <para>
/// Coordinates are converted from metres into pixels by the <see cref="MapViewport"/>; the renderer
/// itself never touches the zoom level.
/// </para>
/// </remarks>
public sealed class CityMapRenderer
{
    private const double RoadNameFontMetres = 2.5;
    private const double SpeedLimitSignDiameterMetres = 6;
    private const double SpeedLimitSignFontMetres = 3;
    private const double SpeedLimitSignOffsetMetres = 2;
    private const double ScaleBarPixels = 100;
    private const double ScaleBarLabelOffsetPixels = 80;
    private const double OneWayArrowMetres = 0.5;
    private const double OneWayArrowHalfAngle = Math.PI / 6;

    private readonly Canvas _canvas;

    /// <summary>Creates a renderer that draws onto a canvas.</summary>
    /// <param name="canvas">Canvas to draw on. The renderer clears it on every pass.</param>
    public CityMapRenderer(Canvas canvas)
        => _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

    /// <summary>Raised when the user clicks a road.</summary>
    public event EventHandler<Road>? RoadClicked;

    /// <summary>Raised when the user clicks an intersection.</summary>
    public event EventHandler<Intersection>? IntersectionClicked;

    /// <summary>Raised when the user clicks a vehicle.</summary>
    public event EventHandler<Vehicle>? VehicleClicked;

    /// <summary>Draws the whole map.</summary>
    /// <param name="state">What to draw and how.</param>
    public void Render(in MapRenderState state)
    {
        ArgumentNullException.ThrowIfNull(state.City);
        ArgumentNullException.ThrowIfNull(state.Viewport);

        _canvas.Children.Clear();

        foreach (Road road in state.City.Roads)
        {
            DrawRoad(road, state);
        }

        // Vehicles and lights are hidden while the traffic-jam overlay is on, so that the colours of
        // the roads stay readable.
        if (!state.IsWatchingTrafficJams)
        {
            foreach (Vehicle vehicle in state.City.Vehicles)
            {
                DrawVehicle(vehicle, state);
            }

            foreach (TrafficLight light in state.City.TrafficLights.All)
            {
                DrawTrafficLight(light, state);
            }
        }

        if (state.Preview is { } preview)
        {
            DrawPreview(preview, state);
        }

        DrawScaleBar(state);
    }

    /// <summary>Draws the road the user is currently drawing.</summary>
    /// <param name="preview">Proposed road.</param>
    /// <param name="state">State of the map.</param>
    private void DrawPreview(RoadPreview preview, in MapRenderState state)
    {
        Point start = state.Viewport.ToScreen(preview.Segment.Start);
        Point end = state.Viewport.ToScreen(preview.Segment.End);

        Line line = new()
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            StrokeThickness = Math.Max(1, state.Viewport.ToPixels(preview.WidthMetres)),
            Stroke = preview.IsAllowed ? Brushes.Aqua : Brushes.Red,
            IsHitTestVisible = false,
        };

        _canvas.Children.Add(line);
    }

    /// <summary>Draws a single road, including everything that is drawn on top of it.</summary>
    /// <param name="road">Road to draw.</param>
    /// <param name="state">State of the map.</param>
    private void DrawRoad(Road road, in MapRenderState state)
    {
        MapViewport viewport = state.Viewport;

        // Roads are shortened so that they stop at the edge of the intersections they connect,
        // instead of crossing them.
        Vector2 start = road.StartIntersection.Position
            + Vector2.FromAngle(road.Angle, road.StartIntersection.HalfWidth);
        Vector2 end = road.EndIntersection.Position
            - Vector2.FromAngle(road.Angle, road.EndIntersection.HalfWidth);

        Point screenStart = viewport.ToScreen(start);
        Point screenEnd = viewport.ToScreen(end);

        Line line = new()
        {
            X1 = screenStart.X,
            Y1 = screenStart.Y,
            X2 = screenEnd.X,
            Y2 = screenEnd.Y,
            StrokeThickness = viewport.ToPixels(road.Width),
            Tag = road,
            Opacity = road.Quality,
            Stroke = ChooseRoadBrush(road, state),
        };

        line.MouseLeftButtonDown += (_, _) => RoadClicked?.Invoke(this, road);
        _canvas.Children.Add(line);

        if (road.Type == RoadType.OneWay)
        {
            DrawOneWayArrow(road, state);
        }

        DrawRoadName(road, state);

        // The sign is only shown when the road deviates from the city-wide limit.
        if (Math.Abs(road.SpeedLimitKph - state.City.SpeedLimitKph) > 1e-9)
        {
            DrawSpeedLimitSign(road, state);
        }

        DrawIntersection(road.StartIntersection, state);
        DrawIntersection(road.EndIntersection, state);
    }

    /// <summary>Chooses the colour of a road according to the current display mode.</summary>
    /// <param name="road">Road to colour.</param>
    /// <param name="state">State of the map.</param>
    /// <returns>The brush to stroke the road with.</returns>
    private static Brush ChooseRoadBrush(Road road, in MapRenderState state)
    {
        if (!state.IsWatchingTrafficJams)
        {
            return Brushes.Black;
        }

        return road.TrafficJamLevel switch
        {
            TrafficJamLevel.Free => Brushes.Green,
            TrafficJamLevel.Busy => Brushes.Yellow,
            TrafficJamLevel.Congested => Brushes.Red,
            _ => Brushes.DarkRed,
        };
    }

    /// <summary>Draws the black square that represents a junction.</summary>
    /// <param name="intersection">Intersection to draw.</param>
    /// <param name="state">State of the map.</param>
    private void DrawIntersection(Intersection intersection, in MapRenderState state)
    {
        double size = state.Viewport.ToPixels(intersection.WidestRoadWidth);

        Rectangle rectangle = new()
        {
            Width = size,
            Height = size,
            Fill = Brushes.Black,
            Tag = intersection,
            Opacity = intersection.BestRoadQuality,
            RenderTransform = new RotateTransform(
                Angles.ToDegrees(intersection.Angle),
                size / 2,
                size / 2),
        };

        rectangle.MouseLeftButtonDown += (_, _) => IntersectionClicked?.Invoke(this, intersection);

        Point topLeft = state.Viewport.ToScreen(
            new Vector2(intersection.X, intersection.Y) - new Vector2(size / 2, size / 2));
        Canvas.SetLeft(rectangle, topLeft.X);
        Canvas.SetTop(rectangle, topLeft.Y);
        _canvas.Children.Add(rectangle);
    }

    /// <summary>Draws the arrow that marks the direction of a one-way road.</summary>
    /// <param name="road">Road to annotate.</param>
    /// <param name="state">State of the map.</param>
    private void DrawOneWayArrow(Road road, in MapRenderState state)
    {
        double arrowLength = Math.Max(1, road.Width - OneWayArrowMetres);
        Point middle = state.Viewport.ToScreen(
            (road.StartIntersection.Position + road.EndIntersection.Position) * 0.5);

        Polygon arrow = new()
        {
            Fill = Brushes.Gray,
            Points =
            {
                middle,
                Translate(middle, Vector2.FromAngle(road.Angle - OneWayArrowHalfAngle, -arrowLength), state.Viewport),
                Translate(middle, Vector2.FromAngle(road.Angle + OneWayArrowHalfAngle, -arrowLength), state.Viewport),
            },
        };

        _canvas.Children.Add(arrow);
    }

    /// <summary>Offsets a screen point by a distance expressed in metres.</summary>
    /// <param name="origin">Screen point to start from.</param>
    /// <param name="offsetMetres">Offset in metres, in simulation coordinates.</param>
    /// <param name="viewport">Viewport that knows the current scale.</param>
    /// <returns>The offset screen point.</returns>
    private static Point Translate(Point origin, Vector2 offsetMetres, MapViewport viewport)
        => new(
            origin.X + viewport.ToPixels(offsetMetres.X),
            origin.Y + viewport.ToPixels(offsetMetres.Y));

    /// <summary>Draws the name of a road across its middle.</summary>
    /// <param name="road">Road to label.</param>
    /// <param name="state">State of the map.</param>
    private void DrawRoadName(Road road, in MapRenderState state)
    {
        if (string.IsNullOrEmpty(road.Name))
        {
            return;
        }

        TextBlock label = CreateLabel(
            road.Name,
            state.Viewport.ToPixels(RoadNameFontMetres),
            Brushes.White);

        Point middle = state.Viewport.ToScreen(
            (road.StartIntersection.Position + road.EndIntersection.Position) * 0.5);

        label.RenderTransform = new RotateTransform(
            Angles.ToDegrees(road.Angle),
            label.ActualWidth / 2,
            label.ActualHeight / 2);

        Canvas.SetLeft(label, middle.X - (label.ActualWidth / 2));
        Canvas.SetTop(label, middle.Y - (label.ActualHeight / 2));
        _canvas.Children.Add(label);
    }

    /// <summary>Draws the round speed limit sign next to a road.</summary>
    /// <param name="road">Road whose limit is shown.</param>
    /// <param name="state">State of the map.</param>
    private void DrawSpeedLimitSign(Road road, in MapRenderState state)
    {
        MapViewport viewport = state.Viewport;
        Point middle = viewport.ToScreen(
            (road.StartIntersection.Position + road.EndIntersection.Position) * 0.5);

        // The sign sits beside the road, on its left hand side.
        Vector2 beside = Vector2.FromAngle(
            road.Angle + (Math.PI / 2),
            road.Width + SpeedLimitSignOffsetMetres);
        double offsetX = middle.X + viewport.ToPixels(beside.X);
        double offsetY = middle.Y + viewport.ToPixels(beside.Y);

        double diameter = viewport.ToPixels(SpeedLimitSignDiameterMetres);
        Ellipse sign = new()
        {
            Width = diameter,
            Height = diameter,
            Fill = Brushes.White,
            Stroke = Brushes.Red,
            StrokeThickness = Math.Max(1, viewport.ToPixels(0.4)),
        };

        Canvas.SetLeft(sign, offsetX - (diameter / 2));
        Canvas.SetTop(sign, offsetY - (diameter / 2));
        _canvas.Children.Add(sign);

        TextBlock text = CreateLabel(
            road.SpeedLimitKph.ToString("F0"),
            viewport.ToPixels(SpeedLimitSignFontMetres),
            Brushes.Black);

        Canvas.SetLeft(text, offsetX - (text.ActualWidth / 2));
        Canvas.SetTop(text, offsetY - (text.ActualHeight / 2));
        _canvas.Children.Add(text);
    }

    /// <summary>Draws a vehicle as a rotated rectangle in the middle of its lane.</summary>
    /// <param name="vehicle">Vehicle to draw.</param>
    /// <param name="state">State of the map.</param>
    private void DrawVehicle(Vehicle vehicle, in MapRenderState state)
    {
        MapViewport viewport = state.Viewport;
        Vector2 worldPosition = vehicle.CurrentRoad.GetPositionOnRoad(vehicle);

        double length = viewport.ToPixels(SimulationConstants.VehicleLengthMetres);
        double width = viewport.ToPixels(SimulationConstants.VehicleWidthMetres);

        Rectangle body = new()
        {
            Width = length,
            Height = width,
            Fill = vehicle.IsDisabled ? Brushes.IndianRed : Brushes.Blue,
            Tag = vehicle,
            RenderTransform = new RotateTransform(Angles.ToDegrees(vehicle.HeadingRadians), length / 2, width / 2),
            ToolTip = $"Speed: {vehicle.SpeedKph:F0} km/h",
        };

        body.MouseDown += (_, _) => VehicleClicked?.Invoke(this, vehicle);

        // Move the body sideways from the centre line into its own lane. Lane 1 is the rightmost
        // lane of the direction of travel, so the offset grows towards the left.
        double laneOffsetMetres = GetLaneOffsetMetres(vehicle);
        double lateralOffsetPixels = viewport.ToPixels(laneOffsetMetres);
        double normalX = Math.Sin(vehicle.CurrentRoad.Angle);
        double normalY = -Math.Cos(vehicle.CurrentRoad.Angle);

        Point centre = viewport.ToScreen(worldPosition);
        double centreX = centre.X + (normalX * lateralOffsetPixels);
        double centreY = centre.Y + (normalY * lateralOffsetPixels);

        Canvas.SetLeft(body, centreX - (length / 2));
        Canvas.SetTop(body, centreY - (width / 2));
        _canvas.Children.Add(body);
    }

    /// <summary>Draws a traffic light at the edge of the junction it controls.</summary>
    /// <param name="light">Light to draw.</param>
    /// <param name="state">State of the map.</param>
    private void DrawTrafficLight(TrafficLight light, in MapRenderState state)
    {
        MapViewport viewport = state.Viewport;
        Point junction = viewport.ToScreen(light.ControlledIntersection.Position);

        // The light is placed half a lane away from the centre, so it appears beside the road.
        double approachAngle = GetApproachAngle(light);
        double offsetX = junction.X + viewport.ToPixels(Math.Cos(approachAngle) * SimulationConstants.LaneWidthMetres / 2);
        double offsetY = junction.Y + viewport.ToPixels(Math.Sin(approachAngle) * SimulationConstants.LaneWidthMetres / 2);

        double diameter = viewport.ToPixels(SimulationConstants.TrafficLightDiameterMetres);
        Ellipse lamp = new()
        {
            Width = diameter,
            Height = diameter,
            Fill = light.State switch
            {
                TrafficLightState.Red => Brushes.Red,
                TrafficLightState.Yellow => Brushes.Yellow,
                _ => Brushes.Green,
            },
        };

        Canvas.SetLeft(lamp, offsetX - (diameter / 2));
        Canvas.SetTop(lamp, offsetY - (diameter / 2));
        _canvas.Children.Add(lamp);
    }

    /// <summary>Draws the scale bar in the bottom right corner of the canvas.</summary>
    /// <param name="state">State of the map.</param>
    private void DrawScaleBar(in MapRenderState state)
    {
        if (_canvas.ActualWidth <= 0 || _canvas.ActualHeight <= 0)
        {
            return;
        }

        double right = _canvas.ActualWidth - 10;
        double bottom = _canvas.ActualHeight - 10;

        Line bar = new()
        {
            X1 = right,
            Y1 = bottom,
            X2 = right - ScaleBarPixels,
            Y2 = bottom,
            Stroke = Brushes.Black,
            StrokeThickness = 1,
            IsHitTestVisible = false,
        };

        _canvas.Children.Add(bar);

        double metres = state.Viewport.ToMetres(ScaleBarPixels);
        TextBlock label = new()
        {
            Text = $"{metres:F0} m",
            Foreground = Brushes.Black,
            FontSize = 10,
            Margin = new Thickness(right - ScaleBarLabelOffsetPixels, bottom - 25, 0, 0),
            IsHitTestVisible = false,
        };

        _canvas.Children.Add(label);
    }

    /// <summary>Creates a text label and measures it, so its size is known before it is placed.</summary>
    /// <param name="text">Text to display.</param>
    /// <param name="fontSize">Font size in pixels.</param>
    /// <param name="foreground">Brush used for the text.</param>
    /// <returns>A measured and arranged label.</returns>
    private static TextBlock CreateLabel(string text, double fontSize, Brush foreground)
    {
        TextBlock label = new()
        {
            Text = text,
            FontSize = Math.Max(1, fontSize),
            Foreground = foreground,
            IsHitTestVisible = false,
        };

        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        label.Arrange(new Rect(label.DesiredSize));
        return label;
    }

    /// <summary>Computes the sideways distance of a vehicle from the centre line of its road.</summary>
    /// <param name="vehicle">Vehicle to place.</param>
    /// <returns>
    /// The distance in metres along the left hand normal of the road, so that lane 1 (the rightmost
    /// lane of the direction of travel) ends up on the correct side of the road.
    /// </returns>
    /// <remarks>
    /// On a one-way road the lanes of the two directions share the same carriageway, so they are
    /// counted outwards from the centre line and a vehicle moving backwards uses the same lane
    /// numbers mirrored. On a two-way road each direction owns half of the road.
    /// </remarks>
    private static double GetLaneOffsetMetres(Vehicle vehicle)
    {
        Road road = vehicle.CurrentRoad;
        int lanes = road.LanesPerDirection;
        int lane = Math.Clamp(vehicle.LaneNumber, 1, lanes);
        double laneWidth = SimulationConstants.LaneWidthMetres;

        if (road.Type == RoadType.OneWay)
        {
            // Lanes are counted from the right hand side of the carriageway, which for a one-way
            // road means the two halves are mirrored around the centre line.
            bool isRightHandHalf = lane <= lanes / 2.0;
            double distanceFromRight = (lane - 0.5) * laneWidth;
            double halfCarriageway = lanes * laneWidth / 2.0;
            double offsetFromCentre = distanceFromRight - halfCarriageway;

            return isRightHandHalf ? -offsetFromCentre : offsetFromCentre;
        }

        // Both directions use the same offset on their own side of the centre line.
        return (lane - 0.5) * laneWidth;
    }

    /// <summary>Computes the angle from which traffic approaches a traffic light.</summary>
    /// <param name="light">Light to inspect.</param>
    /// <returns>The approach direction in radians.</returns>
    private static double GetApproachAngle(TrafficLight light)
    {
        Road road = light.ControlledRoad;

        // A light stands at the end of the road that traffic leaves the junction by, so the road
        // direction points away from the junction when the light controls the start of the road.
        return light.ControlledIntersection == road.StartIntersection
            ? road.Angle
            : road.Angle + Math.PI;
    }
}
