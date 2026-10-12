using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.App.Rendering;

/// <summary>
/// State of the intersection inspector preview.
/// </summary>
/// <param name="City">City the intersection belongs to.</param>
/// <param name="Intersection">Intersection being inspected.</param>
/// <param name="SelectedRoad">Road currently selected in the list, if any.</param>
public readonly record struct IntersectionRenderState(
    City City,
    Intersection Intersection,
    Road? SelectedRoad);

/// <summary>
/// Draws the close-up of a single intersection used by the intersection settings window.
/// </summary>
/// <remarks>
/// The junction is always drawn in the middle of the canvas and the roads are drawn relative to it,
/// so the preview is independent of where the junction sits on the main map. Unlike the main map this
/// view has a fixed zoom level, defined by <see cref="PixelsPerMetre"/>.
/// </remarks>
public sealed class IntersectionPreviewRenderer
{
    /// <summary>Fixed zoom level of the preview, in pixels per metre.</summary>
    public const double PixelsPerMetre = 5;

    private readonly Canvas _canvas;

    /// <summary>Creates a renderer for the preview canvas.</summary>
    /// <param name="canvas">Canvas to draw on.</param>
    public IntersectionPreviewRenderer(Canvas canvas)
        => _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

    /// <summary>Raised when the user clicks one of the drawn roads.</summary>
    public event EventHandler<Road>? RoadClicked;

    /// <summary>Draws the intersection, its roads and its traffic lights.</summary>
    /// <param name="state">What to draw.</param>
    public void Render(in IntersectionRenderState state)
    {
        ArgumentNullException.ThrowIfNull(state.City);
        ArgumentNullException.ThrowIfNull(state.Intersection);

        _canvas.Children.Clear();

        // Every road that touches the junction is drawn, so the user can pick any of them to give it
        // a traffic light.
        foreach (Road road in state.Intersection.AllRoads)
        {
            DrawRoad(road, state);
        }

        DrawIntersection(state);

        foreach (TrafficLight light in state.City.TrafficLights.At(state.Intersection))
        {
            DrawTrafficLight(light, state);
        }
    }

    /// <summary>Draws a single road of the junction, relative to the centre of the canvas.</summary>
    /// <param name="road">Road to draw.</param>
    /// <param name="state">State of the preview.</param>
    private void DrawRoad(Road road, in IntersectionRenderState state)
    {
        Point start = ToCanvas(road.StartIntersection.Position, state.Intersection);
        Point end = ToCanvas(road.EndIntersection.Position, state.Intersection);

        bool isSelected = ReferenceEquals(road, state.SelectedRoad);

        Line line = new()
        {
            X1 = start.X,
            Y1 = start.Y,
            X2 = end.X,
            Y2 = end.Y,
            Stroke = isSelected ? Brushes.Blue : Brushes.Black,
            StrokeThickness = Math.Max(1, road.Width * PixelsPerMetre),
            Tag = road,
            Opacity = road.Quality,
            ToolTip = road.Name,
        };

        line.MouseDown += (_, _) => RoadClicked?.Invoke(this, road);
        _canvas.Children.Add(line);
    }

    /// <summary>Draws the junction itself as a black square aligned with its roads.</summary>
    /// <param name="state">State of the preview.</param>
    private void DrawIntersection(in IntersectionRenderState state)
    {
        double size = Math.Max(1, state.Intersection.WidestRoadWidth * PixelsPerMetre);
        Point centre = new(_canvas.ActualWidth / 2, _canvas.ActualHeight / 2);

        Rectangle rectangle = new()
        {
            Width = size,
            Height = size,
            Fill = Brushes.Black,
            Opacity = state.Intersection.BestRoadQuality,
            RenderTransform = new RotateTransform(Angles.ToDegrees(state.Intersection.Angle), size / 2, size / 2),
            IsHitTestVisible = false,
        };

        Canvas.SetLeft(rectangle, centre.X - (size / 2));
        Canvas.SetTop(rectangle, centre.Y - (size / 2));
        _canvas.Children.Add(rectangle);
    }

    /// <summary>Draws the traffic light that controls one of the roads of the junction.</summary>
    /// <param name="light">Light to draw.</param>
    /// <param name="state">State of the preview.</param>
    private void DrawTrafficLight(TrafficLight light, in IntersectionRenderState state)
    {
        Point centre = new(_canvas.ActualWidth / 2, _canvas.ActualHeight / 2);

        // The light is offset towards the road it controls.
        double approachAngle = GetApproachAngle(light);
        double offsetX = centre.X + (Math.Cos(approachAngle) * SimulationConstants.LaneWidthMetres * PixelsPerMetre / 2);
        double offsetY = centre.Y + (Math.Sin(approachAngle) * SimulationConstants.LaneWidthMetres * PixelsPerMetre / 2);

        double diameter = Math.Max(1, SimulationConstants.TrafficLightDiameterMetres * PixelsPerMetre);
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
            Tag = light,
            IsHitTestVisible = false,
        };

        Canvas.SetLeft(lamp, offsetX - (diameter / 2));
        Canvas.SetTop(lamp, offsetY - (diameter / 2));
        _canvas.Children.Add(lamp);
    }

    /// <summary>Converts a position in metres into a canvas point, relative to the junction.</summary>
    /// <param name="position">Position in metres.</param>
    /// <param name="intersection">Junction drawn at the centre of the canvas.</param>
    private Point ToCanvas(Vector2 position, Intersection intersection)
        => new(
            (_canvas.ActualWidth / 2) + ((position.X - intersection.X) * PixelsPerMetre),
            (_canvas.ActualHeight / 2) + ((position.Y - intersection.Y) * PixelsPerMetre));

    /// <summary>Computes the angle from which traffic approaches a traffic light.</summary>
    /// <param name="light">Light to inspect.</param>
    private static double GetApproachAngle(TrafficLight light)
    {
        Road road = light.ControlledRoad;
        return light.ControlledIntersection == road.EndIntersection
            ? road.Angle + Math.PI
            : road.Angle;
    }
}
