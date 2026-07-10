using ToasterReskinLoader.core;

namespace ToasterReskinLoader.social.probe;

/// Probe ping panel feature toggle. Enables a right-side panel on the Play
/// menu that lists matchmaking probes with RTT measurements and a slider
/// mirroring the user's Max Matchmaking Ping setting.
///
/// Successor to BeaconPing (Edgegap Beacons -> Probes migration).
public static class ProbePing
{
    public static bool IsEnabled { get; private set; }

    public static void Enable()
    {
        if (IsEnabled) return;
        ProbeCache.Initialize();
        ProbePanelController.Initialize();
        IsEnabled = true;
        Plugin.Log("ProbePing enabled.");
    }

    public static void Disable()
    {
        if (!IsEnabled) return;
        ProbePanelController.Shutdown();
        ProbeCache.Shutdown();
        IsEnabled = false;
        Plugin.Log("ProbePing disabled.");
    }
}
