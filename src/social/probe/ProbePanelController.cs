using System.Collections.Generic;

using ToasterReskinLoader.core;

namespace ToasterReskinLoader.social.probe;

/// Wires the panel show/hide to main-menu events:
///   Event_OnMainMenuClickPlay   -> show + trigger one sweep
///   Event_OnPlayClickClose      -> hide
public static class ProbePanelController
{
    private static System.Action<Dictionary<string, object>> _onPlayClick;
    private static System.Action<Dictionary<string, object>> _onPlayClose;

    public static void Initialize()
    {
        _onPlayClick = OnMainMenuClickPlay;
        _onPlayClose = OnPlayClickClose;
        EventManager.AddEventListener("Event_OnMainMenuClickPlay", _onPlayClick);
        EventManager.AddEventListener("Event_OnPlayClickClose", _onPlayClose);
    }

    public static void Shutdown()
    {
        if (_onPlayClick != null)
            EventManager.RemoveEventListener("Event_OnMainMenuClickPlay", _onPlayClick);
        if (_onPlayClose != null)
            EventManager.RemoveEventListener("Event_OnPlayClickClose", _onPlayClose);
        ProbePingPanel.Destroy();
    }

    private static void OnMainMenuClickPlay(Dictionary<string, object> _)
    {
        Plugin.LogDebug("OnMainMenuClickPlay");
        ProbePingPanel.Show();
        ProbePingPanel.RequestSweep();
    }

    private static void OnPlayClickClose(Dictionary<string, object> _)
    {
        ProbePingPanel.Hide();
    }
}
