using Chaos.Packets.Abstractions.Definitions;

namespace Ouroboros.Networking;

/// <summary>One observed or injected packet, captured for the packet console.</summary>
public sealed class PacketLogEntry
{
    public PacketLogEntry(DateTime timestamp, PacketDirection direction, byte[] data)
    {
        Timestamp = timestamp;
        Direction = direction;
        Data = data;
    }

    public DateTime Timestamp { get; }
    public PacketDirection Direction { get; }

    /// <summary>The full cleartext wire frame: <c>[0xAA][len][len][opcode][seq?][body]</c>.</summary>
    public byte[] Data { get; }

    /// <summary>The opcode byte (frame offset 3).</summary>
    public byte OpCode => Data.Length > 3 ? Data[3] : (byte)0;

    /// <summary>Friendly opcode name from the Chaos enums for this direction, falling back to hex.</summary>
    public string OpCodeName => Direction == PacketDirection.ClientToServer
        ? Enum.IsDefined((ClientOpCode)OpCode) ? ((ClientOpCode)OpCode).ToString() : $"0x{OpCode:X2}"
        : Enum.IsDefined((ServerOpCode)OpCode) ? ((ServerOpCode)OpCode).ToString() : $"0x{OpCode:X2}";

    /// <summary>Space-separated hex dump of the whole frame.</summary>
    public string Hex => string.Join(' ', Data.Select(static b => b.ToString("X2")));

    /// <summary>Printable-ASCII view of the whole frame (non-printable bytes shown as '.').</summary>
    public string Ascii => new(Data.Select(static b => b is >= 32 and < 127 ? (char)b : '.').ToArray());
}
