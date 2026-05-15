using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ToasterReskinLoader.qol.beacon;

/// Mirrors BackendManagerController.PingBeacon — TCP connect to beacon.host:beacon.tcp_port,
/// send "ping", measure RTT to first response. Background sweeps return per-beacon results
/// via OnResult on whatever thread the TCP callback fires from; the UI marshals to main thread.
public static class BeaconPinger
{
    public const int ConnectTimeoutMs = 1000;
    public const int ResponseTimeoutMs = 1000;
    public const int RefreshCooldownMs = 5000;
    public const int PerBeaconStaggerMs = 50;

    public class PingResult
    {
        public string BeaconId;
        public int? RttMs;     // null = failure / timeout
    }

    public static event Action<PingResult> OnResult;
    public static event Action OnSweepComplete;

    private static int _sweepInFlight = 0;
    private static DateTime _lastSweepUtc = DateTime.MinValue;

    public static bool IsSweeping => _sweepInFlight != 0;

    public static TimeSpan TimeUntilNextAllowedSweep()
    {
        var elapsed = DateTime.UtcNow - _lastSweepUtc;
        var cd = TimeSpan.FromMilliseconds(RefreshCooldownMs);
        return elapsed >= cd ? TimeSpan.Zero : cd - elapsed;
    }

    /// Kick off a one-shot sweep across the provided beacons. No-op if a sweep
    /// is already running or we're still in cooldown.
    public static bool TryStartSweep(IReadOnlyList<Beacon> beacons)
    {
        if (Interlocked.CompareExchange(ref _sweepInFlight, 1, 0) != 0) return false;
        if (TimeUntilNextAllowedSweep() > TimeSpan.Zero)
        {
            Interlocked.Exchange(ref _sweepInFlight, 0);
            return false;
        }
        _lastSweepUtc = DateTime.UtcNow;

        Task.Run(() => SweepCore(beacons));
        return true;
    }

    private static void SweepCore(IReadOnlyList<Beacon> beacons)
    {
        try
        {
            for (int i = 0; i < beacons.Count; i++)
            {
                var beacon = beacons[i];
                if (beacon == null) continue;

                if (i > 0 && PerBeaconStaggerMs > 0)
                    Thread.Sleep(PerBeaconStaggerMs);

                int? rtt = PingOne(beacon);
                try { OnResult?.Invoke(new PingResult { BeaconId = beacon.id, RttMs = rtt }); }
                catch (Exception e) { Plugin.LogError($"OnResult handler threw: {e.Message}"); }
            }
        }
        finally
        {
            try { OnSweepComplete?.Invoke(); }
            catch (Exception e) { Plugin.LogError($"OnSweepComplete handler threw: {e.Message}"); }
            Interlocked.Exchange(ref _sweepInFlight, 0);
        }
    }

    private static int? PingOne(Beacon beacon)
    {
        try
        {
            var endPoint = new EndPoint(beacon.host, beacon.tcp_port);
            var tcpClient = new TCPClient(endPoint, ConnectTimeoutMs, 1000);
            double pingTimestamp = 0.0;
            int? rtt = null;
            var responseEvent = new ManualResetEventSlim(false);

            tcpClient.OnConnected += delegate { tcpClient.SendMessage("ping"); };
            tcpClient.OnMessageSent += delegate { pingTimestamp = Utils.GetTimestamp(); };
            tcpClient.OnMessageReceived += delegate
            {
                rtt = (int)(Utils.GetTimestamp() - pingTimestamp);
                responseEvent.Set();
            };

            tcpClient.Connect();
            if (tcpClient.IsConnected)
            {
                responseEvent.Wait(ResponseTimeoutMs);
                tcpClient.Disconnect();
            }
            return rtt;
        }
        catch (Exception e)
        {
            Plugin.LogDebug($"Ping to {beacon?.host}:{beacon?.tcp_port} failed: {e.Message}");
            return null;
        }
    }
}
