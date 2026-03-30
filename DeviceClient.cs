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

        using var udp = new UdpClient();
        udp.EnableBroadcast = true;

        var msg      = Encoding.ASCII.GetBytes("BOATRON_DISCOVER");
        var endpoint = new IPEndPoint(IPAddress.Broadcast, udpPort);
        await udp.SendAsync(msg, msg.Length, endpoint);

        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            int remaining = Math.Max(1, (int)(deadline - DateTime.UtcNow).TotalMilliseconds);
            udp.Client.ReceiveTimeout = remaining;
            try
            {
                var result = await udp.ReceiveAsync();
                var json   = Encoding.UTF8.GetString(result.Buffer);
                var info   = JsonSerializer.Deserialize<DeviceInfo>(json, JsonOpts);
                if (info != null)
                {
                    info.IP = result.RemoteEndPoint.Address.ToString();
                    results.Add(info);
                }
            }
            catch (SocketException) { break; }
            catch { /* skip malformed responses */ }
        }

        return results;
    }

    // ── HTTP API ───────────────────────────────────────────────────────────

    public static async Task<DeviceInfo> GetInfoAsync(string ip)
    {
        var json = await Http.GetStringAsync($"http://{ip}/api/info");
        var info = JsonSerializer.Deserialize<DeviceInfo>(json, JsonOpts)
                   ?? throw new InvalidOperationException("Empty response from device.");
        info.IP = ip;
        return info;
    }

    public static async Task<DeviceInfo> PostConfigAsync(string ip, object payload)
    {
        var body = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        var resp = await Http.PostAsync($"http://{ip}/api/config", body);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var info = JsonSerializer.Deserialize<DeviceInfo>(json, JsonOpts)
                   ?? throw new InvalidOperationException("Empty response from device.");
        info.IP = ip;
        return info;
    }

    public static async Task RebootAsync(string ip)
    {
        var body = new StringContent("{}", Encoding.UTF8, "application/json");
        await Http.PostAsync($"http://{ip}/api/reboot", body);
    }
}
