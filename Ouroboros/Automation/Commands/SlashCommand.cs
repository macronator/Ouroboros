namespace Ouroboros.Automation.Commands;

/// <summary>Handles a dispatched slash command against a client's <see cref="BotContext" />.</summary>
public delegate void SlashCommandHandler(BotContext context, SlashCommandArgs args);

/// <summary>A registered command: its verb (without prefix), one-line help, and handler.</summary>
public sealed record SlashCommand(string Verb, string Help, SlashCommandHandler Handler);

/// <summary>The arguments passed to a command: the raw remainder and its whitespace-split tokens.</summary>
public sealed record SlashCommandArgs(string Raw, IReadOnlyList<string> Tokens);
