using System.Text.RegularExpressions;
using Ouroboros.Defintions;

namespace Ouroboros.Automation.Events;

/// <summary>
///     Passive event bus over inbound server text. The server-message (0x0A) and public-message (0x0D) handlers
///     feed every line here; it classifies each against a small set of matchers in priority order and raises a
///     typed <see cref="ServerTextEvent" /> that routines subscribe to. It only observes — nothing is ever
///     dropped from the packet relay. A bot-check match panic-stops the automation engine.
/// </summary>
public sealed class ServerTextRouter
{
    private static readonly Regex DurabilityRegex = new(CONSTANTS.DURABILITY_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CurseRegex = new(CONSTANTS.ANOTHER_CURSE_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CastRegex = new(CONSTANTS.CAST_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex GroupRegex = new(CONSTANTS.GROUP_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex LaborRegex = new(CONSTANTS.LABOR_PATTERN, RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly BotContext Context;
    private ServerTextEvent? LastEvent;
    private DateTime LastUtc;

    public ServerTextRouter(BotContext context) => Context = context;

    /// <summary>Raised for every classified line of inbound server text. Treat <c>Text</c> as untrusted.</summary>
    public event Action<ServerTextEvent>? Received;

    /// <summary>When true, a bot-check match stops all automation and warns in-client.</summary>
    public bool PanicOnBotCheck { get; set; } = true;

    /// <summary>
    ///     Case-insensitive substrings that mark an anti-cheat / bot-check prompt. Server-specific — tune via
    ///     live capture in the Packet Console. Kept conservative to avoid false-positive panic stops.
    /// </summary>
    public List<string> BotCheckPhrases { get; } = ["bot check", "botcheck", "are you botting"];

    /// <summary>Feeds a system / orange-bar line (ServerMessage 0x0A).</summary>
    public void IngestSystem(string? text) => Ingest(text, ServerEventKind.System);

    /// <summary>Feeds a public / nearby-chat line (DisplayPublicMessage 0x0D).</summary>
    public void IngestPublic(string? text) => Ingest(text, ServerEventKind.Public);

    private void Ingest(string? text, ServerEventKind fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        ServerTextEvent evt;

        try
        {
            evt = Classify(text, fallback);
        }
        catch
        {
            //a bad pattern must never disturb the relay — fall back to the raw line
            evt = new ServerTextEvent(fallback, text);
        }

        //collapse rapid identical repeats (a line is often echoed several times in a row)
        var now = DateTime.UtcNow;

        if (LastEvent is { } last && (last.Kind == evt.Kind) && (last.Text == evt.Text) && ((now - LastUtc) < TimeSpan.FromMilliseconds(400)))
            return;

        LastEvent = evt;
        LastUtc = now;

        if (evt.Kind == ServerEventKind.BotCheck && PanicOnBotCheck)
        {
            _ = Context.Engine.StopAsync();
            Context.Reply("[Ouroboros] bot-check detected — automation stopped.");
        }

        try
        {
            Received?.Invoke(evt);
        }
        catch
        {
            //a throwing subscriber must never disturb the packet relay
        }
    }

    private ServerTextEvent Classify(string text, ServerEventKind fallback)
    {
        foreach (var phrase in BotCheckPhrases)
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                return new ServerTextEvent(ServerEventKind.BotCheck, text);

        if (DurabilityRegex.Match(text) is { Success: true } durability)
            return new ServerTextEvent(ServerEventKind.Durability, text, durability.Groups[1].Value);

        if (CurseRegex.Match(text) is { Success: true } curse)
            return new ServerTextEvent(ServerEventKind.CurseApplied, text, curse.Groups[1].Value);

        if (CastRegex.Match(text) is { Success: true } cast)
            return new ServerTextEvent(ServerEventKind.SpellCast, text, cast.Groups[1].Value);

        if (GroupRegex.IsMatch(text))
            return new ServerTextEvent(ServerEventKind.GroupChanged, text);

        if (LaborRegex.IsMatch(text))
            return new ServerTextEvent(ServerEventKind.Labor, text);

        return new ServerTextEvent(fallback, text);
    }
}
