using Ouroboros.Client;
using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>
///     One connected client's live status, bound by the dashboard and updated in place from its
///     <see cref="DarkAgesClient" />. Replaces the old code-behind string dump with bindable properties.
/// </summary>
public sealed class ClientStatusViewModel : NotifyPropertyChangedBase
{
    private string _name = "(character unknown)";
    private string _vitals = "-";
    private string _location = "-";
    private int _nearbyMonsters;
    private int _skillCount;
    private int _spellCount;
    private int _inventoryCount;
    private bool _walking;
    private bool _combat;
    private bool _support;
    private bool _consumables;
    private bool _loot;

    public ClientStatusViewModel(DarkAgesClient client) => Client = client;

    //parameterless: used only to build the design-time sample
    private ClientStatusViewModel() { }

    /// <summary>The underlying client, or null for the design-time sample.</summary>
    public DarkAgesClient? Client { get; }

    public string Name { get => _name; private set => SetField(ref _name, value); }
    public string Vitals { get => _vitals; private set => SetField(ref _vitals, value); }
    public string Location { get => _location; private set => SetField(ref _location, value); }
    public int NearbyMonsters { get => _nearbyMonsters; private set => SetField(ref _nearbyMonsters, value); }
    public int SkillCount { get => _skillCount; private set => SetField(ref _skillCount, value); }
    public int SpellCount { get => _spellCount; private set => SetField(ref _spellCount, value); }
    public int InventoryCount { get => _inventoryCount; private set => SetField(ref _inventoryCount, value); }
    public bool Walking { get => _walking; private set => SetField(ref _walking, value); }
    public bool Combat { get => _combat; private set => SetField(ref _combat, value); }
    public bool Support { get => _support; private set => SetField(ref _support, value); }
    public bool Consumables { get => _consumables; private set => SetField(ref _consumables, value); }
    public bool Loot { get => _loot; private set => SetField(ref _loot, value); }

    /// <summary>Pulls the latest values off the client. Call on the UI thread.</summary>
    public void Update()
    {
        if (Client is null)
            return;

        var vitals = Client.Vitals;
        var map = Client.Aisling?.Map;
        var position = Client.ServerPoint;

        Name = Client.Aisling?.Name ?? "(character unknown)";
        Vitals = $"HP {vitals.CurrentHp}/{vitals.MaximumHp} ({vitals.HealthPercent}%)   "
                 + $"MP {vitals.CurrentMp}/{vitals.MaximumMp} ({vitals.ManaPercent}%)";
        Location = $"{map?.Name ?? "-"} [{map?.Id.ToString() ?? "-"}] @ ({position.X}, {position.Y})";
        NearbyMonsters = Client.EntityManager.GetNearbyMonsters(null).Count;
        SkillCount = Client.SkillBook.Snapshot().Count;
        SpellCount = Client.SpellBook.Snapshot().Count;
        InventoryCount = Client.Inventory.Count;
        Walking = Client.Bot.Walker.Route is not null;
        Combat = Client.Bot.Combat.Enabled;
        Support = Client.Bot.Support.Enabled;
        Consumables = Client.Bot.Consumables.Enabled;
        Loot = Client.Bot.Loot.Enabled;
    }

    /// <summary>A populated instance for the XAML designer (no live client).</summary>
    public static ClientStatusViewModel DesignSample()
        => new()
        {
            Name = "Sample Aisling",
            Vitals = "HP 950/1000 (95%)   MP 400/500 (80%)",
            Location = "Mileth [500] @ (12, 9)",
            NearbyMonsters = 3,
            SkillCount = 6,
            SpellCount = 8,
            InventoryCount = 14,
            Walking = true,
            Combat = false,
            Support = true,
            Consumables = false,
            Loot = true
        };
}
