using System.Globalization;
using System.Text;

namespace Ouroboros.Networking;

/// <summary>
///     Parses the packet-craft mini-language into a <see cref="CraftedPacket" />. The first byte emitted
///     is the opcode; the rest is the body. Tokens are whitespace-separated; Dark Ages strings use
///     code page 949 (EUC-KR):
///     <list type="bullet">
///         <item><c>str(text)</c> — CP949 bytes, no length prefix.</item>
///         <item><c>str8(text)</c> — 1-byte length prefix + CP949 bytes.</item>
///         <item><c>str16(text)</c> — 2-byte big-endian length prefix + CP949 bytes.</item>
///         <item><c>xy(x,y)</c> — two coordinate bytes.</item>
///         <item><c>npc(name)</c> / <c>user(name)</c> — a live entity's 4-byte big-endian serial.</item>
///         <item>anything else — raw hex, e.g. <c>0F</c> or <c>0F001A</c>.</item>
///     </list>
///     A parenthesized argument may not itself contain <c>)</c>.
/// </summary>
public static class PacketCraftParser
{
    private static Encoding Cp949 => Encoding.GetEncoding(949);

    public static CraftedPacket Parse(
        string text,
        Func<string, uint?>? resolveNpc = null,
        Func<string, uint?>? resolveUser = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var bytes = new List<byte>();
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (char.IsWhiteSpace(c))
            {
                i++;

                continue;
            }

            if (char.IsLetter(c))
            {
                var start = i;

                while (i < text.Length && char.IsLetterOrDigit(text[i]))
                    i++;

                var token = text[start..i];

                if (i < text.Length && text[i] == '(')
                {
                    var close = text.IndexOf(')', i);

                    if (close < 0)
                        throw new FormatException($"Unclosed '(' for token '{token}'.");

                    var arg = text[(i + 1)..close];
                    i = close + 1;
                    EmitFunction(bytes, token, arg, resolveNpc, resolveUser);
                } else
                    EmitHex(bytes, token);
            } else
            {
                var start = i;

                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '(')
                    i++;

                EmitHex(bytes, text[start..i]);
            }
        }

        if (bytes.Count == 0)
            throw new FormatException("Packet is empty — the first byte must be the opcode.");

        return new CraftedPacket(bytes[0], bytes.Skip(1).ToArray());
    }

    private static void EmitFunction(
        List<byte> bytes,
        string token,
        string arg,
        Func<string, uint?>? resolveNpc,
        Func<string, uint?>? resolveUser)
    {
        switch (token.ToLowerInvariant())
        {
            case "str":
                bytes.AddRange(Cp949.GetBytes(arg));

                break;
            case "str8":
            {
                var data = Cp949.GetBytes(arg);

                if (data.Length > byte.MaxValue)
                    throw new FormatException("str8 content exceeds 255 bytes.");

                bytes.Add((byte)data.Length);
                bytes.AddRange(data);

                break;
            }
            case "str16":
            {
                var data = Cp949.GetBytes(arg);

                if (data.Length > ushort.MaxValue)
                    throw new FormatException("str16 content exceeds 65535 bytes.");

                bytes.Add((byte)(data.Length >> 8));
                bytes.Add((byte)(data.Length & 0xFF));
                bytes.AddRange(data);

                break;
            }
            case "xy":
            {
                var parts = arg.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length != 2 || !byte.TryParse(parts[0], out var x) || !byte.TryParse(parts[1], out var y))
                    throw new FormatException($"xy() expects two values in 0-255, got '{arg}'.");

                bytes.Add(x);
                bytes.Add(y);

                break;
            }
            case "npc":
                AppendSerial(bytes, Resolve(resolveNpc, arg, "npc"));

                break;
            case "user":
                AppendSerial(bytes, Resolve(resolveUser, arg, "user"));

                break;
            default:
                throw new FormatException($"Unknown token '{token}'.");
        }
    }

    private static uint Resolve(Func<string, uint?>? resolver, string name, string kind)
    {
        if (resolver is null)
            throw new FormatException($"{kind}() is not supported in this context.");

        return resolver(name) ?? throw new FormatException($"{kind}() could not find an entity named '{name}'.");
    }

    private static void AppendSerial(List<byte> bytes, uint serial)
    {
        bytes.Add((byte)(serial >> 24));
        bytes.Add((byte)(serial >> 16));
        bytes.Add((byte)(serial >> 8));
        bytes.Add((byte)serial);
    }

    private static void EmitHex(List<byte> bytes, string token)
    {
        if ((token.Length & 1) != 0)
            throw new FormatException($"Hex token '{token}' has an odd number of digits.");

        for (var i = 0; i < token.Length; i += 2)
            if (byte.TryParse(token.AsSpan(i, 2), NumberStyles.HexNumber, null, out var b))
                bytes.Add(b);
            else
                throw new FormatException($"Invalid hex byte '{token.Substring(i, 2)}'.");
    }
}
