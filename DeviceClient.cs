// DeviceClient.cs — HTTP JSON API + UDP discovery for BoatTron devices

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace BoatTronClient;

public static class DeviceClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ── Discovery ──────────────────────────────────────────────────────────

    public static async Task<List<DeviceInfo>> DiscoverAsync(int udpPort)
    {
        var results = new List<DeviceInfo>();

        Log.Info($"Discovery: broadcasting BOATRON_DISCOVER on UDP port {udpPort}");

        using var udp = new UdpClient();
        udp.EnableBroadcast = true;

        var msg      = Encoding.ASCII.GetBytes("BOATRON_DISCOVER");
        var endpoint = new IPEndPoint(IPAddress.Broadcast, udpPort);
        await udp.SendAsync(msg, msg.Length, endpoint);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (true)
        {
            try
            {
                var result = await udp.ReceiveAsync(cts.Token);
                var json   = Encoding.UTF8.GetString(result.Buffer);
                Log.Info($"Discovery: response from {result.RemoteEndPoint.Address}: {json}");
                var info   = JsonSerializer.Deserialize<DeviceInfo>(json, JsonOpts);
                if (info != null)
                {
                    info.IP = result.RemoteEndPoint.Address.ToString();
                    results.Add(info);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException ex)         { Log.Warn($"Discovery: socket error: {ex.Message}"); break; }
            catch (Exception ex)               { Log.Warn($"Discovery: skipping malformed response: {ex.Message}"); }
        }

        Log.Info($"Discovery: found {results.Count} device(s)");
        return results;
    }

    // ── HTTP API ───────────────────────────────────────────────────────────

    public static async Task<DeviceInfo> GetInfoAsync(string ip)
    {
        Log.Info($"GET /api/info from {ip}");
        try
        {
            var json = await Http.GetStringAsync($"http://{ip}/api/info");
            var info = JsonSerializer.Deserialize<DeviceInfo>(json, JsonOpts)
                       ?? throw new InvalidOperationException("Empty response from device.");
            info.IP = ip;
            return info;
        }
        catch (Exception ex) { Log.Error($"GET /api/info from {ip} failed", ex); throw; }
    }

    public static async Task<DeviceInfo> PostConfigAsync(string ip, object payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload);
        Log.Info($"POST /api/config to {ip}: {payloadJson}");
        try
        {
            var body = new StringContent(payloadJson, Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync($"http://{ip}/api/config", body);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            var info = JsonSerializer.Deserialize<DeviceInfo>(json, JsonOpts)
                       ?? throw new InvalidOperationException("Empty response from device.");
            info.IP = ip;
            return info;
        }
        catch (Exception ex) { Log.Error($"POST /api/config to {ip} failed", ex); throw; }
    }

    public static async Task RebootAsync(string ip)
    {
        Log.Info($"POST /api/reboot to {ip}");
        var body = new StringContent("{}", Encoding.UTF8, "application/json");
        await Http.PostAsync($"http://{ip}/api/reboot", body);
    }
}
