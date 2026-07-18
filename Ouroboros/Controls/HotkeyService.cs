using System.Windows.Interop;
using Ouroboros.PInvoke;

namespace Ouroboros.Controls;

/// <summary>
///     Registers system-wide hotkeys against a window and dispatches WM_HOTKEY to callbacks. The main window
///     uses it to bind bot-feature toggles to Ctrl+Shift+F-keys, so they work even while the game has focus.
///     Dispose to unregister and detach the message hook.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    public const uint ModAlt = 0x1;
    public const uint ModControl = 0x2;
    public const uint ModShift = 0x4;

    private const int WmHotkey = 0x0312;

    private readonly HwndSource Source;
    private readonly nint Handle;
    private readonly Dictionary<int, Action> Actions = new();
    private int NextId = 1;

    public HotkeyService(HwndSource source)
    {
        Source = source;
        Handle = source.Handle;
        source.AddHook(WndProc);
    }

    /// <summary>Registers a hotkey and returns whether the OS accepted it (false if it's already taken).</summary>
    public bool Register(uint virtualKey, Action action, uint modifiers = 0)
    {
        var id = NextId++;

        if (!UnsafeNativeMethods.RegisterHotKey(Handle, id, modifiers, virtualKey))
            return false;

        Actions[id] = action;

        return true;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if ((msg == WmHotkey) && Actions.TryGetValue((int)wParam, out var action))
        {
            action();
            handled = true;
        }

        return nint.Zero;
    }

    public void Dispose()
    {
        Source.RemoveHook(WndProc);

        foreach (var id in Actions.Keys)
            UnsafeNativeMethods.UnregisterHotKey(Handle, id);

        Actions.Clear();
    }
}
