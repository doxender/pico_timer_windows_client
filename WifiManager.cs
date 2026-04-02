// WifiManager.cs — WiFi switching via Windows netsh (no third-party libs)
// netsh wlan operations do not require elevation for user-scope profiles.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace BoatTronClient;

public class WifiManager
{
    private string? _savedSsid;

    // ── SSID scan ──────────────────────────────────────────────────────────
    // Triggers an active hardware sweep via WlanScan(), waits for it to
    // complete, then polls netsh every second for new SSIDs.  Runs until
    // the CancellationToken is cancelled (Stop button or AP found).

    public static async Task<List<string>> ScanSsidsAsync(
        IProgress<string>? progress = null,
        Action<int>? onPollComplete = null,
        CancellationToken ct = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all  = new List<string>();

        // Trigger an immediate hardware channel sweep and wait for it.
        // WlanScan() commands the adapter directly — unlike netsh which
        // only reads the stale cache.
        await TriggerWlanScanAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            var output      = await RunNetshAsync("wlan show networks mode=bssid");
            int newThisPoll = 0;

            foreach (var line in output.Split('\n'))
            {
                // Match "SSID 1 : MyNetwork" but not "BSSID 1 : ..."
                var m = Regex.Match(line, @"^\s*SSID\s+\d+\s*:\s*(.+)$");
                if (m.Success)
                {
                    var ssid = m.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(ssid) && seen.Add(ssid))
                    {
                        all.Add(ssid);
                        newThisPoll++;
                        progress?.Report(ssid);
                    }
                }
            }

            onPollComplete?.Invoke(newThisPoll);

            try { await Task.Delay(1000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        return all;
    }

    // ── WlanScan P/Invoke ─────────────────────────────────────────────────

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanOpenHandle(
        uint dwClientVersion, IntPtr pReserved,
        out uint pdwNegotiatedVersion, out IntPtr phClientHandle);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanEnumInterfaces(
        IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);

    [DllImport("wlanapi.dll", SetLastError = true)]
    private static extern uint WlanScan(
        IntPtr hClientHandle, ref Guid pInterfaceGuid,
        IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);

    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    private static async Task TriggerWlanScanAsync(CancellationToken ct)
    {
        await Task.Run(() =>
        {
            if (WlanOpenHandle(2, IntPtr.Zero, out _, out var handle) != 0) return;
            try
            {
                if (WlanEnumInterfaces(handle, IntPtr.Zero, out var listPtr) != 0) return;
                try
                {
                    // WLAN_INTERFACE_INFO_LIST: dwNumberOfItems (4) + dwIndex (4) + items
                    int count = Marshal.ReadInt32(listPtr);
                    IntPtr itemPtr = listPtr + 8; // skip header
                    for (int i = 0; i < count; i++)
                    {
                        var guid = Marshal.PtrToStructure<Guid>(itemPtr);
                        WlanScan(handle, ref guid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                        itemPtr += 532; // sizeof(WLAN_INTERFACE_INFO)
                    }
                }
                finally { WlanFreeMemory(listPtr); }
            }
            finally { WlanCloseHandle(handle, IntPtr.Zero); }
        }, ct);

        // Give the adapter ~2 s to complete the channel sweep
        try { await Task.Delay(2000, ct).ConfigureAwait(false); }
        catch (OperationCanceledException) { }
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
