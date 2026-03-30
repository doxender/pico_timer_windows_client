// ConfigureForm.cs — modal device configuration dialog (tabbed AP / Network)

using System.Drawing;
using System.Windows.Forms;

namespace BoatTronClient;

public class ConfigureForm : Form
{
    // ── Shared fields ──────────────────────────────────────────────────────
    private readonly TextBox   _tbName;
    private readonly TextBox   _tbApPass;
    private readonly TextBox   _tbUdpPort;
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

        Text            = $"Configure — {info.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ClientSize      = new Size(500, 320);

        // ── Shared control instances ───────────────────────────────────────
        _tbName       = new TextBox { Text = info.Name, Width = 240, MaxLength = 32 };
        _tbApPass     = new TextBox { PasswordChar = '*', Width = 240, MaxLength = 64 };
        _tbUdpPort    = new TextBox { Text = info.UdpPort.ToString(), Width = 80 };
        _cbStandalone = new CheckBox { Text = "Standalone (always AP mode)", Checked = info.Standalone };
        _cbSsid       = new ComboBox { Text = info.WifiSsid, Width = 200, DropDownStyle = ComboBoxStyle.DropDown };
        _tbWifiPass   = new TextBox { PasswordChar = '*', Width = 240, MaxLength = 64 };
        _btnScan      = new Button  { Text = "Scan", Width = 60, Height = 23 };
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
            Height        = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding       = new Padding(8, 6, 8, 0),
        };

        var btnSave   = new Button { Text = "Save",   Width = 80, Height = 28 };
        var btnCancel = new Button { Text = "Cancel", Width = 80, Height = 28, DialogResult = DialogResult.Cancel };
        var btnReboot = new Button { Text = "Reboot", Width = 80, Height = 28 };

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
        int y = 16;

        // All tabs have identical fields per spec — AP tab shows the shared controls
        AddRow(f, "Device name:", _tbName,       ref y);
        AddRow(f, "AP password:", _tbApPass,     ref y);
        AddHint(f, "(leave blank to keep current)", ref y);
        AddRow(f, "UDP port:",    _tbUdpPort,    ref y);
        y += 4;
        _cbStandalone.Location = new Point(130, y);
        f.Controls.Add(_cbStandalone);
    }

    private void BuildLanTab()
    {
        var f  = _lanTab;
        int y  = 16;

        // Name — shares the same TextBox instance
        AddRow(f, "Device name:", _tbName, ref y);

        // SSID row: combobox + scan button side by side
        var ssidPanel = new Panel { Width = 280, Height = 24, Location = new Point(130, y) };
        _cbSsid.Location  = new Point(0, 0);
        _btnScan.Location = new Point(_cbSsid.Width + 4, 0);
        ssidPanel.Controls.Add(_cbSsid);
        ssidPanel.Controls.Add(_btnScan);
        AddLabel(f, "WiFi SSID:", y);
        f.Controls.Add(ssidPanel);
        y += 32;

        AddRow(f, "WiFi password:", _tbWifiPass, ref y);

        // AP password — same TextBox instance as AP tab
        AddRow(f, "AP password:",   _tbApPass,   ref y);
        AddHint(f, "(leave blank to keep current)", ref y);

        AddRow(f, "UDP port:",      _tbUdpPort,  ref y);
        y += 4;
        _cbStandalone.Location = new Point(130, y);
        f.Controls.Add(_cbStandalone);
    }

    private static void AddRow(Control parent, string label, Control ctl, ref int y)
    {
        AddLabel(parent, label, y);
        ctl.Location = new Point(130, y);
        parent.Controls.Add(ctl);
        y += 32;
    }

    private static void AddLabel(Control parent, string text, int y)
    {
        parent.Controls.Add(new Label
        {
            Text      = text,
            Location  = new Point(12, y + 3),
            Width     = 114,
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
            Font      = new Font("Segoe UI", 9),
        });
    }

    private static void AddHint(Control parent, string text, ref int y)
    {
        parent.Controls.Add(new Label
        {
            Text      = text,
            Location  = new Point(130, y - 26),
            Width     = 280,
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
            var ssids = await WifiManager.ScanSsidsAsync();
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

        if (!int.TryParse(_tbUdpPort.Text.Trim(), out int port) || port < 1024 || port > 65535)
        {
            MessageBox.Show("UDP port must be 1024–65535.", "Invalid",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var updates = new Dictionary<string, object>
        {
            ["name"]       = name,
            ["udp_port"]   = port,
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
