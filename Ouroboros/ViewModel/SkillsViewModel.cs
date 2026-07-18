using System.Collections.ObjectModel;
using Ouroboros.Client;
using Ouroboros.ViewModel.Abstractions;

namespace Ouroboros.ViewModel;

/// <summary>A skill or spell row: slot, name, and a live ready/cooldown status string.</summary>
public sealed class AbilityRow : NotifyPropertyChangedBase
{
    private string _status = string.Empty;

    public AbilityRow(byte slot, string name)
    {
        Slot = slot;
        Name = name;
    }

    public byte Slot { get; }
    public string Name { get; }
    public string Status { get => _status; set => SetField(ref _status, value); }
}

/// <summary>
///     View-model for the skills/spells tab. Mirrors the selected client's skill and spell books, rebuilding a
///     list only when its set changes (by slot+name signature) and otherwise updating the status text in place,
///     so cooldowns count down live without churning the collection or losing selection.
/// </summary>
public sealed class SkillsViewModel : NotifyPropertyChangedBase
{
    private DarkAgesClient? Client;
    private string SkillSignature = string.Empty;
    private string SpellSignature = string.Empty;

    public ObservableCollection<AbilityRow> Skills { get; } = [];
    public ObservableCollection<AbilityRow> Spells { get; } = [];

    public void Bind(DarkAgesClient? client)
    {
        if (ReferenceEquals(client, Client))
            return;

        Client = client;
        SkillSignature = string.Empty;
        SpellSignature = string.Empty;
        Skills.Clear();
        Spells.Clear();
    }

    public void Refresh()
    {
        if (Client is null)
            return;

        var now = DateTime.UtcNow;

        var skillData = Client.SkillBook.Snapshot()
                              .OrderBy(skill => skill.Slot)
                              .Select(skill => (skill.Slot, skill.Name,
                                  Status: skill.IsReadyAt(now) ? "ready" : $"cd {(int)skill.RemainingCooldownAt(now).TotalSeconds}s"))
                              .ToArray();

        var spellData = Client.SpellBook.Snapshot()
                              .OrderBy(spell => spell.Slot)
                              .Select(spell => (spell.Slot, spell.Name,
                                  Status: (spell.IsReadyAt(now) ? "ready" : "cd") + (spell.IsBuffActiveAt(now) ? " (active)" : "")))
                              .ToArray();

        Apply(Skills, skillData, ref SkillSignature);
        Apply(Spells, spellData, ref SpellSignature);
    }

    //rebuilds the collection when the set (slot+name) changes; otherwise updates status text in place
    private static void Apply(ObservableCollection<AbilityRow> collection, (byte Slot, string Name, string Status)[] data, ref string signature)
    {
        var current = string.Join('|', data.Select(entry => $"{entry.Slot}:{entry.Name}"));

        if (current != signature)
        {
            signature = current;
            collection.Clear();

            foreach (var entry in data)
                collection.Add(new AbilityRow(entry.Slot, entry.Name) { Status = entry.Status });

            return;
        }

        for (var index = 0; (index < collection.Count) && (index < data.Length); index++)
            collection[index].Status = data[index].Status;
    }
}
