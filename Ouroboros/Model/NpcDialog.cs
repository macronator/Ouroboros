using Chaos.Common.Definitions;

namespace Ouroboros.Model;

/// <summary>
///     The NPC dialog the client currently has open, captured from the DisplayDialog packet. Holds the ids
///     the client must echo back to interact (dialog id, source entity, pursuit) plus the visible text/options.
/// </summary>
public sealed class NpcDialog
{
    public required ushort DialogId { get; init; }
    public required uint SourceId { get; init; }
    public required EntityType EntityType { get; init; }
    public required ushort PursuitId { get; init; }
    public required DialogType DialogType { get; init; }
    public required string Name { get; init; }
    public required string Text { get; init; }
    public required IReadOnlyList<string> Options { get; init; }
    public required bool HasNext { get; init; }
    public required bool HasPrevious { get; init; }
}
