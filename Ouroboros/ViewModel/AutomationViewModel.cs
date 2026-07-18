using Ouroboros.Client;
using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>
///     Automation toggles for the currently-selected client — combat auto-attack, heal/buff support, and
///     consumable auto-use. Two-way bound: flipping a switch enables the routine (and starts the engine).
///     <see cref="Bind" /> re-points it at the selected client and refreshes the switches.
/// </summary>
public sealed class AutomationViewModel : NotifyPropertyChangedBase
{
    private DarkAgesClient? _client;

    public bool HasClient => _client is not null;

    public bool Combat
    {
        get => _client?.Bot.Combat.Enabled ?? false;
        set => Toggle(client => client.Bot.Combat.Enabled = value, value);
    }

    public bool Support
    {
        get => _client?.Bot.Support.Enabled ?? false;
        set => Toggle(client => client.Bot.Support.Enabled = value, value);
    }

    public bool Consumables
    {
        get => _client?.Bot.Consumables.Enabled ?? false;
        set => Toggle(client => client.Bot.Consumables.Enabled = value, value);
    }

    public bool Loot
    {
        get => _client?.Bot.Loot.Enabled ?? false;
        set => Toggle(client => client.Bot.Loot.Enabled = value, value);
    }

    public bool Trash
    {
        get => _client?.Bot.Trash.Enabled ?? false;
        set => Toggle(client => client.Bot.Trash.Enabled = value, value);
    }

    /// <summary>Support heals when HP is at or below this percent.</summary>
    public int HealThreshold
    {
        get => _client?.Bot.Support.HealHealthThreshold ?? 0;
        set => SetThreshold(client => client.Bot.Support.HealHealthThreshold = value);
    }

    /// <summary>Consumables use a healing item when HP is at or below this percent.</summary>
    public int PotionHpThreshold
    {
        get => _client?.Bot.Consumables.HealHealthThreshold ?? 0;
        set => SetThreshold(client => client.Bot.Consumables.HealHealthThreshold = value);
    }

    /// <summary>Consumables use a mana item when MP is at or below this percent.</summary>
    public int PotionMpThreshold
    {
        get => _client?.Bot.Consumables.RestoreManaThreshold ?? 0;
        set => SetThreshold(client => client.Bot.Consumables.RestoreManaThreshold = value);
    }

    /// <summary>Re-points the toggles at <paramref name="client" /> (or nothing) and refreshes their state.</summary>
    public void Bind(DarkAgesClient? client)
    {
        _client = client;
        OnPropertyChanged(nameof(HasClient));
        OnPropertyChanged(nameof(Combat));
        OnPropertyChanged(nameof(Support));
        OnPropertyChanged(nameof(Consumables));
        OnPropertyChanged(nameof(Loot));
        OnPropertyChanged(nameof(Trash));
        OnPropertyChanged(nameof(HealThreshold));
        OnPropertyChanged(nameof(PotionHpThreshold));
        OnPropertyChanged(nameof(PotionMpThreshold));
    }

    private void SetThreshold(Action<DarkAgesClient> apply)
    {
        if (_client is null)
            return;

        apply(_client);
    }

    private void Toggle(Action<DarkAgesClient> apply, bool enabling)
    {
        if (_client is null)
            return;

        apply(_client);

        //enabling any routine needs the engine running; leave it running when disabling one
        if (enabling)
            _client.Bot.Engine.Start();

        OnPropertyChanged(nameof(Combat));
        OnPropertyChanged(nameof(Support));
        OnPropertyChanged(nameof(Consumables));
        OnPropertyChanged(nameof(Loot));
        OnPropertyChanged(nameof(Trash));
    }
}
