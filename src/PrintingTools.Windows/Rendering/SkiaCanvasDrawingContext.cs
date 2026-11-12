using System;
using System.Reflection;
using Avalonia;
using Avalonia.Media;
using Avalonia.Skia.Helpers;
using SkiaSharp;

namespace PrintingTools.Windows.Rendering;

/// <summary>
/// Wraps an external Skia canvas into an Avalonia <see cref="DrawingContext"/> so page-level
/// transforms can be applied through the PushTransform stack (the same mechanism the preview
/// path uses). Avalonia 12 keeps <c>DrawingContextHelper.WrapSkiaCanvas(SKCanvas, Vector)</c>
/// and <c>PlatformDrawingContext</c> internal, so they are resolved by reflection - the same
/// style already used by <see cref="PrintingTools.Core.Rendering.PrintPageRenderer"/> for
/// <c>ImmediateRenderer</c>. When the internals cannot be resolved, callers fall back to
/// <c>DrawingContextHelper.RenderAsync</c>.
/// </summary>
internal static class SkiaCanvasDrawingContext
{
    private static readonly MethodInfo? WrapMethod = typeof(DrawingContextHelper)
        .GetMethod(
            "WrapSkiaCanvas",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(SKCanvas), typeof(Vector) },
            null);

    private static readonly Type? PlatformContextType =
        Type.GetType("Avalonia.Media.PlatformDrawingContext, Avalonia.Base", throwOnError: false);

    public static bool TryCreate(SKCanvas canvas, Vector dpi, out DrawingContext? context, out string? failure)
    {
        context = null;
        failure = null;
        if (WrapMethod is null)
        {
            failure = "DrawingContextHelper.WrapSkiaCanvas(SKCanvas, Vector) not found";
            return false;
        }

        if (PlatformContextType is null)
        {
            failure = "Avalonia.Media.PlatformDrawingContext type not found";
            return false;
        }

        try
        {
            var impl = WrapMethod.Invoke(null, new object?[] { canvas, dpi });
            if (impl is null)
            {
                failure = "WrapSkiaCanvas returned null";
                return false;
            }

            // PlatformDrawingContext(IDrawingContextImpl impl, bool useLayers) - layer mode needs
            // a composition visual and is not available for foreign canvases.
            var created = Activator.CreateInstance(
                PlatformContextType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { impl, false },
                culture: null);

            if (created is DrawingContext drawingContext)
            {
                context = drawingContext;
                return true;
            }

            failure = $"created instance is {created?.GetType().FullName ?? "null"}, not a DrawingContext";
            (created as IDisposable)?.Dispose();
            return false;
        }
        catch (Exception ex)
        {
            failure = ex.ToString();
            return false;
        }
    }
}
