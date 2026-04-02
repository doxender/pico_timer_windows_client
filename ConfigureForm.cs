// ConfigureForm.cs — modal device configuration dialog (tabbed AP / Network)

using System.Drawing;
using System.Windows.Forms;

namespace BoatTronClient;

public class ConfigureForm : Form
{
    // ── Shared fields ──────────────────────────────────────────────────────
    private readonly TextBox   _tbName;
    private readonly TextBox   _tbApPass;
    private readonly CheckBox  _cbStandalone;

    // Network-only fields
    private readonly ComboBox  _cbSsid;
    private readonly TextBox   _tbWifiPass;
    private readonly Button    _btnScan;

    // Tabs
    private readonly TabControl  _tabs;
    private readonly TabPage     _apTab;
    private readonly TabPage     _lanTab;

    // Device info at time of opening
    private readonly DeviceInfo _info;

    // Result
    public Dictionary<string, object>? ConfigUpdates { get; private set; }
    public bool RebootRequested { get; private set; }

    public ConfigureForm(DeviceInfo info)
    {
        _info = info;

        // Title shows the device's fixed hardware identity, not just the user name
        Text            = $"Configure — {info.Brand}-{info.DeviceTag}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ClientSize      = new Size(520, 430);

        // ── Shared control instances ───────────────────────────────────────
        _tbName       = new TextBox { Text = info.Name, Width = 260, MaxLength = 32 };
        _tbApPass     = new TextBox { PasswordChar = '*', Width = 260, MaxLength = 64 };
        _cbStandalone = new CheckBox { Text = "Standalone mode (always stay in AP/hotspot mode)", Checked = info.Standalone };
        _cbSsid       = new ComboBox { Text = info.WifiSsid, Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
        _tbWifiPass   = new TextBox { PasswordChar = '*', Width = 260, MaxLength = 64 };
        _btnScan      = new Button  { Text = "Scan", Width = 60, Height = 26 };
        _btnScan.Click += OnScanSsids;

        // ── Tabs ───────────────────────────────────────────────────────────
        _tabs   = new TabControl { Dock = DockStyle.Fill };
        _apTab  = new TabPage("AP Mode");
        _lanTab = new TabPage("Network Mode");
        _tabs.TabPages.AddRange(new[] { _apTab, _lanTab });

        BuildApTab();
        BuildLanTab();

        // Select the currently active mode tab
        _tabs.SelectedTab = info.IsOnLan ? _lanTab : _apTab;

        // ── Button strip ───────────────────────────────────────────────────
        var btnPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            Height        = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding       = new Padding(8, 8, 8, 0),
        };

        var btnSave   = new Button { Text = "Save",   Width = 88, Height = 30 };
        var btnCancel = new Button { Text = "Cancel", Width = 88, Height = 30, DialogResult = DialogResult.Cancel };
        var btnReboot = new Button { Text = "Reboot", Width = 88, Height = 30 };

        btnSave.Click   += OnSave;
        btnReboot.Click += OnReboot;
        CancelButton     = btnCancel;

        btnPanel.Controls.Add(btnCancel);
        btnPanel.Controls.Add(btnSave);
        btnPanel.Controls.Add(btnReboot);

        Controls.Add(_tabs);
        Controls.Add(btnPanel);
    }

    // ── Tab builders ───────────────────────────────────────────────────────

    private void BuildApTab()
    {
        var f = _apTab;
        int y = 20;

        AddRow(f, "Device name:", _tbName,   ref y);
        y += 8;

        // Hotspot password — "AP" is jargon; use plain language
        AddRow(f, "Hotspot password:", _tbApPass, ref y);
        AddHint(f, "(leave blank to keep the current password)", ref y);
        y += 8;

        _cbStandalone.Location = new Point(140, y);
        _cbStandalone.Width    = 340;
        f.Controls.Add(_cbStandalone);
    }

    private void BuildLanTab()
    {
        var f = _lanTab;
        int y = 20;

        // Name — shares the same TextBox instance as AP tab
        AddRow(f, "Device name:", _tbName, ref y);
        y += 8;

        // SSID row: combobox + scan button side by side
        var ssidPanel = new Panel { Width = 290, Height = 26, Location = new Point(140, y) };
        _cbSsid.Location  = new Point(0, 0);
        _btnScan.Location = new Point(_cbSsid.Width + 4, 0);
        ssidPanel.Controls.Add(_cbSsid);
        ssidPanel.Controls.Add(_btnScan);
        AddLabel(f, "WiFi network:", y);
        f.Controls.Add(ssidPanel);
        y += 44;

        AddRow(f, "WiFi password:", _tbWifiPass, ref y);
        y += 8;

        AddRow(f, "Hotspot password:", _tbApPass, ref y);
        AddHint(f, "(leave blank to keep the current password)", ref y);
        y += 8;

        _cbStandalone.Location = new Point(140, y);
        _cbStandalone.Width    = 340;
        f.Controls.Add(_cbStandalone);
    }

    private static void AddRow(Control parent, string label, Control ctl, ref int y)
    {
        AddLabel(parent, label, y);
        ctl.Location = new Point(140, y);
        parent.Controls.Add(ctl);
        y += 44;
    }

    private static void AddLabel(Control parent, string text, int y)
    {
        parent.Controls.Add(new Label
        {
            Text      = text,
            Location  = new Point(12, y + 3),
            Width     = 124,
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            Font      = new Font("Segoe UI", 9),
        });
    }

    private static void AddHint(Control parent, string text, ref int y)
    {
        parent.Controls.Add(new Label
        {
            Text      = text,
            Location  = new Point(140, y - 38),
            Width     = 340,
            ForeColor = System.Drawing.Color.Gray,
            Font      = new Font("Segoe UI", 8),
        });
    }

    // ── SSID scan ──────────────────────────────────────────────────────────
    private async void OnScanSsids(object? s, EventArgs e)
    {
        _btnScan.Enabled = false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var ssids = await WifiManager.ScanSsidsAsync(ct: cts.Token);
            _cbSsid.Items.Clear();
            _cbSsid.Items.AddRange(ssids.Cast<object>().ToArray());
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Scan failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _btnScan.Enabled = true; }
    }

    // ── Save ───────────────────────────────────────────────────────────────
    private void OnSave(object? s, EventArgs e)
    {
        var name = _tbName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Device name cannot be empty.", "Invalid",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var updates = new Dictionary<string, object>
        {
            ["name"]       = name,
            ["standalone"] = _cbStandalone.Checked,
        };

        var apPass = _tbApPass.Text;
        if (!string.IsNullOrEmpty(apPass))
            updates["ap_pass"] = apPass;

        // If Network tab is active and SSID is provided, include WiFi credentials
        if (_tabs.SelectedTab == _lanTab)
        {
            var ssid = _cbSsid.Text.Trim();
            if (!string.IsNullOrEmpty(ssid))
            {
                updates["wifi_ssid"] = ssid;
                updates["wifi_pass"] = _tbWifiPass.Text;
            }
        }

        ConfigUpdates = updates;
        DialogResult  = DialogResult.OK;
        Close();
    }

    // ── Reboot ─────────────────────────────────────────────────────────────
    private void OnReboot(object? s, EventArgs e)
    {
        if (MessageBox.Show("Reboot the device now?", "Confirm reboot",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            RebootRequested = true;
            DialogResult    = DialogResult.OK;
            Close();
        }
    }
}
