using Ouroboros.Defintions;
using Ouroboros.PInvoke;

namespace Ouroboros.Controls;

/// <summary>
///     Wraps a DWM thumbnail: mirrors a source window (a game client) into a rectangle of a destination window
///     (our main window), composited by DWM on top of the WPF content. Re-registers when the source or
///     destination changes; <see cref="Hide" /> keeps the registration but stops drawing; <see cref="Dispose" />
///     unregisters. All coordinates are physical pixels in the destination window's client space.
/// </summary>
public sealed class ThumbnailPreview : IDisposable
{
    private nint Thumb;
    private nint Dest;
    private nint Source;

    public void Update(nint dest, nint source, Rect destinationRect)
    {
        if ((dest != Dest) || (source != Source))
        {
            Release();

            if ((dest == nint.Zero) || (source == nint.Zero))
                return;

            if (UnsafeNativeMethods.DwmRegisterThumbnail(dest, source, out var thumb) != 0)
                return;

            Thumb = thumb;
            Dest = dest;
            Source = source;
        }

        if (Thumb == nint.Zero)
            return;

        var props = new ThumbnailProperties
        {
            Flags = ThumbnailFlags.RectDestination | ThumbnailFlags.Visible | ThumbnailFlags.SourceClientAreaOnly,
            DestinationRect = destinationRect,
            Visible = true,
            OnlyClientRect = true
        };

        UnsafeNativeMethods.DwmUpdateThumbnailProperties(Thumb, ref props);
    }

    public void Hide()
    {
        if (Thumb == nint.Zero)
            return;

        var props = new ThumbnailProperties
        {
            Flags = ThumbnailFlags.Visible,
            Visible = false
        };

        UnsafeNativeMethods.DwmUpdateThumbnailProperties(Thumb, ref props);
    }

    public void Release()
    {
        if (Thumb != nint.Zero)
        {
            UnsafeNativeMethods.DwmUnregisterThumbnail(Thumb);
            Thumb = nint.Zero;
        }

        Dest = nint.Zero;
        Source = nint.Zero;
    }

    public void Dispose() => Release();
}
