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
    private bool    _scanning;
    private bool    _inApMode;      // true while connected to a BoatTron AP
    private string? _apPassword;    // saved so we can connect to the next AP

    // ── Controls ───────────────────────────────────────────────────────────
    // [0] = current main status (bold); [1..5] = sub-status history (newest first)
    private readonly Label[] _statusLines = new Label[6];
    private readonly Panel   _cardContainer;
    private Button           _btnScan = null!;   // assigned in BuildToolbar
    private CancellationTokenSource? _scanCts;

    // ── Constructor ────────────────────────────────────────────────────────
    public MainForm()
    {
        _settings = ClientSettings.Load();

        Text          = "BoatTron Monitor  v2.23 — DigiTron Sensors";
        MinimumSize   = new Size(860, 540);
        Size          = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterScreen;

        BuildToolbar();
        _cardContainer = BuildCardPanel();

        // Auto-refresh every 10 minutes
        _autoRefresh = new System.Windows.Forms.Timer { Interval = 600_000 };
        _autoRefresh.Tick += (_, _) => _ = ScanAsync();
        _autoRefresh.Start();

        // Scan on startup
        Load += async (_, _) =>
        {
            Log.Info("Application started");
            await ScanAsync();
        };
    }

    // ── Layout ─────────────────────────────────────────────────────────────
    private void BuildToolbar()
    {
        var btnFont  = new Font("Segoe UI", 9);
        var statFont = new Font("Segoe UI", 9, FontStyle.Bold);
        var subFont  = new Font("Segoe UI", 8);

        // ── Thin button bar (Scan Now only) ────────────────────────────────
        var btnBar   = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = SystemColors.Control };
        var btnPanel = new Panel { Dock = DockStyle.Left };
        _btnScan = new Button { Text = "Scan Now", FlatStyle = FlatStyle.System, Font = btnFont };
        _btnScan.Click += async (_, _) =>
        {
            if (_scanning)
                _scanCts?.Cancel();
            else
                await ScanAsync();
        };
        btnPanel.Controls.Add(_btnScan);
        btnBar.Controls.Add(btnPanel);

        // Right-click anywhere on the form → Settings (removes need for a button)
        var ctx = new ContextMenuStrip();
        ctx.Items.Add("Settings…", null, OnOpenSettings);
        ContextMenuStrip = ctx;

        // ── 6-line status panel (centered below buttons) ───────────────────
        // [0] = current status (bold), [1..5] = sub-status history (newest→oldest)
        var statusPanel = new Panel { Dock = DockStyle.Top, BackColor = SystemColors.Control };
        for (int i = 0; i < 6; i++)
        {
            _statusLines[i] = new Label
            {
                AutoSize  = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = i == 0 ? statFont : subFont,
                ForeColor = i == 0 ? Color.FromArgb(30, 30, 30) : Color.Gray,
            };
            statusPanel.Controls.Add(_statusLines[i]);
        }

        Controls.Add(btnBar);
        Controls.Add(statusPanel);

        // Keep all label widths equal to the status panel's current width
        void UpdateWidths()
        {
            // Fall back to the form's client width if the panel hasn't sized yet
            int w = statusPanel.ClientSize.Width > 0
                        ? statusPanel.ClientSize.Width
                        : ClientSize.Width;
            if (w <= 0) return;
            foreach (var lbl in _statusLines)
                lbl.Width = w;
        }
        statusPanel.Resize += (_, _) => UpdateWidths();

        // Compute all sizes from live font metrics — correct at any DPI
        void RecalculateLayout()
        {
            int lineH  = TextRenderer.MeasureText("Mg", statFont).Height;
            int btnH   = lineH + 10;
            int margin = Math.Max(8, lineH / 3);
            int bw     = Math.Max(TextRenderer.MeasureText("Stop Scanning", btnFont).Width, 80) + 24;

            btnBar.Height  = btnH + 2 * margin;
            btnPanel.Width = 8 + bw + 8;
            _btnScan.SetBounds(8, (btnBar.Height - btnH) / 2, bw, btnH);

            // Place the 6 labels vertically; widths set by UpdateWidths()
            int boldH = lineH + 10;
            int subH  = lineH + 6;
            int y     = 18;
            _statusLines[0].SetBounds(0, y, 0, boldH); y += boldH + 8;
            for (int i = 1; i < 6; i++)
            {
                _statusLines[i].SetBounds(0, y, 0, subH); y += subH;
            }
            statusPanel.Height = y + 18;
            UpdateWidths();   // always runs after panel height is set
        }

        Load                          += (_, _) => RecalculateLayout();
        btnBar.DpiChangedAfterParent  += (_, _) => RecalculateLayout();
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
        _scanCts  = new CancellationTokenSource();
        if (InvokeRequired) Invoke(() => _btnScan.Text = "Stop Scanning");
        else _btnScan.Text = "Stop Scanning";
        SetStatus("Scanning...");
        Log.Info("Scan started");

        var progress = new Progress<string>(msg =>
        {
            SetSubStatus(msg);
            Log.Info($"Scan: {msg}");
        });

        try
        {
            // ── Step 1: try LAN UDP broadcast (4 seconds) ────────────────
            SetStatus("Scanning...");
            SetSubStatus("Scanning current network...");
            var results = await DeviceClient.DiscoverAsync(_settings.UdpPort, progress, timeout: 4);

            // ── Step 2: if nothing on LAN, scan WiFi for BoatTron APs ───
            if (results.Count == 0)
            {
                Log.Info("LAN scan empty — scanning WiFi spectrum for BoatTron APs");
                SetStatus("Scanning...");
                SetSubStatus("Scanning wireless spectrum for BoatTron access points...");

                var boatronAps  = new List<string>();
                int totalFound  = 0;
                bool boatronFound = false;

                // ── Smooth-scroll queue: display each network for ~1 second ──
                var scrollQueue = new Queue<string>();
                var queueLock   = new object();
                var scrollCts   = new CancellationTokenSource();

                var scrollTask = Task.Run(async () =>
                {
                    while (!scrollCts.Token.IsCancellationRequested)
                    {
                        string? next = null;
                        lock (queueLock)
                        {
                            if (scrollQueue.Count > 0)
                                next = scrollQueue.Dequeue();
                        }
                        if (next != null)
                            SetSubStatus(next);
                        try { await Task.Delay(1000, scrollCts.Token); }
                        catch (OperationCanceledException) { break; }
                    }
                });

                var wifiProgress = new Progress<string>(ssid =>
                {
                    totalFound++;
                    Log.Info($"WiFi scan found: {ssid}");
                    bool isBoatron = ssid.StartsWith("BoatTron-", StringComparison.OrdinalIgnoreCase) ||
                                     ssid.StartsWith("BoatTron_", StringComparison.OrdinalIgnoreCase);
                    if (isBoatron)
                    {
                        boatronAps.Add(ssid);
                        boatronFound = true;
                        SetStatus($"Found BoatTron AP: {ssid}");
                        // Clear pending non-BoatTron items; show the AP next; stop scan
                        lock (queueLock)
                        {
                            scrollQueue.Clear();
                            scrollQueue.Enqueue($">>> {ssid}");
                        }
                        Log.Info($"Found BoatTron AP: {ssid}");
                        _scanCts?.Cancel();
                    }
                    else if (!boatronFound)
                    {
                        // Queue non-BoatTron networks for 1-second-each display
                        lock (queueLock)
                            scrollQueue.Enqueue(ssid);
                    }
                });

                // "no new networks" feedback on each empty poll
                void OnPollComplete(int newCount)
                {
                    if (newCount == 0 && !boatronFound)
                        lock (queueLock)
                            scrollQueue.Enqueue("(no new networks found)");
                }

                // Scan runs indefinitely — cancelled by Stop button or AP found
                var ssids = await WifiManager.ScanSsidsAsync(
                    wifiProgress, OnPollComplete, _scanCts!.Token);

                // Stop the scroll consumer — don't wait for leftover queue items
                scrollCts.Cancel();
                try { await scrollTask; } catch { }

                Log.Info($"WiFi scan complete: {ssids.Count} network(s) found, {boatronAps.Count} BoatTron AP(s)");
                Log.Info($"All SSIDs: {string.Join(", ", ssids)}");

                if (boatronAps.Count > 0)
                {
                    SetSubStatus($"Scan complete — found {boatronAps.Count} BoatTron AP(s)");
                    var apList = string.Join("\n  • ", boatronAps);
                    var prompt = $"Wireless scan complete. Found {boatronAps.Count} BoatTron AP(s):\n  • {apList}\n\n" +
                                 $"Switch networks to connect and scan them?\n" +
                                 $"(Your original network will be restored after each device is configured.)";

                    var answer = MessageBox.Show(prompt, "Switch to BoatTron AP?",
                                     MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (answer == DialogResult.Yes)
                    {
                        if (PromptPasswordWithRetry("", out var pass))
                        {
                            _apPassword = pass!;
                            var apResults = await ConnectAndDiscoverAsync(boatronAps[0], progress);
                            results.AddRange(apResults);

                            // Auto-open Configure when connected via AP — don't make user hunt for the card
                            if (_inApMode && apResults.Count > 0)
                            {
                                ApplyScanResults(results);
                                _scanning = false;
                                SetStatus($"Connected to {boatronAps[0]} — configuring...");
                                OnConfigureRequested(this, apResults[0]);
                                return;
                            }
                        }
                    }
                }
            }

            ApplyScanResults(results);
            if (results.Count == 0)
                SetSubStatus("No devices found.");
        }
        catch (Exception ex)
        {
            Log.Error("Scan failed", ex);
            SetStatus($"Scan error: {ex.Message}");
            SetSubStatus(ex.Message);
        }
        finally
        {
            _scanning = false;
            _scanCts?.Dispose();
            _scanCts = null;
            _btnScan.Text = "Scan Now";
            Log.Info("Scan finished");
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

    // ── AP provisioning helpers ────────────────────────────────────────────

    private async Task<List<DeviceInfo>> ConnectAndDiscoverAsync(string ap,
        IProgress<string>? progress = null)
    {
        SetSubStatus($"Connecting to {ap}...");
        Log.Info($"Connecting to AP: {ap}");

        var connected = await _wifi.ConnectToApAsync(ap, _apPassword!);
        if (!connected)
        {
            Log.Warn($"Could not connect to {ap}");
            SetSubStatus($"Could not connect to {ap}.");
            return new List<DeviceInfo>();
        }

        _inApMode = true;
        Log.Info($"Connected to {ap}, discovering devices");

        // Try UDP broadcast first
        SetSubStatus($"Connected to {ap} — scanning...");
        var results = await DeviceClient.DiscoverAsync(_settings.UdpPort, progress, timeout: 3);

        // UDP often doesn't reach Pico AP subnet — fall back to direct HTTP
        if (results.Count == 0)
        {
            SetSubStatus("Trying direct connection to 192.168.4.1...");
            Log.Info("UDP returned nothing — trying direct HTTP to 192.168.4.1");
            try
            {
                var info = await DeviceClient.GetInfoAsync("192.168.4.1");
                results.Add(info);
                Log.Info($"Direct HTTP found device: {info.Name}");
            }
            catch (Exception ex)
            {
                Log.Warn($"Direct HTTP to 192.168.4.1 failed: {ex.Message}");
            }
        }

        // Still nothing — restore and inform user
        if (results.Count == 0)
        {
            Log.Warn($"No device found on {ap} — restoring network");
            SetSubStatus("No device found — restoring network...");
            await _wifi.RestoreAsync();
            _inApMode = false;
            MessageBox.Show(
                $"Connected to {ap} but no BoatTron device responded.\n\n" +
                "Check the device is powered on and try again.",
                "Device not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        return results;
    }

    private async Task ContinueApProvisioningAsync()
    {
        SetSubStatus("Restoring original network...");
        await _wifi.RestoreAsync();
        _inApMode = false;
        Log.Info("Network restored — scanning for more BoatTron APs");

        SetSubStatus("Looking for more BoatTron access points...");
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ssids      = await WifiManager.ScanSsidsAsync(ct: cts2.Token);
        var boatronAps = ssids.Where(s => s.StartsWith("BoatTron-",
                             StringComparison.OrdinalIgnoreCase) ||
                             s.StartsWith("BoatTron_",
                             StringComparison.OrdinalIgnoreCase)).ToList();

        Log.Info($"Found {boatronAps.Count} remaining BoatTron AP(s)");

        if (boatronAps.Count > 0 && _apPassword != null)
        {
            var apList = string.Join("\n  • ", boatronAps);
            var answer = MessageBox.Show(
                $"Found {boatronAps.Count} more BoatTron AP(s):\n  • {apList}\n\nConnect to next?",
                "Next device?", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer == DialogResult.Yes)
            {
                var progress = new Progress<string>(msg =>
                {
                    SetSubStatus(msg);
                    Log.Info($"AP scan: {msg}");
                });
                var results = await ConnectAndDiscoverAsync(boatronAps[0], progress);
                ApplyScanResults(results);
                SetStatus($"Connected to {boatronAps[0]} — configure device, then scan for next.");
                return;
            }
        }

        // No more APs or user declined
        _apPassword = null;
        SetSubStatus(boatronAps.Count == 0 ? "No more BoatTron APs found." : "");
        SetStatus($"{_cards.Count} device{(_cards.Count == 1 ? "" : "s")} found — {DateTime.Now:HH:mm:ss}");
    }

    private async Task AfterDeviceInteractionAsync()
    {
        if (_inApMode)
            await ContinueApProvisioningAsync();
        else
            await _wifi.RestoreAsync();
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
            await AfterDeviceInteractionAsync();
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
            await AfterDeviceInteractionAsync();
        }
    }

    // ── IP resolution (LAN or AP) ──────────────────────────────────────────
    private async Task<string?> ResolveIpAsync(DeviceInfo info)
    {
        // If device has a known IP from LAN or AP discovery, use it directly
        if (!string.IsNullOrEmpty(info.IP))
            return info.IP;

        // Already connected to its AP from the provisioning scan
        if (_inApMode)
            return "192.168.4.1";

        // Device is AP-only — offer to switch WiFi to its AP
        var apSsid = $"{info.Brand}-{info.DeviceTag}";
        var prompt = $"Device '{info.Name}' has no LAN IP.\n" +
                     $"Connect to its AP ({apSsid})?\n\n" +
                     $"Your PC will switch to the BoatTron AP temporarily.";

        if (MessageBox.Show(prompt, "AP connection required",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return null;

        // Ask for AP password — 5 attempts
        if (!PromptPasswordWithRetry(apSsid, out var pass))
            return null;

        SetStatus($"Connecting to {apSsid}...");
        var connected = await _wifi.ConnectToApAsync(apSsid, pass!);
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
        if (InvokeRequired) { Invoke(() => SetStatus(msg)); return; }
        _statusLines[0].Text = msg;
    }

    private void SetSubStatus(string msg)
    {
        if (InvokeRequired) { Invoke(() => SetSubStatus(msg)); return; }
        // Scroll UP: oldest lines move toward [1], new entry appears at bottom [5]
        for (int i = 1; i < 5; i++)
            _statusLines[i].Text = _statusLines[i + 1].Text;
        _statusLines[5].Text = msg;
    }

    private void OnOpenSettings(object? s, EventArgs e)
    {
        using var dlg = new SettingsForm(_settings);
        dlg.ShowDialog(this);
    }

    private bool PromptPasswordWithRetry(string context, out string? password)
    {
        for (int attempt = 1; attempt <= 5; attempt++)
        {
            string label = attempt == 1
                ? $"AP password{(context.Length > 0 ? $" for {context}" : "")}:"
                : $"AP password — attempt {attempt} of 5:";
            var pass = PromptPassword(label);
            if (pass == null) { password = null; return false; }
            if (_settings.VerifyPassword(pass)) { password = pass; return true; }
            string remaining = attempt < 5 ? $"{5 - attempt} attempt(s) remaining." : "No more attempts.";
            MessageBox.Show($"Incorrect password. {remaining}",
                "Wrong password", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        password = null;
        return false;
    }

    private static string? PromptPassword(string prompt)
    {
        using var dlg = new Form
        {
            Text            = "Password",
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition   = FormStartPosition.CenterParent,
            ClientSize      = new Size(400, 150),
            MaximizeBox     = false,
            MinimizeBox     = false,
        };
        dlg.Controls.Add(new Label
        {
            Text     = prompt,
            Location = new Point(12, 16),
            Width    = 370,
            Height   = 36,
            AutoSize = false,
            Font     = new Font("Segoe UI", 10),
        });
        var tb = new TextBox { PasswordChar = '*', Location = new Point(12, 60), Width = 370, MaxLength = 64, Font = new Font("Segoe UI", 10) };
        var ok = new Button { Text = "OK",     Location = new Point(204, 106), Width = 84, Height = 32, DialogResult = DialogResult.OK };
        var cn = new Button { Text = "Cancel", Location = new Point(296, 106), Width = 84, Height = 32, DialogResult = DialogResult.Cancel };
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
