// WifiManager.cs — WiFi switching via Windows netsh (no third-party libs)
// netsh wlan operations do not require elevation for user-scope profiles.

using System.Diagnostics;
using System.Text.RegularExpressions;

namespace BoatTronClient;

public class WifiManager
{
    private string? _savedSsid;

    // ── SSID scan ──────────────────────────────────────────────────────────

    public static async Task<List<string>> ScanSsidsAsync()
    {
        var output = await RunNetshAsync("wlan show networks mode=bssid");
        var ssids  = new List<string>();

        foreach (var line in output.Split('\n'))
        {
            // Match "SSID 1 : MyNetwork" but not "BSSID 1 : ..."
            var m = Regex.Match(line, @"^\s*SSID\s+\d+\s*:\s*(.+)$");
            if (m.Success)
            {
                var ssid = m.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(ssid))
                    ssids.Add(ssid);
            }
        }

        return ssids.Distinct().ToList();
    }

    // ── Connect to AP ──────────────────────────────────────────────────────

    public async Task<bool> ConnectToApAsync(string ssid, string password)
    {
        // Save current connection so we can restore later
        var ifaceInfo = await RunNetshAsync("wlan show interfaces");
        var m = Regex.Match(ifaceInfo, @"SSID\s*:\s*(.+)", RegexOptions.IgnoreCase);
        _savedSsid = m.Success ? m.Groups[1].Value.Trim() : null;

        // Write a temporary WPA2-Personal profile XML
        var profileXml = BuildWpa2ProfileXml(ssid, password);
        var xmlPath    = Path.GetTempFileName() + ".xml";
        await File.WriteAllTextAsync(xmlPath, profileXml);

        try
        {
            // Add for current user only — no elevation needed
            await RunNetshAsync($"wlan add profile filename=\"{xmlPath}\" user=current");
            await RunNetshAsync($"wlan connect name=\"{ssid}\"");

            // Wait up to 12s for the connection to establish
            var deadline = DateTime.UtcNow.AddSeconds(12);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(600);
                var status = await RunNetshAsync("wlan show interfaces");
                if (status.Contains(ssid, StringComparison.OrdinalIgnoreCase) &&
                    status.Contains("connected", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        finally
        {
            try { File.Delete(xmlPath); } catch { }
        }
    }

    // ── Restore original network ───────────────────────────────────────────

    public async Task RestoreAsync()
    {
        if (!string.IsNullOrEmpty(_savedSsid))
        {
            await RunNetshAsync($"wlan connect name=\"{_savedSsid}\"");
            _savedSsid = null;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static string BuildWpa2ProfileXml(string ssid, string password) => $"""
        <?xml version="1.0"?>
        <WLANProfile xmlns="http://www.microsoft.com/networking/WLAN/profile/v1">
          <name>{ssid}</name>
          <SSIDConfig>
            <SSID><name>{ssid}</name></SSID>
          </SSIDConfig>
          <connectionType>ESS</connectionType>
          <connectionMode>manual</connectionMode>
          <MSM>
            <security>
              <authEncryption>
                <authentication>WPA2PSK</authentication>
                <encryption>AES</encryption>
                <useOneX>false</useOneX>
              </authEncryption>
              <sharedKey>
                <keyType>passPhrase</keyType>
                <protected>false</protected>
                <keyMaterial>{password}</keyMaterial>
              </sharedKey>
            </security>
          </MSM>
        </WLANProfile>
        """;

    private static async Task<string> RunNetshAsync(string args)
    {
        var psi = new ProcessStartInfo("netsh", args)
        {
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        using var proc = Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return output;
    }
}
