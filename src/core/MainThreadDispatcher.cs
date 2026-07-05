using System;

namespace ToasterReskinLoader.core;

/// Marshals work onto Unity's main thread via the game's ThreadManager singleton.
/// Background TCP callbacks (probe pinger, server-preview pings) fire on worker
/// threads, but UIToolkit element mutations must happen on the main thread.
///
/// Formerly social/beacon/BeaconMainThread; relocated here and renamed because it
/// is a generic dispatcher shared by the probe panel and the server-preview cache,
/// not part of any one feature.
public static class MainThreadDispatcher
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
            Plugin.LogError($"MainThreadDispatcher.Run dispatch failed: {e.Message}");
        }
    }
}
