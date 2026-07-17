namespace Ouroboros.Automation.Commands;

/// <summary>
///     Case-insensitive slash-command dispatcher. <see cref="TryHandle" /> returns true when a message
///     was a recognized command (and was executed) so the caller can suppress it instead of forwarding it
///     to the server as chat; unrecognized messages (including unknown verbs) pass through untouched.
/// </summary>
public sealed class SlashCommandInterpreter
{
    private readonly Dictionary<string, SlashCommand> Commands = new(StringComparer.OrdinalIgnoreCase);

    public string Prefix { get; set; } = "/";

    public IReadOnlyCollection<SlashCommand> Registered => Commands.Values;

    public void Register(string verb, string help, SlashCommandHandler handler)
        => Commands[verb] = new SlashCommand(verb, help, handler);

    public bool TryHandle(string message, BotContext context)
    {
        if (SlashCommandParser.Parse(message, Prefix) is not (var verb, var raw))
            return false;

        if (!Commands.TryGetValue(verb, out var command))
            return false;

        var tokens = raw.Length == 0
            ? []
            : raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        try
        {
            command.Handler(context, new SlashCommandArgs(raw, tokens));
        }
        catch (Exception ex)
        {
            context.Reply($"command error: {ex.Message}");
        }

        return true;
    }
}
