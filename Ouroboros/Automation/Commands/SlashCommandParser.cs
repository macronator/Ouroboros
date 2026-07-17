namespace Ouroboros.Automation.Commands;

/// <summary>Pure parsing for the slash-command mini-language: strip the prefix, split verb from arguments.</summary>
public static class SlashCommandParser
{
    /// <summary>
    ///     Returns the verb and the trimmed remainder if <paramref name="message" /> begins with
    ///     <paramref name="prefix" />, otherwise null (meaning "not a command — leave it alone").
    /// </summary>
    public static (string Verb, string Raw)? Parse(string message, string prefix)
    {
        if (string.IsNullOrWhiteSpace(message) || !message.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var body = message[prefix.Length..].TrimStart();

        if (body.Length == 0)
            return null;

        var idx = body.IndexOf(' ');
        var verb = idx < 0 ? body : body[..idx];
        var raw = idx < 0 ? string.Empty : body[(idx + 1)..].Trim();

        return (verb, raw);
    }
}
