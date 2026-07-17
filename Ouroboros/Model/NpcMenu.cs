using Chaos.Common.Definitions;

namespace Ouroboros.Model;

/// <summary>
///     The NPC menu (pursuit list) the client currently has open, captured from the DisplayMenu packet. This
///     is the entry point of an NPC conversation — each option carries the pursuit id to send back to choose it.
/// </summary>
public sealed class NpcMenu
{
    public required uint SourceId { get; init; }
    public required EntityType EntityType { get; init; }
    public required MenuType MenuType { get; init; }
    public required ushort PursuitId { get; init; }
    public required string Name { get; init; }
    public required string Text { get; init; }
    public required IReadOnlyList<NpcMenuOption> Options { get; init; }
}

/// <summary>A single selectable menu option: its label and the pursuit id to send when chosen.</summary>
public sealed record NpcMenuOption(string Text, ushort PursuitId);
