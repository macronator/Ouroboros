namespace Ouroboros.Networking;

/// <summary>
///     Builds Dark Ages wire frames: <c>[0xAA][2-byte BE length][opcode][seq?][body]</c>. The sequence
///     byte is only present for encrypted opcodes; the length field counts everything after itself
///     (opcode + optional sequence + body), matching <c>size = (buf[1] &lt;&lt; 8) + buf[2] + 3</c>.
/// </summary>
public static class PacketFrame
{
    public const byte Signature = 0xAA;

    public static byte[] Build(byte opCode, ReadOnlySpan<byte> body, bool encrypted, byte sequence)
    {
        var contentLength = 1 + (encrypted ? 1 : 0) + body.Length;
        var frame = new byte[3 + contentLength];

        frame[0] = Signature;
        frame[1] = (byte)((contentLength >> 8) & 0xFF);
        frame[2] = (byte)(contentLength & 0xFF);
        frame[3] = opCode;

        var offset = 4;
        if (encrypted)
            frame[offset++] = sequence;

        body.CopyTo(frame.AsSpan(offset));

        return frame;
    }
}
