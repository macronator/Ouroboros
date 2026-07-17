namespace Ouroboros.Networking;

/// <summary>A packet assembled from the craft DSL: an opcode plus its cleartext body.</summary>
public sealed record CraftedPacket(byte OpCode, byte[] Body);
