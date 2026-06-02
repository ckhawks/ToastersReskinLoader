using ToasterReskinLoader.core;

namespace ToasterReskinLoader.qol.beacon;

/// Beacon ping panel feature toggle. Enables a right-side panel on the Play
/// menu that lists cached server beacons with RTT measurements and a slider
/// mirroring the user's Max Matchmaking Ping setting.
public static class BeaconPing
{
    public static bool IsEnabled { get; private set; }

    public static void Enable()
    {
        if (IsEnabled) return;
        BeaconCache.Initialize();
        BeaconPanelController.Initialize();
        IsEnabled = true;
        Plugin.Log("BeaconPing enabled.");
    }

    public static void Disable()
    {
        if (!IsEnabled) return;
        BeaconPanelController.Shutdown();
        BeaconCache.Shutdown();
        IsEnabled = false;
        Plugin.Log("BeaconPing disabled.");
    }
}
