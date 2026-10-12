using System.Windows;
using System.Windows.Input;
using TrafficSimulation.App.Rendering;
using TrafficSimulation.Engine;
using TrafficSimulation.Engine.Domain;
using TrafficSimulation.Engine.Geometry;
using TrafficSimulation.Engine.Simulation;

namespace TrafficSimulation.App.Interaction;

/// <summary>The tool currently selected on the map.</summary>
public enum MapTool
{
    /// <summary>Clicking selects roads and intersections, dragging pans the map.</summary>
    Select,

    /// <summary>Clicking places the first end point of a new road.</summary>
    PlaceRoadStart,

    /// <summary>The first end point exists; clicking places the second one.</summary>
    PlaceRoadEnd,
}

/// <summary>What a click on empty space should be interpreted as.</summary>
public enum MapClickTarget
{
    /// <summary>Nothing of interest was under the cursor.</summary>
    Empty,

    /// <summary>An intersection was under the cursor.</summary>
    Intersection,

    /// <summary>The body of a road was under the cursor.</summary>
    Road,
}

/// <summary>
/// Translates mouse gestures on the map into edits of the <see cref="City"/>.
/// </summary>
/// <remarks>
/// <para>
/// The controller owns the little state machine behind the map: which tool is active, whether a drag
/// is in progress, and which road is currently being previewed. It knows nothing about WPF beyond
/// points and buttons, and it never draws anything: instead it reports what happened so the window
/// can refresh the view.
/// </para>
/// <para>
/// Keeping this logic out of the window's event handlers is what makes map editing testable — a unit
/// test can drive the state machine with synthetic points and assert on the resulting city.
/// </para>
/// </remarks>
public sealed class MapInputController
{
    /// <summary>Radius, in pixels, within which a click selects an intersection rather than a road.</summary>
    public const double IntersectionHitRadiusPixels = 12;

    /// <summary>Maximum distance, in pixels, from a road centre line for a click to hit it.</summary>
    public const double RoadHitTolerancePixels = 12;

    private readonly City _city;

    private Point _dragOrigin;
    private bool _isDragging;
    /// <summary>Creates a controller for a city.</summary>
    /// <param name="city">City the map shows.</param>
    public MapInputController(City city) => _city = city ?? throw new ArgumentNullException(nameof(city));

    /// <summary>Raised whenever the visible state of the map changed and a redraw is required.</summary>
    public event EventHandler? RedrawRequested;

    /// <summary>Raised when the user asks to open the settings of a road.</summary>
    public event EventHandler<Road>? RoadSelected;

    /// <summary>Raised when the user asks to open the settings of an intersection.</summary>
    public event EventHandler<Intersection>? IntersectionSelected;

    /// <summary>Tool currently active on the map.</summary>
    public MapTool Tool { get; private set; } = MapTool.Select;

    /// <summary>Road being previewed while the user is drawing, if any.</summary>
    /// <remarks>
    /// The preview is only a geometry plus the settings the road would get; nothing is added to the
    /// city until the user commits, so an abandoned road never leaves a trace on the map.
    /// </remarks>
    public RoadPreview? Preview { get; private set; }

    /// <summary>Whether the previewed road could legally be created where it currently ends.</summary>
    public bool IsPreviewAllowed { get; private set; } = true;

    /// <summary>Intersection the previewed road starts from, if a road is being drawn.</summary>
    public Intersection? PreviewStart { get; private set; }

    /// <summary>Lanes the previewed road would have in each direction.</summary>
    public int PreviewLanesPerDirection { get; private set; } = SimulationConstants.MinLanesPerDirection;

    /// <summary>Whether a pan drag is currently in progress.</summary>
    public bool IsPanning => _isDragging;

    /// <summary>Switches to the "draw a road" tool.</summary>
    public void BeginRoadPlacement()
    {
        ClearPreview();
        Tool = MapTool.PlaceRoadStart;
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cancels road drawing and returns to the selection tool.</summary>
    public void CancelRoadPlacement()
    {
        ClearPreview();
        Tool = MapTool.Select;
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Handles a left button press at a position on the map.
    /// </summary>
    /// <param name="screenPosition">Cursor position in canvas pixels.</param>
    /// <param name="viewport">Viewport used to convert the position into metres.</param>
    public void OnLeftButtonDown(Point screenPosition, MapViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        if (Tool is MapTool.PlaceRoadStart or MapTool.PlaceRoadEnd)
        {
            HandleRoadPlacementClick(screenPosition, viewport);
            return;
        }

        _dragOrigin = screenPosition;
        _isDragging = true;
    }

    /// <summary>Handles cursor movement, updating the preview or panning the view.</summary>
    /// <param name="screenPosition">Cursor position in canvas pixels.</param>
    /// <param name="viewport">Viewport used to convert the position into metres.</param>
    /// <param name="isLeftButtonPressed">Whether the left mouse button is currently held down.</param>
    /// <returns><see langword="true"/> when the view has to be redrawn.</returns>
    public bool OnMouseMove(Point screenPosition, MapViewport viewport, bool isLeftButtonPressed)
    {
        ArgumentNullException.ThrowIfNull(viewport);

        if (Tool == MapTool.PlaceRoadEnd && Preview is not null && !isLeftButtonPressed)
        {
            UpdatePreview(screenPosition, viewport);
            return true;
        }

        if (_isDragging && isLeftButtonPressed)
        {
            double deltaX = screenPosition.X - _dragOrigin.X;
            double deltaY = screenPosition.Y - _dragOrigin.Y;

            _dragOrigin = screenPosition;

            // Panning is applied to the viewport, so the next redraw places everything correctly.
            viewport.Pan(deltaX, deltaY);
            return true;
        }

        return false;
    }

    /// <summary>Handles the release of the left mouse button.</summary>
    public void OnLeftButtonUp() => _isDragging = false;

    /// <summary>Handles a click on an intersection.</summary>
    /// <param name="intersection">Intersection that was clicked.</param>
    /// <param name="viewport">Viewport used to convert positions into metres.</param>
    public void OnIntersectionClicked(Intersection intersection, MapViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(intersection);
        ArgumentNullException.ThrowIfNull(viewport);

        switch (Tool)
        {
            case MapTool.PlaceRoadStart:
                StartPreview(intersection);
                break;

            case MapTool.PlaceRoadEnd when Preview is not null && IsPreviewAllowed:
                CompleteRoad(intersection);
                break;

            case MapTool.Select:
                IntersectionSelected?.Invoke(this, intersection);
                break;
        }
    }

    /// <summary>
    /// Handles a click on the body of a road.
    /// </summary>
    /// <param name="road">Road that was clicked.</param>
    /// <param name="screenPosition">Cursor position in canvas pixels.</param>
    /// <param name="viewport">Viewport used to convert positions into metres.</param>
    /// <remarks>
    /// While a road is being drawn, clicking the body of an existing road splits it and continues
    /// drawing from the new junction, which is how the network grows new branches.
    /// </remarks>
    public void OnRoadClicked(Road road, Point screenPosition, MapViewport viewport)
    {
        ArgumentNullException.ThrowIfNull(road);
        ArgumentNullException.ThrowIfNull(viewport);

        if (Tool == MapTool.Select)
        {
            RoadSelected?.Invoke(this, road);
            return;
        }

        if (Tool == MapTool.PlaceRoadEnd && !IsPreviewAllowed)
        {
            return;
        }

        Vector2 world = viewport.ToWorld(screenPosition);
        Intersection junction = CreateJunctionOn(road, world);

        if (Tool == MapTool.PlaceRoadStart)
        {
            StartPreview(junction);
        }
        else
        {
            CompleteRoad(junction);
        }
    }

    /// <summary>Creates the junction that splits a road at the given point.</summary>
    /// <param name="road">Road to split.</param>
    /// <param name="worldPoint">Point, in metres, where the user clicked.</param>
    /// <returns>The junction that the click resolved to.</returns>
    private Intersection CreateJunctionOn(Road road, Vector2 worldPoint)
    {
        // The junction goes through the city index so that the roads meeting here share one node.
        Intersection junction = _city.GetOrCreateIntersection(CityEditor.ProjectOntoRoad(road, worldPoint));

        // The road really has to be split, otherwise the new road would start in the middle of an
        // existing segment and traffic could never reach it.
        _city.Editor.SplitRoad(road, junction);
        return junction;
    }

    /// <summary>Starts previewing a road that begins at the given intersection.</summary>
    /// <param name="start">Intersection where the road begins.</param>
    private void StartPreview(Intersection start)
    {
        PreviewStart = start;
        PreviewLanesPerDirection = _city.Settings.DefaultLanesPerDirection;
        Preview = new RoadPreview(
            new LineSegment(start.Position, start.Position),
            GetPreviewWidthMetres(),
            IsAllowed: false);

        IsPreviewAllowed = false;
        Tool = MapTool.PlaceRoadEnd;
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves the free end of the previewed road to follow the cursor.</summary>
    /// <param name="screenPosition">Cursor position in canvas pixels.</param>
    /// <param name="viewport">Viewport used to convert the position into metres.</param>
    private void UpdatePreview(Point screenPosition, MapViewport viewport)
    {
        Intersection start = PreviewStart!;
        Vector2 world = viewport.ToWorld(screenPosition);

        // Snap to a nearby junction so that consecutive roads actually connect.
        Intersection? nearby = _city.Editor.FindIntersectionAt(
            world,
            viewport.ToMetres(IntersectionHitRadiusPixels));

        Vector2 end = nearby is not null && nearby != start ? nearby.Position : world;

        Intersection candidate = _city.GetOrCreateIntersection(end);
        IsPreviewAllowed = candidate != start && _city.Editor.CanCreateRoad(start, candidate);

        Preview = new RoadPreview(new LineSegment(start.Position, end), GetPreviewWidthMetres(), IsPreviewAllowed);
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Finishes the road that is being drawn.</summary>
    /// <param name="end">Intersection where the road ends.</param>
    private void CompleteRoad(Intersection end)
    {
        Intersection start = PreviewStart!;

        if (_city.Editor.CanCreateRoad(start, end))
        {
            Road road = _city.Editor.CreateRoad(start, end);
            road.LanesPerDirection = PreviewLanesPerDirection;
        }

        ClearPreview();
        RedrawRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops the preview and returns the tool to its idle selection state.</summary>
    private void ClearPreview()
    {
        Preview = null;
        PreviewStart = null;
        PreviewLanesPerDirection = _city.Settings.DefaultLanesPerDirection;
        IsPreviewAllowed = false;
        Tool = MapTool.Select;
    }

    /// <summary>
    /// Width the previewed road would have, so that the preview is drawn at the right thickness.
    /// </summary>
    private double GetPreviewWidthMetres()
        => PreviewLanesPerDirection * SimulationConstants.LaneWidthMetres * 2;

    /// <summary>Determines whether two positions are the same point, within floating point noise.</summary>
    /// <param name="first">First position, in metres.</param>
    /// <param name="second">Second position, in metres.</param>
    private static bool IsSamePoint(Vector2 first, Vector2 second)
        => first.DistanceTo(second) < 1e-6;

    /// <summary>Handles a click while the road tool is active.</summary>
    /// <param name="screenPosition">Cursor position in canvas pixels.</param>
    /// <param name="viewport">Viewport used to convert the position into metres.</param>
    private void HandleRoadPlacementClick(Point screenPosition, MapViewport viewport)
    {
        Vector2 world = viewport.ToWorld(screenPosition);
        double intersectionRadius = viewport.ToMetres(IntersectionHitRadiusPixels);
        double roadTolerance = viewport.ToMetres(RoadHitTolerancePixels);

        Intersection? intersection = _city.Editor.FindIntersectionAt(world, intersectionRadius);
        if (intersection is not null)
        {
            OnIntersectionClicked(intersection, viewport);
            return;
        }

        Road? road = _city.Editor.FindRoadAt(world, roadTolerance);
        if (road is not null)
        {
            OnRoadClicked(road, screenPosition, viewport);
            return;
        }

        if (Tool == MapTool.PlaceRoadEnd && Preview is not null && IsPreviewAllowed)
        {
            CompleteRoad(_city.GetOrCreateIntersection(world));
        }
    }
}
