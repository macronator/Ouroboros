using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>
///     Scaffold view-model for the packet-console tab. The console core already exists
///     (<c>Networking/PacketConsole.cs</c>); this tab will bind its live log stream and the
///     send-to-client / send-to-server craft box. Kept minimal and designer-ready until then.
/// </summary>
public sealed class PacketConsoleViewModel : NotifyPropertyChangedBase
{
    public string Title => "Packet Console";

    public string Placeholder
        => "Live packet log (hex + opcode names, direction, filtering) and a craft box to inject "
           + "packets toward the client or server. Backed by Networking/PacketConsole.cs — UI binding pending.";
}
