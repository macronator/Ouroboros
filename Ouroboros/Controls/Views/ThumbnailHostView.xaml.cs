using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ouroboros.ViewModel;

namespace Ouroboros.Controls.Views;

/// <summary>
///     Interaction logic for ThumbnailHostView.xaml. Hosts a DWM thumbnail of the selected client's window,
///     kept aligned to the <c>PreviewRegion</c> placeholder by a timer (so it tracks resizes/tab switches).
/// </summary>
public sealed partial class ThumbnailHostView
{
    private readonly ThumbnailPreview Preview = new();
    private readonly DispatcherTimer Timer = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public ThumbnailHostView()
    {
        InitializeComponent();

        Timer.Tick += (_, _) => UpdatePreview();
        Loaded += (_, _) => Timer.Start();
        Unloaded += (_, _) =>
        {
            Timer.Stop();
            Preview.Dispose();
        };
    }

    private ThumbnailViewModel? ViewModel => DataContext as ThumbnailViewModel;

    private void UpdatePreview()
    {
        var window = Window.GetWindow(this);
        var source = ViewModel?.SourceHandle ?? nint.Zero;

        //nothing to show unless this tab is visible and we have both windows and a laid-out region
        if (window is null || (source == nint.Zero) || !IsVisible || (PreviewRegion.ActualWidth <= 0))
        {
            Preview.Hide();

            return;
        }

        var dest = new WindowInteropHelper(window).Handle;

        if (dest == nint.Zero)
        {
            Preview.Hide();

            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var topLeft = PreviewRegion.TransformToAncestor(window).Transform(new Point(0, 0));

        var rect = new PInvoke.Rect
        {
            Left = (int)(topLeft.X * dpi.DpiScaleX),
            Top = (int)(topLeft.Y * dpi.DpiScaleY),
            Right = (int)((topLeft.X + PreviewRegion.ActualWidth) * dpi.DpiScaleX),
            Bottom = (int)((topLeft.Y + PreviewRegion.ActualHeight) * dpi.DpiScaleY)
        };

        Preview.Update(dest, source, rect);
    }
}
