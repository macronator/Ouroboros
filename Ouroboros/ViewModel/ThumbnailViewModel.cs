using Ouroboros.Client;
using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>Exposes the selected client's game-window handle for the preview tab to mirror via a DWM thumbnail.</summary>
public sealed class ThumbnailViewModel : NotifyPropertyChangedBase
{
    private nint _sourceHandle;

    /// <summary>The window to mirror, or <see cref="nint.Zero" /> when no client window is attached.</summary>
    public nint SourceHandle
    {
        get => _sourceHandle;
        private set => SetField(ref _sourceHandle, value);
    }

    public bool HasSource => _sourceHandle != nint.Zero;

    public void Bind(DarkAgesClient? client)
    {
        var handle = client?.DaWindow?.WindowHandle ?? nint.Zero;

        if (handle == _sourceHandle)
            return;

        SourceHandle = handle;
        OnPropertyChanged(nameof(HasSource));
    }
}
