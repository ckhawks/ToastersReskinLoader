using System;

namespace ToasterReskinLoader.qol.beacon;

/// Marshals work onto Unity's main thread via the game's ThreadManager singleton.
/// TCP callbacks from TCPClient fire on the SimpleTcpClient worker thread, but
/// UIToolkit element mutations must happen on the main thread.
public static class BeaconMainThread
{
    public static void Run(Action action)
    {
        if (action == null) return;
        try
        {
            MonoBehaviourSingleton<ThreadManager>.Instance.Enqueue(action);
        }
        catch (Exception e)
        {
            Plugin.LogError($"BeaconMainThread.Run dispatch failed: {e.Message}");
        }
    }
}
