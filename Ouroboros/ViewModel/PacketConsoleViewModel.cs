using System.Collections.ObjectModel;
using Ouroboros.Client;
using Ouroboros.Networking;
using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>
///     View-model for the packet-console tab. Bound to the selected client's <see cref="PacketConsole" />, it
///     appends newly-captured entries to <see cref="Entries" /> on the UI-thread refresh (tracked by a monotonic
///     sequence, so no duplicates and no full rebuilds), and drives the craft box + capture/clear controls.
/// </summary>
public sealed class PacketConsoleViewModel : NotifyPropertyChangedBase
{
    private const int MaxDisplay = 500;
    private PacketConsole? Console;
    private long LastSequence;
    private string _craftText = string.Empty;
    private string _filter = string.Empty;
    private string _status = "No client selected.";

    public ObservableCollection<PacketLogEntry> Entries { get; } = [];

    public bool HasClient => Console is not null;

    public bool IsCapturing
    {
        get => Console?.IsCapturing ?? false;
        set
        {
            if (Console is null)
                return;

            Console.IsCapturing = value;
            OnPropertyChanged();
        }
    }

    public string CraftText { get => _craftText; set => SetField(ref _craftText, value); }
    public string Status { get => _status; private set => SetField(ref _status, value); }

    /// <summary>Substring matched against opcode name or hex (case-insensitive). Empty shows everything.</summary>
    public string Filter
    {
        get => _filter;
        set
        {
            if (!SetField(ref _filter, value))
                return;

            //re-scan the log from the start so the filter applies to already-captured entries too
            Entries.Clear();
            LastSequence = 0;
        }
    }

    /// <summary>Re-points the tab at the selected client's console; resets the view when it changes.</summary>
    public void Bind(DarkAgesClient? client)
    {
        var console = client?.Console;

        if (ReferenceEquals(console, Console))
            return;

        Console = console;
        LastSequence = 0;
        Entries.Clear();
        Status = console is null ? "No client selected." : string.Empty;

        OnPropertyChanged(nameof(HasClient));
        OnPropertyChanged(nameof(IsCapturing));
    }

    /// <summary>Appends entries captured since the last poll. Call on the UI thread.</summary>
    public void Refresh()
    {
        if (Console is null)
            return;

        foreach (var entry in Console.Snapshot())
            if (entry.Sequence > LastSequence)
            {
                LastSequence = entry.Sequence;

                if (Matches(entry))
                    Entries.Add(entry);
            }

        while (Entries.Count > MaxDisplay)
            Entries.RemoveAt(0);
    }

    private bool Matches(PacketLogEntry entry)
        => (_filter.Length == 0)
           || entry.OpCodeName.Contains(_filter, StringComparison.OrdinalIgnoreCase)
           || entry.Hex.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    public void SendToServer() => Send(toServer: true);

    public void SendToClient() => Send(toServer: false);

    public void ClearLog()
    {
        Console?.Clear();
        Entries.Clear();
        LastSequence = 0;
    }

    private void Send(bool toServer)
    {
        var console = Console;

        if (console is null || string.IsNullOrWhiteSpace(CraftText))
            return;

        try
        {
            if (toServer)
                console.SendToServer(CraftText);
            else
                console.SendToClient(CraftText);

            Status = toServer ? "sent → server" : "sent → client";
        } catch (Exception ex)
        {
            Status = $"error: {ex.Message}";
        }
    }
}
