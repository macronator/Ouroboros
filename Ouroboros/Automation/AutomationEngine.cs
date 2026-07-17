using Microsoft.Extensions.Logging;

namespace Ouroboros.Automation;

/// <summary>
///     Runs a set of <see cref="BotRoutine" />s for one client. Each registered routine gets its own
///     cooperatively-cancelled loop task; <see cref="Start" /> snapshots the current routines and
///     <see cref="StopAsync" /> cancels and awaits them. This is the foundation the higher-level
///     automation (support, combat, walking, …) is built on — it is intentionally behavior-free.
/// </summary>
public sealed class AutomationEngine
{
    private readonly BotContext Context;
    private readonly ILogger? Logger;
    private readonly List<BotRoutine> Routines = [];
    private readonly object Gate = new();
    private CancellationTokenSource? Cts;
    private List<Task>? Loops;

    public AutomationEngine(BotContext context, ILogger? logger = null)
    {
        Context = context;
        Logger = logger;
    }

    /// <summary>Whether the engine currently has running loops.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>The routines registered on this engine.</summary>
    public IReadOnlyList<BotRoutine> RegisteredRoutines
    {
        get
        {
            lock (Gate)
                return Routines.ToArray();
        }
    }

    /// <summary>
    ///     Registers a routine. New routines are picked up on the next <see cref="Start" />; registering
    ///     while running does not add a loop to the current run.
    /// </summary>
    public void Register(BotRoutine routine)
    {
        lock (Gate)
            Routines.Add(routine);
    }

    /// <summary>Starts a loop task for every registered routine. No-op if already running.</summary>
    public void Start()
    {
        lock (Gate)
        {
            if (IsRunning)
                return;

            Cts = new CancellationTokenSource();
            var token = Cts.Token;
            Loops = Routines.Select(routine => RunLoop(routine, token)).ToList();
            IsRunning = true;
        }
    }

    /// <summary>Cancels all loops and waits for them to unwind. Safe to call when not running.</summary>
    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        List<Task>? loops;

        lock (Gate)
        {
            if (!IsRunning)
                return;

            cts = Cts;
            loops = Loops;
            Cts = null;
            Loops = null;
            IsRunning = false;
        }

        if (cts is not null)
            await cts.CancelAsync();

        if (loops is not null)
            try
            {
                await Task.WhenAll(loops);
            }
            catch (OperationCanceledException)
            {
                //expected while loops unwind
            }

        cts?.Dispose();
    }

    private async Task RunLoop(BotRoutine routine, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await routine.InvokeAsync(Context, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                //one bad tick must never kill the loop
                Logger?.LogError(ex, "Automation routine '{Routine}' threw", routine.Name);
            }

            try
            {
                await Task.Delay(routine.Interval, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
