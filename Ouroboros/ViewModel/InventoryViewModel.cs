using System.Collections.ObjectModel;
using Ouroboros.Client;
using Ouroboros.Model;
using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>
///     View-model for the inventory tab. Mirrors the selected client's <see cref="Inventory" /> into a bound
///     list, rebuilding only when the contents actually change (tracked by a lightweight signature) so the
///     grid doesn't churn on every poll.
/// </summary>
public sealed class InventoryViewModel : NotifyPropertyChangedBase
{
    private DarkAgesClient? Client;
    private string Signature = string.Empty;
    private string _header = "No client selected.";

    public ObservableCollection<Item> Items { get; } = [];

    public string Header { get => _header; private set => SetField(ref _header, value); }

    public void Bind(DarkAgesClient? client)
    {
        if (ReferenceEquals(client, Client))
            return;

        Client = client;
        Signature = string.Empty;
        Items.Clear();
        Header = client is null ? "No client selected." : string.Empty;
    }

    /// <summary>Refreshes the item list when the inventory has changed. Call on the UI thread.</summary>
    public void Refresh()
    {
        if (Client is null)
            return;

        var snapshot = Client.Inventory.Snapshot().OrderBy(item => item.Slot).ToArray();
        var signature = string.Join('|', snapshot.Select(item => $"{item.Slot}:{item.Name}:{item.Count}:{item.CurrentDurability}"));

        if (signature == Signature)
            return;

        Signature = signature;

        Items.Clear();

        foreach (var item in snapshot)
            Items.Add(item);

        Header = $"{Client.Inventory.Count}/{Inventory.Capacity} used, {Client.Inventory.FreeSlots} free";
    }
}
