namespace Ouroboros.Automation.Events;

/// <summary>The kind an incoming line of server text was classified as by the <see cref="ServerTextRouter" />.</summary>
public enum ServerEventKind
{
    /// <summary>Uncategorized system / orange-bar text.</summary>
    System,

    /// <summary>Public or nearby chat.</summary>
    Public,

    /// <summary>Anti-cheat / bot-check prompt — triggers a panic stop.</summary>
    BotCheck,

    /// <summary>An item's durability changed. <see cref="ServerTextEvent.Capture" /> is the item name.</summary>
    Durability,

    /// <summary>"Another curse afflicts thee. [tier]". <see cref="ServerTextEvent.Capture" /> is the tier.</summary>
    CurseApplied,

    /// <summary>"You cast X" confirmation. <see cref="ServerTextEvent.Capture" /> is the spell name.</summary>
    SpellCast,

    /// <summary>Someone joined or left the group.</summary>
    GroupChanged,

    /// <summary>A labor / "works for you" line.</summary>
    Labor,
}

/// <summary>
///     A classified line of inbound server text. <see cref="Text" /> is raw server / other-player content and
///     must be treated as UNTRUSTED — subscribers must never dispatch it as a command or otherwise execute it.
/// </summary>
public sealed record ServerTextEvent(ServerEventKind Kind, string Text, string? Capture = null);
