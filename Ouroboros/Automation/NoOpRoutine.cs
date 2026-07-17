namespace Ouroboros.Automation;

/// <summary>
///     A do-nothing routine used to validate the engine harness (start/stop, pacing) without touching
///     the game. Not registered by default; kept as the minimal template for real routines.
/// </summary>
public sealed class NoOpRoutine : BotRoutine
{
    public override string Name => "No-op";

    public override ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken) => default;
}
