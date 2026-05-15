using System.Collections.Generic;
using System.Linq;
using ToasterReskinLoader.ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ToasterReskinLoader.qol.beacon;

/// Right-side panel injected into the UIPlay view. One row per cached beacon
/// with location label + RTT in ms, sorted best-first. A divider line marks the
/// user's MaxMatchmakingPing threshold; a slider mirrors and writes that setting.
public static class BeaconPingPanel
{
    private const int RttGreenMax = 50;
    private const int RttYellowMax = 120;

    private const int SliderMin = 30;
    private const int SliderMax = 300;

    private static VisualElement _panel;
    private static VisualElement _rowsContainer;
    private static VisualElement _thresholdDivider;
    private static Label _thresholdLabel;
    private static Button _refreshButton;
    private static Label _statusLabel;
    private static SliderInt _maxPingSlider;
    private static Label _sliderValueLabel;

    private class RowState
    {
        public VisualElement Row;
        public Label LocationLabel;
        public Label RttLabel;
        public int? RttMs;
    }
    private static readonly Dictionary<string, RowState> _rows = new();

    private static System.Action<Dictionary<string, object>> _onMaxPingChanged;

    public static void Show()
    {
        EnsureBuilt();
        if (_panel != null) _panel.style.display = DisplayStyle.Flex;
        Rebuild();
        SyncSliderFromSettings();
    }

    public static void Hide()
    {
        if (_panel != null) _panel.style.display = DisplayStyle.None;
    }

    public static void Destroy()
    {
        BeaconPinger.OnResult -= HandleResult;
        BeaconPinger.OnSweepComplete -= HandleSweepComplete;
        BeaconCache.OnBeaconsUpdated -= HandleBeaconsUpdated;
        if (_onMaxPingChanged != null)
        {
            EventManager.RemoveEventListener("Event_OnMaxMatchmakingPingChanged", _onMaxPingChanged);
            _onMaxPingChanged = null;
        }
        if (_panel != null && _panel.parent != null)
            _panel.parent.Remove(_panel);
        _panel = null;
        _rowsContainer = null;
        _thresholdDivider = null;
        _thresholdLabel = null;
        _refreshButton = null;
        _statusLabel = null;
        _maxPingSlider = null;
        _sliderValueLabel = null;
        _rows.Clear();
    }

    public static void RequestSweep()
    {
        var beacons = BeaconCache.GetBeacons();
        if (beacons.Length == 0)
        {
            SetStatus("Beacon cache doesn't exist — queue and unqueue to fetch the beacon list.");
            return;
        }

        if (!BeaconPinger.TryStartSweep(beacons))
        {
            var remaining = BeaconPinger.TimeUntilNextAllowedSweep();
            SetStatus(remaining > System.TimeSpan.Zero
                ? $"Cooldown: {remaining.TotalSeconds:0.0}s"
                : "Sweep already running");
            return;
        }

        SetStatus("Pinging...");
        ClearRtts();
    }

    private static void EnsureBuilt()
    {
        if (_panel != null) return;

        var uiPlay = UIManager.Instance != null ? UIManager.Instance.Play : null;
        if (uiPlay == null)
        {
            Plugin.LogError("UIPlay not available yet; cannot mount panel");
            return;
        }

        var playView = (VisualElement)typeof(UIView)
            .GetProperty("View", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(uiPlay);
        if (playView == null)
        {
            Plugin.LogError("UIPlay.View is null; cannot mount panel");
            return;
        }

        _panel = new VisualElement
        {
            name = "ToasterBeaconPingPanel",
            style =
            {
                position = Position.Absolute,
                right = 24,
                top = Length.Percent(50),
                translate = new Translate(0, Length.Percent(-50)),
                width = 300,
                paddingTop = 12,
                paddingBottom = 12,
                paddingLeft = 12,
                paddingRight = 12,
                backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.70f)),
                borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6,
                borderBottomRightRadius = 6,
            }
        };

        var title = new Label("BEACON PING")
        {
            style =
            {
                color = Color.white,
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 14,
                marginBottom = 8,
            }
        };
        _panel.Add(title);

        _statusLabel = new Label("")
        {
            style =
            {
                color = new StyleColor(new Color(0.8f, 0.8f, 0.8f)),
                fontSize = 11,
                marginBottom = 6,
                whiteSpace = WhiteSpace.Normal,
            }
        };
        _panel.Add(_statusLabel);

        _rowsContainer = new VisualElement();
        _panel.Add(_rowsContainer);

        var note = new Label("Only the party leader's Max Matchmaking RTT setting is used for matchmaking compatibility.")
        {
            style =
            {
                color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)),
                fontSize = 10,
                whiteSpace = WhiteSpace.Normal,
                marginTop = 10,
                marginBottom = 6,
            }
        };
        _panel.Add(note);

        BuildSliderRow();

        _refreshButton = new Button(() => RequestSweep())
        {
            text = "REFRESH",
            style =
            {
                marginTop = 8,
                backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f)),
                color = Color.white,
                unityTextAlign = TextAnchor.MiddleCenter,
                paddingTop = 6, paddingBottom = 6,
            }
        };
        UITools.AddHoverEffectsForButton(_refreshButton);
        _panel.Add(_refreshButton);

        _refreshButton.schedule.Execute(UpdateRefreshButton).Every(200);

        playView.Add(_panel);

        BeaconPinger.OnResult += HandleResult;
        BeaconPinger.OnSweepComplete += HandleSweepComplete;
        BeaconCache.OnBeaconsUpdated += HandleBeaconsUpdated;

        _onMaxPingChanged = OnMaxMatchmakingPingChanged;
        EventManager.AddEventListener("Event_OnMaxMatchmakingPingChanged", _onMaxPingChanged);
    }

    private static void BuildSliderRow()
    {
        var sliderRow = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 }
        };
        var sliderLabel = new Label("Max RTT")
        {
            style = { color = Color.white, fontSize = 11, width = 60 }
        };
        _maxPingSlider = new SliderInt(SliderMin, SliderMax)
        {
            value = SettingsManager.MaxMatchmakingPing,
            style =
            {
                flexGrow = 1,
                marginLeft = 4,
                marginRight = 6,
                minHeight = 20,
                justifyContent = Justify.Center,
            }
        };
        StyleSliderInternals(_maxPingSlider);
        _maxPingSlider.RegisterValueChangedCallback(evt =>
        {
            EventManager.TriggerEvent("Event_OnSettingsMaxMatchmakingPingChanged",
                new Dictionary<string, object> { { "value", evt.newValue } });
            UpdateSliderValueLabel(evt.newValue);
            UpdateThresholdDivider();
        });
        _sliderValueLabel = new Label(SettingsManager.MaxMatchmakingPing + " ms")
        {
            style = { color = Color.white, fontSize = 11, width = 56, unityTextAlign = TextAnchor.MiddleRight }
        };
        sliderRow.Add(sliderLabel);
        sliderRow.Add(_maxPingSlider);
        sliderRow.Add(_sliderValueLabel);
        _panel.Add(sliderRow);
    }

    private static void StyleSliderInternals(SliderInt slider)
    {
        slider.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            var tracker = slider.Q(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new StyleColor(new Color(0.35f, 0.35f, 0.35f));
                tracker.style.height = 4;
                tracker.style.borderTopLeftRadius = 2;
                tracker.style.borderTopRightRadius = 2;
                tracker.style.borderBottomLeftRadius = 2;
                tracker.style.borderBottomRightRadius = 2;
            }
            var dragger = slider.Q(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.backgroundColor = Color.white;
                dragger.style.width = 12;
                dragger.style.height = 12;
                dragger.style.marginTop = -4;
                dragger.style.borderTopLeftRadius = 6;
                dragger.style.borderTopRightRadius = 6;
                dragger.style.borderBottomLeftRadius = 6;
                dragger.style.borderBottomRightRadius = 6;
            }
        });
    }

    private static void UpdateSliderValueLabel(int v)
    {
        if (_sliderValueLabel != null) _sliderValueLabel.text = v + " ms";
    }

    private static void SyncSliderFromSettings()
    {
        if (_maxPingSlider == null) return;
        var current = SettingsManager.MaxMatchmakingPing;
        if (_maxPingSlider.value != current) _maxPingSlider.SetValueWithoutNotify(current);
        UpdateSliderValueLabel(current);
        UpdateThresholdDivider();
    }

    private static void OnMaxMatchmakingPingChanged(Dictionary<string, object> message)
    {
        BeaconMainThread.Run(SyncSliderFromSettings);
    }

    private static void Rebuild()
    {
        if (_rowsContainer == null) return;
        _rowsContainer.Clear();
        _rows.Clear();
        _thresholdDivider = null;
        _thresholdLabel = null;

        var beacons = BeaconCache.GetBeacons();
        if (beacons.Length == 0)
        {
            SetStatus("Beacon cache doesn't exist — queue and unqueue to fetch the beacon list.");
            return;
        }

        SetStatus("");
        foreach (var beacon in beacons)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    marginTop = 2, marginBottom = 2,
                }
            };
            var label = new Label(FormatLocation(beacon))
            {
                style = { color = Color.white, fontSize = 12 }
            };
            var rtt = new Label("—")
            {
                style = { color = new StyleColor(new Color(0.7f, 0.7f, 0.7f)), fontSize = 12 }
            };
            row.Add(label);
            row.Add(rtt);
            _rowsContainer.Add(row);
            _rows[beacon.id] = new RowState
            {
                Row = row,
                LocationLabel = label,
                RttLabel = rtt,
                RttMs = null,
            };
        }

        UpdateThresholdDivider();
    }

    private static void ClearRtts()
    {
        foreach (var kv in _rows)
        {
            kv.Value.RttMs = null;
            kv.Value.RttLabel.text = "...";
            kv.Value.RttLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
        }
        UpdateThresholdDivider();
    }

    private static void HandleResult(BeaconPinger.PingResult r)
    {
        BeaconMainThread.Run(() =>
        {
            if (!_rows.TryGetValue(r.BeaconId, out var state)) return;
            state.RttMs = r.RttMs;
            if (r.RttMs.HasValue)
            {
                state.RttLabel.text = $"{r.RttMs.Value} ms";
                state.RttLabel.style.color = ColorForRtt(r.RttMs.Value);
            }
            else
            {
                state.RttLabel.text = "—";
                state.RttLabel.style.color = new StyleColor(new Color(0.7f, 0.4f, 0.4f));
            }

            ReSortRows();
            UpdateThresholdDivider();
        });
    }

    private static void HandleSweepComplete()
    {
        BeaconMainThread.Run(() =>
        {
            SetStatus("");
            ReSortRows();
            UpdateThresholdDivider();
        });
    }

    private static void HandleBeaconsUpdated()
    {
        BeaconMainThread.Run(() =>
        {
            Rebuild();
            RequestSweep();
        });
    }

    private static void ReSortRows()
    {
        if (_rowsContainer == null) return;

        var sorted = _rows.Values
            .OrderBy(s => s.RttMs.HasValue ? 0 : 1)
            .ThenBy(s => s.RttMs ?? int.MaxValue)
            .ToList();

        foreach (var state in sorted)
        {
            if (state.Row.parent != null) state.Row.parent.Remove(state.Row);
        }
        if (_thresholdDivider != null && _thresholdDivider.parent != null)
            _thresholdDivider.parent.Remove(_thresholdDivider);
        if (_thresholdLabel != null && _thresholdLabel.parent != null)
            _thresholdLabel.parent.Remove(_thresholdLabel);

        foreach (var state in sorted) _rowsContainer.Add(state.Row);
    }

    private static void UpdateThresholdDivider()
    {
        if (_rowsContainer == null) return;

        int threshold = SettingsManager.MaxMatchmakingPing;

        if (_thresholdDivider != null && _thresholdDivider.parent != null)
            _thresholdDivider.parent.Remove(_thresholdDivider);
        if (_thresholdLabel != null && _thresholdLabel.parent != null)
            _thresholdLabel.parent.Remove(_thresholdLabel);

        var children = _rowsContainer.Children().ToList();
        int insertIndex = -1;
        bool anyAbove = false;
        bool anyBelowOrEqual = false;
        for (int i = 0; i < children.Count; i++)
        {
            var row = children[i];
            var state = _rows.Values.FirstOrDefault(s => s.Row == row);
            if (state == null || !state.RttMs.HasValue) continue;
            if (state.RttMs.Value <= threshold) anyBelowOrEqual = true;
            else
            {
                anyAbove = true;
                if (insertIndex < 0) insertIndex = i;
            }
        }

        if (!anyAbove || !anyBelowOrEqual) return;

        if (_thresholdLabel == null)
        {
            _thresholdLabel = new Label("")
            {
                style =
                {
                    color = new StyleColor(new Color(1f, 0.85f, 0.4f)),
                    fontSize = 10,
                    marginTop = 4,
                    marginBottom = 2,
                    unityTextAlign = TextAnchor.MiddleRight,
                }
            };
        }
        _thresholdLabel.text = $"— max RTT: {threshold} ms —";

        if (_thresholdDivider == null)
        {
            _thresholdDivider = new VisualElement
            {
                style =
                {
                    height = 1,
                    backgroundColor = new StyleColor(new Color(1f, 0.85f, 0.4f, 0.6f)),
                    marginBottom = 4,
                }
            };
        }

        _rowsContainer.Insert(insertIndex, _thresholdDivider);
        _rowsContainer.Insert(insertIndex, _thresholdLabel);
    }

    private static void SetStatus(string s)
    {
        if (_statusLabel != null) _statusLabel.text = s;
    }

    private static void UpdateRefreshButton()
    {
        if (_refreshButton == null) return;
        if (BeaconPinger.IsSweeping)
        {
            _refreshButton.text = "REFRESHING...";
            _refreshButton.SetEnabled(false);
            return;
        }
        var remaining = BeaconPinger.TimeUntilNextAllowedSweep();
        if (remaining > System.TimeSpan.Zero)
        {
            _refreshButton.text = $"COOLDOWN {remaining.TotalSeconds:0.0}s";
            _refreshButton.SetEnabled(false);
            return;
        }
        _refreshButton.text = "REFRESH";
        _refreshButton.SetEnabled(true);
    }

    private static StyleColor ColorForRtt(int rtt)
    {
        if (rtt < RttGreenMax)  return new StyleColor(new Color(0.4f, 1f, 0.5f));
        if (rtt < RttYellowMax) return new StyleColor(new Color(1f, 0.9f, 0.4f));
        return new StyleColor(new Color(1f, 0.5f, 0.4f));
    }

    private static string FormatLocation(Beacon b)
    {
        if (b == null) return "?";
        var loc = b.location;
        if (loc == null) return b.id ?? b.host ?? "?";
        if (!string.IsNullOrEmpty(loc.city)) return $"{loc.city}, {loc.country}";
        if (!string.IsNullOrEmpty(loc.country)) return loc.country;
        return b.id ?? b.host ?? "?";
    }
}
