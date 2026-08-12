// Tracks whether the user's settings have made it to disk, so the Reskin Manager can say so.
//
// Both save paths (the shareable reskin profile and the QoL settings side-cars) catch their
// own write exceptions and only log, so a failed save is invisible from the UI unless it is
// reported here. Every save reports through this type; the menu renders whatever it says.

using System;

namespace ToasterReskinLoader.core;

public enum SaveState
{
    Idle,
    Pending,
    Saved,
    Failed
}

public static class SaveStatus
{
    public static SaveState State { get; private set; } = SaveState.Idle;

    /// Raised whenever State changes. The menu subscribes while it is open.
    public static event Action Changed;

    /// A change has been made but not yet written — the slider debounce window.
    public static void ReportPending() => Set(SaveState.Pending);

    public static void ReportSaved() => Set(SaveState.Saved);

    public static void ReportFailed() => Set(SaveState.Failed);

    /// Drops the transient "Saved!" back to idle. Ignored in any other state so it can't
    /// clear a failure the user hasn't seen, or a pending edit that landed in the meantime.
    public static void ClearSaved()
    {
        if (State == SaveState.Saved)
        {
            Set(SaveState.Idle);
        }
    }

    /// Reports the outcome of a save that returns success as a bool.
    public static void Report(bool ok)
    {
        if (ok)
        {
            ReportSaved();
        }
        else
        {
            ReportFailed();
        }
    }

    private static void Set(SaveState state)
    {
        // Pending fires on every change event of a drag, so collapse the repeats. Saved is
        // allowed to re-fire: a second save while the "Saved!" flash is still up needs to
        // re-arm the menu's fade timer, or the flash ends early.
        if (State == state && state == SaveState.Pending)
        {
            return;
        }
        State = state;
        Changed?.Invoke();
    }
}
