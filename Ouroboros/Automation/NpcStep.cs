namespace Ouroboros.Automation;

/// <summary>The kind of action an <see cref="NpcStep" /> performs against the open NPC dialog/menu.</summary>
public enum NpcStepKind
{
    /// <summary>Select a menu pursuit by id or matching text (<see cref="NpcStep.Arg" />).</summary>
    Pursuit,

    /// <summary>Select a dialog option by 1-based index or matching text.</summary>
    Option,

    /// <summary>Press the dialog's Next button.</summary>
    Next,

    /// <summary>Close the dialog.</summary>
    Close
}

/// <summary>One step of an NPC interaction script: an action plus its (optional) argument.</summary>
public sealed record NpcStep(NpcStepKind Kind, string Arg = "");
