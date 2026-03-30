// ClientSettings.cs — persistent client-side settings
// Stored in %APPDATA%\DigiTronSensors\BoatTronClient\settings.json

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoatTronClient;

public class ClientSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DigiTronSensors", "BoatTronClient");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts =
        new() { WriteIndented = true };

    // AP password stored only as SHA-256 hex — never plain text
    [JsonPropertyName("ap_pass_hash")]
    public string ApPassHash { get; set; } = HashPassword("DigiTron_1234");

    [JsonPropertyName("udp_port")]
    public int UdpPort { get; set; } = 5757;

    // serial → friendly name, for uniqueness enforcement
    [JsonPropertyName("device_registry")]
    public Dictionary<string, string> DeviceRegistry { get; set; } = new();

    // ── Persistence ────────────────────────────────────────────────────────

    public static ClientSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<ClientSettings>(json) ?? new();
            }
        }
        catch { /* first run or corrupt — use defaults */ }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDir);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts));
    }

    // ── Password helpers ───────────────────────────────────────────────────

    public static string HashPassword(string plaintext)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool VerifyPassword(string plaintext) =>
        string.Equals(HashPassword(plaintext), ApPassHash,
                      StringComparison.OrdinalIgnoreCase);

    // ── Name registry ──────────────────────────────────────────────────────

    /// <summary>
    /// Check if the given name is already used by a different serial.
    /// Returns the owning serial, or null if the name is free.
    /// </summary>
    public string? FindNameConflict(string serial, string name)
    {
        foreach (var kvp in DeviceRegistry)
        {
            if (kvp.Key != serial &&
                string.Equals(kvp.Value, name, StringComparison.OrdinalIgnoreCase))
                return kvp.Key;
        }
        return null;
    }

    public void RegisterDevice(string serial, string name)
    {
        DeviceRegistry[serial] = name;
        Save();
    }
}
