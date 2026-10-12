using System.Windows;
using TrafficSimulation.App.Rendering;
using TrafficSimulation.Engine.Geometry;

namespace TrafficSimulation.Tests.Rendering;

/// <summary>
/// Tests for <see cref="MapViewport"/>, the only place where simulation metres are turned into
/// screen pixels.
/// </summary>
/// <remarks>
/// The conversion is easy to get subtly wrong — an inverted pan, a zoom step applied to the wrong
/// range — and every symptom shows up as "the map is in the wrong place" rather than as an
/// exception. These tests pin the arithmetic down.
/// </remarks>
public class MapViewportTests
{
    [Fact]
    public void ANewViewportStartsAtTheDefaultZoomWithNoPanning()
    {
        MapViewport viewport = new();

        Assert.Equal(MapViewport.DefaultPixelsPerMetre, viewport.PixelsPerMetre);
        Assert.Equal(0, viewport.PanX);
        Assert.Equal(0, viewport.PanY);
    }

    [Fact]
    public void ToScreen_ScalesMetresIntoPixels()
    {
        MapViewport viewport = new();
        viewport.SetZoom(5);

        Point screen = viewport.ToScreen(new Vector2(10, 4));

        Assert.Equal(50, screen.X, 10);
        Assert.Equal(20, screen.Y, 10);
    }

    [Fact]
    public void ToWorld_IsTheInverseOfToScreen()
    {
        MapViewport viewport = new();
        viewport.SetZoom(3);
        viewport.Pan(40, -25);

        Vector2 world = new(12.5, -7.25);
        Vector2 roundTrip = viewport.ToWorld(viewport.ToScreen(world));

        Assert.Equal(world.X, roundTrip.X, 10);
        Assert.Equal(world.Y, roundTrip.Y, 10);
    }

    [Fact]
    public void PanningShiftsEverythingByTheSameNumberOfPixels()
    {
        MapViewport viewport = new();
        Point before = viewport.ToScreen(new Vector2(10, 10));

        viewport.Pan(30, -12);

        Point after = viewport.ToScreen(new Vector2(10, 10));

        Assert.Equal(before.X + 30, after.X, 10);
        Assert.Equal(before.Y - 12, after.Y, 10);
    }

    [Fact]
    public void LengthConversionsRoundTrip()
    {
        MapViewport viewport = new();
        viewport.SetZoom(2.5);

        Assert.Equal(25, viewport.ToPixels(10), 10);
        Assert.Equal(10, viewport.ToMetres(25), 10);
    }

    [Fact]
    public void ZoomIn_StopsAtTheMaximumZoom()
    {
        MapViewport viewport = new();

        for (int i = 0; i < 100; i++)
        {
            viewport.ZoomIn();
        }

        Assert.Equal(MapViewport.MaxPixelsPerMetre, viewport.PixelsPerMetre, 10);
        Assert.True(viewport.IsFullyZoomedIn);

        // Once fully zoomed in, further requests must report that nothing changed so that the caller
        // can skip a redraw.
        Assert.False(viewport.ZoomIn());
    }

    [Fact]
    public void ZoomOut_StopsAtTheMinimumZoom()
    {
        MapViewport viewport = new();

        for (int i = 0; i < 100; i++)
        {
            viewport.ZoomOut();
        }

        Assert.Equal(MapViewport.MinPixelsPerMetre, viewport.PixelsPerMetre, 10);
        Assert.True(viewport.IsFullyZoomedOut);
        Assert.False(viewport.ZoomOut());
    }

    [Fact]
    public void ZoomStepsAreCoarseWhenFarOutAndFineWhenCloseUp()
    {
        MapViewport viewport = new();
        viewport.SetZoom(MapViewport.MaxPixelsPerMetre);

        // Above the threshold the zoom moves in coarse steps.
        viewport.SetZoom(8);
        viewport.ZoomOut();
        Assert.Equal(8 - MapViewport.CoarseZoomStep, viewport.PixelsPerMetre, 10);

        // At or below it the steps get finer, which makes close-up navigation usable.
        viewport.SetZoom(4);
        viewport.ZoomOut();
        Assert.Equal(4 - MapViewport.FineZoomStep, viewport.PixelsPerMetre, 10);
    }

    [Fact]
    public void ZoomIsClampedToTheSupportedRange()
    {
        MapViewport viewport = new();

        viewport.SetZoom(1000);
        Assert.Equal(MapViewport.MaxPixelsPerMetre, viewport.PixelsPerMetre, 10);

        viewport.SetZoom(-5);
        Assert.Equal(MapViewport.MinPixelsPerMetre, viewport.PixelsPerMetre, 10);

        viewport.SetZoom(double.NaN);
        Assert.Equal(MapViewport.MinPixelsPerMetre, viewport.PixelsPerMetre, 10);
    }

    [Fact]
    public void Reset_RestoresTheDefaultZoomAndClearsPanning()
    {
        MapViewport viewport = new();
        viewport.SetZoom(9);
        viewport.Pan(120, -60);

        viewport.Reset();

        Assert.Equal(MapViewport.DefaultPixelsPerMetre, viewport.PixelsPerMetre, 10);
        Assert.Equal(0, viewport.PanX);
        Assert.Equal(0, viewport.PanY);
        Assert.Equal(new Point(0, 0), viewport.ToScreen(Vector2.Zero));
    }
}
