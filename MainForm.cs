// MainForm.cs — main window: toolbar + scrollable device card list

using System.Drawing;
using System.Windows.Forms;

namespace BoatTronClient;

public class MainForm : Form
{
    // ── State ──────────────────────────────────────────────────────────────
    private readonly ClientSettings _settings;
    private readonly WifiManager    _wifi = new();
    private readonly Dictionary<string, DeviceCard> _cards = new();
    private readonly System.Windows.Forms.Timer _autoRefresh;
    private bool _scanning;

    // ── Controls ───────────────────────────────────────────────────────────
    private readonly Label  _lblStatus;
    private readonly Panel  _cardContainer;

    // ── Constructor ────────────────────────────────────────────────────────
    public MainForm()
    {
        _settings = ClientSettings.Load();

        Text          = "BoatTron Monitor  v2.6 — DigiTron Sensors";
        MinimumSize   = new Size(720, 300);
        Size          = new Size(820, 500);
        StartPosition = FormStartPosition.CenterScreen;

        BuildToolbar(out _lblStatus);
        _cardContainer = BuildCardPanel();

        // Auto-refresh every 10 minutes
        _autoRefresh = new System.Windows.Forms.Timer { Interval = 600_000 };
        _autoRefresh.Tick += (_, _) => _ = ScanAsync();
        _autoRefresh.Start();

        // Scan on startup
        Load += async (_, _) => await ScanAsync();
    }

    // ── Layout ─────────────────────────────────────────────────────────────
    private void BuildToolbar(out Label lblStatus)
    {
        var toolbar = new Panel
        {
            Dock    = DockStyle.Top,
            Height  = 40,
            Padding = new Padding(6, 6, 6, 0),
        };

        // ── App name / version — upper left ───────────────────────────────
        var lblAppName = new Label
        {
            Text      = "BoatTron Monitor  v2.6",
            Location  = new Point(8, 10),
            AutoSize  = true,
            Font      = new Font("Segoe UI", 11, FontStyle.Bold),
            ForeColor = Color.FromArgb(26, 58, 106),   // dark navy
        };

        var btnScan     = MkButton("Scan Now",  90, async (_, _) => await ScanAsync());
        var btnSettings = MkButton("Settings",  80, OnOpenSettings);

        btnScan.Location     = new Point(200, 6);
        btnSettings.Location = new Point(298, 6);

        lblStatus = new Label
        {
            Location  = new Point(390, 10),
            AutoSize  = true,
            ForeColor = Color.Gray,
            Font      = new Font("Segoe UI", 9),
        };

        toolbar.Controls.Add(lblAppName);
        toolbar.Controls.Add(btnScan);
        toolbar.Controls.Add(btnSettings);
        toolbar.Controls.Add(lblStatus);
        Controls.Add(toolbar);
    }

    private Panel BuildCardPanel()
    {
        // Outer panel that fills the remainder of the form
        var outer = new Panel { Dock = DockStyle.Fill };

        // AutoScroll panel that holds the cards
        var scrollPanel = new Panel
        {
            Dock       = DockStyle.Fill,
            AutoScroll = true,
            Padding    = new Padding(4),
        };

        // Inner flow panel for the cards
        var flow = new FlowLayoutPanel
        {
            FlowDirection    = FlowDirection.TopDown,
            WrapContents     = false,
            AutoSize         = true,
            AutoSizeMode     = AutoSizeMode.GrowAndShrink,
            Dock             = DockStyle.Top,
        };
        flow.SizeChanged += (_, _) => scrollPanel.AutoScrollMinSize = new Size(0, flow.Height);

        scrollPanel.Controls.Add(flow);
        outer.Controls.Add(scrollPanel);
        Controls.Add(outer);

        // Store a reference to the FlowLayoutPanel via Tag
        scrollPanel.Tag = flow;
        outer.Tag       = scrollPanel;
        return flow;
    }

    // ── Scan ───────────────────────────────────────────────────────────────
    private async Task ScanAsync()
    {
        if (_scanning) return;
        _scanning = true;
        SetStatus("Scanning...");

        try
        {
            var results = await DeviceClient.DiscoverAsync(_settings.UdpPort);
            ApplyScanResults(results);
        }
        catch (Exception ex)
        {
            SetStatus($"Scan error: {ex.Message}");
        }
        finally
        {
            _scanning = false;
        }
    }

    private void ApplyScanResults(List<DeviceInfo> results)
    {
        foreach (var info in results)
        {
            var key = string.IsNullOrEmpty(info.Serial) ? info.IP : info.Serial;

            // Update name registry
            if (!string.IsNullOrEmpty(info.Serial) && !string.IsNullOrEmpty(info.Name))
                _settings.DeviceRegistry[info.Serial] = info.Name;

            if (_cards.TryGetValue(key, out var existing))
            {
                existing.UpdateDisplay(info);
            }
            else
            {
                var card = new DeviceCard(info) { Width = _cardContainer.Width - 12 };
                card.ConfigureRequested += OnConfigureRequested;
                card.AlarmsRequested    += OnAlarmsRequested;
                _cards[key] = card;
                _cardContainer.Controls.Add(card);
            }
        }

        _settings.Save();
        int n = results.Count;
        SetStatus($"{n} device{(n == 1 ? "" : "s")} found — {DateTime.Now:HH:mm:ss}");
    }

    // ── Configure ──────────────────────────────────────────────────────────
    private async void OnConfigureRequested(object? sender, DeviceInfo info)
    {
        var ip = await ResolveIpAsync(info);
        if (ip == null) return;

        // Refresh info from device before showing dialog
        DeviceInfo current = info;
        try { current = await DeviceClient.GetInfoAsync(ip); }
        catch { /* use cached info */ }

        using var dlg = new ConfigureForm(current);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        // Name uniqueness check
        if (dlg.ConfigUpdates != null &&
            dlg.ConfigUpdates.TryGetValue("name", out var nameObj))
        {
            var conflict = _settings.FindNameConflict(current.Serial, nameObj.ToString()!);
            if (conflict != null)
            {
                MessageBox.Show(
                    $"Name already used by device serial {conflict}.\nChoose a different name.",
                    "Name conflict", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        try
        {
            if (dlg.RebootRequested)
            {
                await DeviceClient.RebootAsync(ip);
            }
            else if (dlg.ConfigUpdates != null)
            {
                var updated = await DeviceClient.PostConfigAsync(ip, dlg.ConfigUpdates);
                UpdateCard(updated);

                if (!string.IsNullOrEmpty(updated.Serial))
                    _settings.RegisterDevice(updated.Serial, updated.Name);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Save failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            await _wifi.RestoreAsync();
        }
    }

    // ── Alarms ─────────────────────────────────────────────────────────────
    private async void OnAlarmsRequested(object? sender, DeviceInfo info)
    {
        var ip = await ResolveIpAsync(info);
        if (ip == null) return;

        DeviceInfo current = info;
        try { current = await DeviceClient.GetInfoAsync(ip); }
        catch { /* use cached */ }

        using var dlg = new AlarmsForm(current);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result == null) return;

        try
        {
            var updated = await DeviceClient.PostConfigAsync(ip, new { alarms = dlg.Result });
            UpdateCard(updated);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Alarm save failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            await _wifi.RestoreAsync();
        }
    }

    // ── IP resolution (LAN or AP) ──────────────────────────────────────────
    private async Task<string?> ResolveIpAsync(DeviceInfo info)
    {
        // If device has a known IP from LAN discovery, use it directly
        if (!string.IsNullOrEmpty(info.IP))
            return info.IP;

        // Device is AP-only — offer to switch WiFi to its AP
        var apSsid = $"{info.Brand}-{info.DeviceTag}";
        var prompt = $"Device '{info.Name}' has no LAN IP.\n" +
                     $"Connect to its AP ({apSsid})?\n\n" +
                     $"Your PC will switch to the BoatTron AP temporarily.";

        if (MessageBox.Show(prompt, "AP connection required",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return null;

        // Ask for AP password — verify against stored hash
        var pass = PromptPassword($"AP password for {apSsid}:");
        if (pass == null) return null;

        if (!_settings.VerifyPassword(pass))
        {
            MessageBox.Show("Password does not match the stored hash.",
                            "Wrong password", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }

        SetStatus($"Connecting to {apSsid}...");
        var connected = await _wifi.ConnectToApAsync(apSsid, pass);
        if (!connected)
        {
            MessageBox.Show($"Could not connect to {apSsid}.\n" +
                            "Ensure the device is powered on and in AP mode.",
                            "Connection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus("AP connection failed.");
            return null;
        }

        SetStatus($"Connected to {apSsid}");
        return "192.168.4.1";   // default AP gateway IP assigned by MicroPython
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private void UpdateCard(DeviceInfo info)
    {
        var key = string.IsNullOrEmpty(info.Serial) ? info.IP : info.Serial;
        if (_cards.TryGetValue(key, out var card))
            card.UpdateDisplay(info);
    }

    private void SetStatus(string msg)
    {
        if (InvokeRequired) Invoke(() => SetStatus(msg));
        else _lblStatus.Text = msg;
    }

    private void OnOpenSettings(object? s, EventArgs e)
    {
        using var dlg = new SettingsForm(_settings);
        dlg.ShowDialog(this);
    }

    private static string? PromptPassword(string prompt)
    {
        using var dlg = new Form
        {
            Text            = "Password",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            ClientSize      = new Size(360, 110),
            MaximizeBox     = false,
            MinimizeBox     = false,
        };
        dlg.Controls.Add(new Label { Text = prompt, Location = new Point(12, 16), Width = 330, AutoSize = false });
        var tb = new TextBox { PasswordChar = '*', Location = new Point(12, 40), Width = 330, MaxLength = 64 };
        var ok = new Button { Text = "OK",     Location = new Point(192, 72), Width = 72, Height = 26, DialogResult = DialogResult.OK };
        var cn = new Button { Text = "Cancel", Location = new Point(272, 72), Width = 72, Height = 26, DialogResult = DialogResult.Cancel };
        dlg.Controls.Add(tb); dlg.Controls.Add(ok); dlg.Controls.Add(cn);
        dlg.AcceptButton = ok; dlg.CancelButton = cn;
        return dlg.ShowDialog() == DialogResult.OK ? tb.Text : null;
    }

    private static Button MkButton(string text, int width, EventHandler handler)
    {
        var b = new Button { Text = text, Width = width, Height = 28, FlatStyle = FlatStyle.System };
        b.Click += handler;
        return b;
    }
}
