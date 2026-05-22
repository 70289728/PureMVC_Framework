using System;

/// <summary>
/// Dialog type — controls visual style and behavior.
/// </summary>
public enum DialogType
{
    /// <summary>Floating toast, auto-dismiss after delay. No blocking.</summary>
    Tip = 0,

    /// <summary>Modal confirm dialog with confirm/cancel buttons. Blocks input.</summary>
    Confirm = 1,

    /// <summary>Server-pushed announcement. Must be dismissed manually.</summary>
    ServerPush = 2,
}
