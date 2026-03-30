// AlarmsForm.cs — modal dialog for viewing and editing the four device alarms

using System.Drawing;
using System.Windows.Forms;

namespace BoatTronClient;

public class AlarmsForm : Form
{
    private readonly DeviceInfo     _info;
    private readonly AlarmRow[]     _rows = new AlarmRow[4];
    private readonly Button         _btnSave;
    private readonly Button         _btnCancel;

    public List<AlarmInfo>? Result { get; private set; }

    public AlarmsForm(DeviceInfo info)
    {
        _info = info;

        Text            = $"Alarms — {info.Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ClientSize      = new Size(620, 240);

        // ── Header ────────────────────────────────────────────────────────
        var header = new Panel { Dock = DockStyle.Top, Height = 30, Padding = new Padding(8, 6, 0, 0) };
        header.Controls.Add(Label("#",    40,  6, FontStyle.Bold));
        header.Controls.Add(Label("Action", 80, 6, FontStyle.Bold));
        header.Controls.Add(Label("Hours",  140, 6, FontStyle.Bold));
        header.Controls.Add(Label("Name",   210, 6, FontStyle.Bold));
        header.Controls.Add(Label("Status", 460, 6, FontStyle.Bold));

        // ── Alarm rows ────────────────────────────────────────────────────
        var rowPanel = new Panel { Dock = DockStyle.Top, Height = 160, Padding = new Padding(8, 0, 8, 0) };

        var alarms = info.Alarms ?? new List<AlarmInfo>();
        while (alarms.Count < 4) alarms.Add(new AlarmInfo());

        for (int i = 0; i < 4; i++)
        {
            _rows[i] = new AlarmRow(i, alarms[i], (int)info.CounterS);
            _rows[i].Location = new Point(8, i * 38 + 4);
            rowPanel.Controls.Add(_rows[i]);
        }

        // ── Buttons ───────────────────────────────────────────────────────
        var btnPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            Height        = 44,
            FlowDirection = FlowDirection.RightToLeft,
            Padding       = new Padding(8, 6, 8, 0),
        };

        _btnSave   = new Button { Text = "Save",   Width = 80, Height = 28, DialogResult = DialogResult.None };
        _btnCancel = new Button { Text = "Cancel", Width = 80, Height = 28, DialogResult = DialogResult.Cancel };
        _btnSave.Click   += OnSave;
        CancelButton      = _btnCancel;

        btnPanel.Controls.Add(_btnCancel);
        btnPanel.Controls.Add(_btnSave);

        Controls.Add(btnPanel);
        Controls.Add(rowPanel);
        Controls.Add(header);
    }

    private void OnSave(object? s, EventArgs e)
    {
        Result = _rows.Select(r => r.GetAlarm()).ToList();
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label Label(string text, int x, int y, FontStyle style = FontStyle.Regular)
    {
        return new Label
        {
            Text      = text,
            Location  = new Point(x, y),
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9, style),
        };
    }
}

// ── AlarmRow ─────────────────────────────────────────────────────────────────

internal class AlarmRow : Panel
{
    private readonly int    _index;
    private AlarmInfo       _alarm;
    private readonly int    _counterS;
    private bool            _previewMode;

    private readonly Button  _btn;
    private readonly TextBox _tbHours;
    private readonly TextBox _tbName;
    private readonly Label   _lblStatus;

    private System.Windows.Forms.Timer? _holdTimer;

    internal AlarmRow(int index, AlarmInfo alarm, int counterS)
    {
        _index    = index;
        _alarm    = alarm.Clone();
        _counterS = counterS;

        Height = 34;
        Width  = 600;

        // Index
        Controls.Add(MkLabel((_index + 1).ToString(), 0, 8, 36));

        // Action button
        _btn = new Button { Width = 72, Height = 26, Location = new Point(40, 4), FlatStyle = FlatStyle.System };
        _btn.MouseDown += OnMouseDown;
        _btn.MouseUp   += OnMouseUp;
        Controls.Add(_btn);

        // Hours box (1–9999)
        _tbHours = new TextBox { Width = 54, Location = new Point(120, 5), MaxLength = 4 };
        _tbHours.KeyPress += OnHoursKeyPress;
        Controls.Add(_tbHours);

        // Name box (20 chars)
        _tbName = new TextBox { Width = 200, Location = new Point(182, 5), MaxLength = 20 };
        Controls.Add(_tbName);

        // Status
        _lblStatus = new Label { Location = new Point(392, 8), Width = 200, AutoSize = false };
        Controls.Add(_lblStatus);

        Refresh_();
    }

    // ── Keyboard validation ────────────────────────────────────────────────
    private void OnHoursKeyPress(object? s, KeyPressEventArgs e)
    {
        if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            e.Handled = true;
    }

    // ── Press-and-hold detection ───────────────────────────────────────────
    private void OnMouseDown(object? s, MouseEventArgs e)
    {
        if (_alarm.Fired) return;   // fired → handled on release as Reset
        _holdTimer = new System.Windows.Forms.Timer { Interval = 800 };
        _holdTimer.Tick += OnHoldFired;
        _holdTimer.Start();
    }

    private void OnMouseUp(object? s, MouseEventArgs e)
    {
        bool wasHoldTimer = _holdTimer?.Enabled ?? false;
        _holdTimer?.Stop();
        _holdTimer?.Dispose();
        _holdTimer = null;

        if (_alarm.Fired)
        {
            DoReset();
        }
        else if (_previewMode)
        {
            // Second press after hold → commit
            _previewMode = false;
            DoSet();
        }
        else if (wasHoldTimer)
        {
            // Short press, no hold → normal activate
            DoSet();
        }
        // if !wasHoldTimer && !_previewMode → hold already handled by OnHoldFired
    }

    private void OnHoldFired(object? s, EventArgs e)
    {
        _holdTimer?.Stop();
        _holdTimer?.Dispose();
        _holdTimer = null;

        // Enter preview mode: disable entries to show alarm-fired appearance
        _previewMode = true;
        _tbHours.Enabled = false;
        _tbName.Enabled  = false;
        _lblStatus.Text  = "(preview — press again to commit)";
        _lblStatus.ForeColor = Color.DarkOrange;

        // Auto-cancel preview after 4 seconds if user does nothing
        var cancel = new System.Windows.Forms.Timer { Interval = 4000 };
        cancel.Tick += (_, _) =>
        {
            cancel.Stop(); cancel.Dispose();
            CancelPreview();
        };
        cancel.Start();
    }

    private void CancelPreview()
    {
        _previewMode     = false;
        _tbHours.Enabled = true;
        _tbName.Enabled  = true;
        Refresh_();
    }

    // ── Alarm actions ──────────────────────────────────────────────────────
    private void DoSet()
    {
        if (!int.TryParse(_tbHours.Text.Trim(), out int hours) || hours < 1 || hours > 9999)
        {
            MessageBox.Show("Enter hours 1–9999.", "Invalid", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _alarm = new AlarmInfo
        {
            Name   = _tbName.Text.Trim()[..Math.Min(_tbName.Text.Trim().Length, 20)],
            Hours  = hours,
            BaseS  = _counterS,
            Active = true,
            Fired  = false,
        };
        Refresh_();
    }

    private void DoReset()
    {
        int.TryParse(_tbHours.Text.Trim(), out int hours);
        _alarm = new AlarmInfo
        {
            Name   = _tbName.Text.Trim()[..Math.Min(_tbName.Text.Trim().Length, 20)],
            Hours  = hours,
            BaseS  = _counterS,
            Active = hours > 0,
            Fired  = false,
        };
        Refresh_();
    }

    private void Refresh_()
    {
        bool fired  = _alarm.Fired;
        bool active = _alarm.Active;

        _btn.Text        = fired ? "Reset" : "Set";
        _tbHours.Enabled = !fired;
        _tbName.Enabled  = !fired;
        _tbHours.Text    = _alarm.Hours > 0 ? _alarm.Hours.ToString() : "";
        _tbName.Text     = _alarm.Name;

        if (fired)
        {
            _lblStatus.Text      = "!! ALARM !!";
            _lblStatus.ForeColor = Color.Red;
        }
        else if (active)
        {
            double targetH = (_alarm.BaseS + _alarm.Hours * 3600.0) / 3600.0;
            _lblStatus.Text      = $"fires @ {targetH:F1}h";
            _lblStatus.ForeColor = Color.DarkGreen;
        }
        else
        {
            _lblStatus.Text      = "inactive";
            _lblStatus.ForeColor = Color.Gray;
        }
    }

    internal AlarmInfo GetAlarm() => _alarm.Clone();

    // ── Helpers ────────────────────────────────────────────────────────────
    private static Label MkLabel(string text, int x, int y, int w)
    {
        return new Label
        {
            Text     = text,
            Location = new Point(x, y),
            Width    = w,
            AutoSize = false,
        };
    }
}
