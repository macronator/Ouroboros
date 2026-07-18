using Ouroboros.Defintions;

namespace Ouroboros.Automation;

/// <summary>
///     Runs a scripted NPC interaction (banking, vendoring, quests) step by step through <see cref="NpcSession" />.
///     Each step retries until it applies — because a step only succeeds once the matching dialog/menu is open,
///     this naturally waits out the server's request/response round-trips without hard-coded delays. A per-step
///     timeout aborts a script that's waiting on state that never arrives (e.g. a wrong pursuit id).
/// </summary>
public sealed class NpcScriptRunner : BotRoutine
{
    private readonly List<NpcStep> Steps = [];
    private int Index;
    private int StartedIndex = -1;
    private DateTime StepStartedUtc;
    private DateTime LastAttemptUtc = DateTime.MinValue;

    public override string Name => "NPC script";

    /// <summary>True while a script is running.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Minimum gap between step attempts, so retries don't flood the server.</summary>
    public TimeSpan StepDelay { get; set; } = CONSTANTS.HALF_SECOND;

    /// <summary>Abort a step (and the script) if it can't be applied within this long.</summary>
    public TimeSpan StepTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public override TimeSpan Interval => CONSTANTS.QUARTER_SECOND;

    /// <summary>Progress as "current/total" for display.</summary>
    public string Progress => $"{Math.Min(Index + 1, Steps.Count)}/{Steps.Count}";

    /// <summary>Loads and starts a fresh script.</summary>
    public void Run(IReadOnlyList<NpcStep> steps)
    {
        Steps.Clear();
        Steps.AddRange(steps);
        Index = 0;
        StartedIndex = -1;
        LastAttemptUtc = DateTime.MinValue;
        Enabled = Steps.Count > 0;
    }

    public void Stop() => Enabled = false;

    public override ValueTask InvokeAsync(BotContext context, CancellationToken cancellationToken)
    {
        if (!Enabled)
            return default;

        if (Index >= Steps.Count)
        {
            Enabled = false;

            return default;
        }

        var now = DateTime.UtcNow;

        //start the per-step timeout clock the first time we look at this step
        if (StartedIndex != Index)
        {
            StartedIndex = Index;
            StepStartedUtc = now;
        }

        if ((now - LastAttemptUtc) < StepDelay)
            return default;

        LastAttemptUtc = now;

        if (Apply(context, Steps[Index]))
        {
            //applied — move to the next step
            Index++;
        } else if ((now - StepStartedUtc) > StepTimeout)
        {
            //the expected dialog/menu never showed — give up rather than spin forever
            System.Diagnostics.Debug.WriteLine($"[Ouroboros] NPC script aborted: step {Index + 1} ({Steps[Index].Kind}) timed out");
            Enabled = false;
        }

        return default;
    }

    private static bool Apply(BotContext context, NpcStep step)
        => step.Kind switch
        {
            NpcStepKind.Pursuit => ushort.TryParse(step.Arg, out var pursuitId)
                ? context.Npc.SelectPursuit(pursuitId)
                : context.Npc.SelectPursuit(step.Arg),
            NpcStepKind.Option => byte.TryParse(step.Arg, out var option)
                ? context.Npc.SelectOption(option)
                : context.Npc.SelectOption(step.Arg),
            NpcStepKind.Next => context.Npc.Next(),
            NpcStepKind.Close => context.Npc.Close(),
            _ => false
        };
}
