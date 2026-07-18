using Ouroboros.PInvoke;

namespace Ouroboros.Memory;

/// <summary>
///     Posts keyboard input to a Dark Ages client window via Win32 messages, for actions driven through the
///     client's own UI (function-key spell slots, chat) rather than the protocol. Owner-authorized and
///     client-side only. A zero window handle makes every call a no-op.
/// </summary>
public sealed class InputSender
{
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmChar = 0x0102;

    private readonly nint WindowHandle;

    public InputSender(nint windowHandle) => WindowHandle = windowHandle;

    /// <summary>Posts a key-down then key-up for the given virtual-key code.</summary>
    public void KeyPress(int virtualKey)
    {
        if (WindowHandle == nint.Zero)
            return;

        UnsafeNativeMethods.PostMessage(WindowHandle, WmKeyDown, (nint)virtualKey, BuildLParam(virtualKey, up: false));
        UnsafeNativeMethods.PostMessage(WindowHandle, WmKeyUp, (nint)virtualKey, BuildLParam(virtualKey, up: true));
    }

    /// <summary>Posts a single character to the window's input (WM_CHAR).</summary>
    public void TypeChar(char character)
    {
        if (WindowHandle != nint.Zero)
            UnsafeNativeMethods.PostMessage(WindowHandle, WmChar, (nint)character, nint.Zero);
    }

    /// <summary>Types a whole string, one character at a time.</summary>
    public void TypeText(string text)
    {
        foreach (var character in text)
            TypeChar(character);
    }

    //WM_KEYDOWN/UP lParam: repeat count 1, the hardware scan code, and (on key-up) the transition/prior-state bits.
    private static nint BuildLParam(int virtualKey, bool up)
    {
        var scan = UnsafeNativeMethods.MapVirtualKey((uint)virtualKey, 0 /* MAPVK_VK_TO_VSC */);
        var lParam = 1u | (scan << 16);

        if (up)
            lParam |= 0xC0000000;

        return unchecked((nint)lParam);
    }
}
