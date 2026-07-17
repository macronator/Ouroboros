using Ouroboros.Defintions;

namespace Ouroboros.Automation;

/// <summary>
///     A single automation loop. The <see cref="AutomationEngine" /> runs each routine on its own
///     cooperatively-cancelled task, invoking <see cref="InvokeAsync" /> repeatedly and pacing the loop
///     by <see cref="Interval" />. A tick that throws is caught so one bad iteration never tears the loop
///     down — the same resilience the reference bot got from per-task retry, without <c>Thread.Abort</c>.
/// </summary>
public abstract class BotRoutine
{
    /// <summary>Human-readable name, used for logging and the UI.</summary>
    public abstract string Name { get; }

    /// <summary>Delay between ticks. Defaults to the shared bot pacing.</summary>
    public virtual TimeSpan Interval => TimeSpan.FromMilliseconds(CONSTANTS.BOT_DELAY_MS);

    /// <summary>
    ///     One iteration of the loop. Keep it short and non-blocking, and honor
    ///     <paramref name="cancellationToken" /> so the engine can stop promptly.
    /// </summary>
    public abstract ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken);
}
