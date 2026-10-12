using System.Windows;
using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.App.Rendering;

/// <summary>
/// Converts between simulation coordinates (metres) and screen coordinates (pixels).
/// </summary>
/// <remarks>
/// <para>
/// The engine works purely in metres. Everything that has to appear on a canvas goes through this
/// class, which owns the two pieces of state that describe the current view: the zoom level and the
/// panning offset.
/// </para>
/// <para>
/// Keeping the conversion in one place means the zoom buttons, the mouse wheel, the pan gesture and
/// every draw call all agree on what "one metre" means, which was previously expressed by a mutable
/// global constant that every part of the program wrote to.
/// </para>
/// </remarks>
public sealed class MapViewport
{
    /// <summary>Smallest number of pixels one metre may be drawn with, that is the most zoomed-out view.</summary>
    public const double MinPixelsPerMetre = 1;

    /// <summary>Largest number of pixels one metre may be drawn with, that is the most zoomed-in view.</summary>
    public const double MaxPixelsPerMetre = 10;

    /// <summary>Default zoom level, at which one metre is drawn as five pixels.</summary>
    public const double DefaultPixelsPerMetre = 5;

    /// <summary>Difference applied by the zoom buttons in the coarse range of the scale.</summary>
    public const double CoarseZoomStep = 0.5;

    /// <summary>Difference applied by the zoom buttons in the fine range of the scale.</summary>
    public const double FineZoomStep = 0.25;

    /// <summary>Zoom level at and below which the fine step is used.</summary>
    public const double FineZoomThreshold = 5;

    private double _panX;
    private double _panY;

    /// <summary>Number of pixels one metre is currently drawn with.</summary>
    public double PixelsPerMetre { get; private set; } = DefaultPixelsPerMetre;

    /// <summary>Horizontal panning offset, in pixels.</summary>
    public double PanX => _panX;

    /// <summary>Vertical panning offset, in pixels.</summary>
    public double PanY => _panY;

    /// <summary>Factor that converts a length in metres into a length in pixels.</summary>
    public double PixelsPerMetreFactor => PixelsPerMetre;

    /// <summary>Whether the view is zoomed all the way in.</summary>
    public bool IsFullyZoomedIn => PixelsPerMetre >= MaxPixelsPerMetre - 1e-9;

    /// <summary>Whether the view is zoomed all the way out.</summary>
    public bool IsFullyZoomedOut => PixelsPerMetre <= MinPixelsPerMetre + 1e-9;

    /// <summary>
    /// Converts a point of the simulation into a point on the canvas.
    /// </summary>
    /// <param name="point">Position in metres.</param>
    /// <returns>The position in pixels, including the current panning offset.</returns>
    public Point ToScreen(Vector2 point)
        => new((point.X * PixelsPerMetre) + _panX, (point.Y * PixelsPerMetre) + _panY);

    /// <summary>
    /// Converts a canvas point back into simulation coordinates.
    /// </summary>
    /// <param name="screenPoint">Position in pixels, relative to the canvas.</param>
    /// <returns>The position in metres.</returns>
    public Vector2 ToWorld(Point screenPoint)
        => new((screenPoint.X - _panX) / PixelsPerMetre, (screenPoint.Y - _panY) / PixelsPerMetre);

    /// <summary>Converts a length in metres into a length in pixels.</summary>
    /// <param name="metres">Length in metres.</param>
    public double ToPixels(double metres) => metres * PixelsPerMetre;

    /// <summary>Converts a length in pixels into a length in metres.</summary>
    /// <param name="pixels">Length in pixels.</param>
    public double ToMetres(double pixels) => pixels / PixelsPerMetre;

    /// <summary>Pans the view by a screen space delta.</summary>
    /// <param name="deltaX">Horizontal movement in pixels.</param>
    /// <param name="deltaY">Vertical movement in pixels.</param>
    public void Pan(double deltaX, double deltaY)
    {
        _panX += deltaX;
        _panY += deltaY;
    }

    /// <summary>
    /// Zooms one step in, using the finer step when the view is already close up.
    /// </summary>
    /// <returns><see langword="true"/> when the zoom level actually changed.</returns>
    public bool ZoomIn()
    {
        if (IsFullyZoomedIn)
        {
            return false;
        }

        double step = PixelsPerMetre > FineZoomThreshold ? CoarseZoomStep : FineZoomStep;
        PixelsPerMetre = Math.Min(MaxPixelsPerMetre, PixelsPerMetre + step);
        return true;
    }

    /// <summary>
    /// Zooms one step out, using the finer step while the view is still close up.
    /// </summary>
    /// <returns><see langword="true"/> when the zoom level actually changed.</returns>
    public bool ZoomOut()
    {
        if (IsFullyZoomedOut)
        {
            return false;
        }

        double step = PixelsPerMetre > FineZoomThreshold ? CoarseZoomStep : FineZoomStep;
        PixelsPerMetre = Math.Max(MinPixelsPerMetre, PixelsPerMetre - step);
        return true;
    }

    /// <summary>Restores the default zoom level and clears the panning offset.</summary>
    public void Reset()
    {
        PixelsPerMetre = DefaultPixelsPerMetre;
        _panX = 0;
        _panY = 0;
    }

    /// <summary>Sets the zoom level directly, clamped to the supported range.</summary>
    /// <param name="pixelsPerMetre">Desired zoom level.</param>
    public void SetZoom(double pixelsPerMetre)
        => PixelsPerMetre = Math.Clamp(pixelsPerMetre, MinPixelsPerMetre, MaxPixelsPerMetre);
}
