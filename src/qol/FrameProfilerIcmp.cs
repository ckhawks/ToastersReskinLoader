using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace ToasterReskinLoader.qol;

// Background ICMP echo prober for the currently-connected game server.
// Periodically pings the server's host IP at the network layer and exposes
// the latest round-trip time so the Frame Profiler can compare it against
// the in-game (UDP transport) RTT.
//
// Why: the game RTT (NetworkTransport.GetCurrentRtt) is an application-layer
// measurement over UDP — it includes server tick processing, ack timing, and
// any send-buffer queueing. A raw ICMP echo measures the pure network
// round-trip to the host's NIC. The gap between them (game RTT − ICMP) is
// therefore a rough read on how much of your latency is the server/netcode
// vs. the wire.
//
// Caveats: many hosts and firewalls drop or deprioritize ICMP echo, so a
// missing reply is *expected* and surfaced as a status — never treated as
// packet loss. System.Net Ping blocks, so probes run on a worker thread
// (one in flight at a time, mirroring BeaconPinger); results are written to
// volatile fields the main thread reads.
public static class FrameProfilerIcmp
{
    public enum Status { NoTarget, Probing, Ok, Timeout, Unreachable, Error }

    // One probe every ~1.5s — frequent enough to track trends, sparse enough
    // to be invisible traffic-wise and to avoid tripping host ICMP rate limits.
    const int IntervalMs = 1500;
    const int TimeoutMs = 1000;

    // -1 = no usable reading yet (still probing, or the target blocks ICMP).
    // >= 0 = last successful round-trip time in ms.
    public static volatile float currentIcmpMs = -1f;
    public static volatile Status currentStatus = Status.NoTarget;

    static string _targetIp;
    static int _inFlight = 0;
    static DateTime _lastProbeUtc = DateTime.MinValue;

    // Set/clear the IP we probe. Called from the overlay each stat refresh
    // with the resolved server IP (null/empty when offline). Switching target
    // clears the cached reading immediately so stale numbers never linger.
    public static void SetTarget(string ip)
    {
        if (string.IsNullOrEmpty(ip))
        {
            if (_targetIp != null)
            {
                _targetIp = null;
                currentIcmpMs = -1f;
                currentStatus = Status.NoTarget;
            }
            return;
        }
        if (ip != _targetIp)
        {
            _targetIp = ip;
            currentIcmpMs = -1f;
            currentStatus = Status.Probing;
        }
    }

    // Cheap main-thread tick. Kicks a background probe when one is due and none
    // is already in flight. Safe to call every frame.
    public static void Poll()
    {
        var ip = _targetIp;
        if (string.IsNullOrEmpty(ip)) return;
        if ((DateTime.UtcNow - _lastProbeUtc).TotalMilliseconds < IntervalMs) return;
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0) return;
        _lastProbeUtc = DateTime.UtcNow;
        Task.Run(() => ProbeOnce(ip));
    }

    static void ProbeOnce(string ip)
    {
        try
        {
            using (var ping = new Ping())
            {
                var reply = ping.Send(ip, TimeoutMs);
                if (reply != null && reply.Status == IPStatus.Success)
                {
                    currentIcmpMs = reply.RoundtripTime;
                    currentStatus = Status.Ok;
                }
                else
                {
                    currentIcmpMs = -1f;
                    currentStatus =
                        reply == null ? Status.Error :
                        reply.Status == IPStatus.TimedOut ? Status.Timeout :
                        (reply.Status == IPStatus.DestinationHostUnreachable
                         || reply.Status == IPStatus.DestinationNetworkUnreachable
                         || reply.Status == IPStatus.DestinationUnreachable) ? Status.Unreachable :
                        Status.Error;
                }
            }
        }
        catch (Exception e)
        {
            currentIcmpMs = -1f;
            currentStatus = Status.Error;
            Plugin.LogDebug($"[FrameProfiler][ICMP] probe to {ip} failed: {e.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _inFlight, 0);
        }
    }

    public static void Reset()
    {
        _targetIp = null;
        currentIcmpMs = -1f;
        currentStatus = Status.NoTarget;
        _lastProbeUtc = DateTime.MinValue;
    }

    // Short human-readable status for the overlay when there's no usable
    // reading (i.e. currentIcmpMs < 0).
    public static string StatusText()
    {
        switch (currentStatus)
        {
            case Status.Probing:     return "probing…";
            case Status.Timeout:     return "blocked/timeout";
            case Status.Unreachable: return "unreachable";
            case Status.Error:       return "n/a";
            default:                 return "—";
        }
    }
}
