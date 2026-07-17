using Chaos.Networking.Entities.Client;
using Ouroboros.Client;
using Ouroboros.Model;

namespace Ouroboros.Automation;

/// <summary>
///     Drives the NPC dialog/menu the client currently has open. Reads the tracked <see cref="NpcDialog" /> /
///     <see cref="NpcMenu" /> and sends the matching interaction packets, applying the DialogId action offset
///     the protocol expects: +1 to advance (Next / an option), -1 to go back (Previous), 0 to close. Every
///     method returns false when there's nothing of the right kind open, so callers can branch without throwing.
/// </summary>
public sealed class NpcSession
{
    private readonly DarkAgesClient Client;

    public NpcSession(DarkAgesClient client) => Client = client;

    public NpcDialog? Dialog => Client.Dialog;
    public NpcMenu? Menu => Client.Menu;

    /// <summary>Advances the dialog (the Next button).</summary>
    public bool Next() => SendDialog(offset: 1, option: null);

    /// <summary>Steps the dialog back a page (the Previous button).</summary>
    public bool Previous() => SendDialog(offset: -1, option: null);

    /// <summary>Selects a 1-based option in the current dialog.</summary>
    public bool SelectOption(byte option) => SendDialog(offset: 1, option: option);

    /// <summary>Selects the first dialog option whose text contains <paramref name="text" /> (case-insensitive).</summary>
    public bool SelectOption(string text)
    {
        if (Client.Dialog is not { } dialog)
            return false;

        for (var index = 0; index < dialog.Options.Count; index++)
            if (dialog.Options[index].Contains(text, StringComparison.OrdinalIgnoreCase))
                return SelectOption((byte)(index + 1));

        return false;
    }

    /// <summary>Closes the current dialog.</summary>
    public bool Close()
    {
        if (Client.Dialog is not { } dialog)
            return false;

        Client.ServerActions.SendDialogInteraction(new DialogInteractionArgs
        {
            DialogId = 0,
            EntityId = dialog.SourceId,
            EntityType = dialog.EntityType,
            PursuitId = dialog.PursuitId
        });

        Client.Dialog = null;

        return true;
    }

    /// <summary>Chooses a menu pursuit by its id.</summary>
    public bool SelectPursuit(ushort pursuitId)
    {
        if (Client.Menu is not { } menu)
            return false;

        Client.ServerActions.SendMenuInteraction(new MenuInteractionArgs
        {
            EntityId = menu.SourceId,
            EntityType = menu.EntityType,
            PursuitId = pursuitId
        });

        return true;
    }

    /// <summary>Chooses the first menu pursuit whose text contains <paramref name="text" /> (case-insensitive).</summary>
    public bool SelectPursuit(string text)
    {
        if (Client.Menu is not { } menu)
            return false;

        foreach (var option in menu.Options)
            if (option.Text.Contains(text, StringComparison.OrdinalIgnoreCase))
                return SelectPursuit(option.PursuitId);

        return false;
    }

    private bool SendDialog(int offset, byte? option)
    {
        if (Client.Dialog is not { } dialog)
            return false;

        Client.ServerActions.SendDialogInteraction(new DialogInteractionArgs
        {
            DialogId = (ushort)(dialog.DialogId + offset),
            EntityId = dialog.SourceId,
            EntityType = dialog.EntityType,
            PursuitId = dialog.PursuitId,
            Option = option
        });

        return true;
    }
}
