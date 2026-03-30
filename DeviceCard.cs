// DeviceCard.cs — UserControl representing one BoatTron device in the main list

using System.Drawing;
using System.Windows.Forms;

namespace BoatTronClient;

public class DeviceCard : Panel
{
    // ── Controls ───────────────────────────────────────────────────────────
    private readonly Label  _lblName;
    private readonly Label  _lblHours;
    private readonly Label  _lblDetail;
    private readonly Label  _lblAlarm;
    private readonly Button _btnUpdate;
    private readonly Button _btnConfigure;
    private readonly Button _btnAlarms;

    // ── State ──────────────────────────────────────────────────────────────
    public DeviceInfo Info { get; private set; }

    // ── Events ─────────────────────────────────────────────────────────────
    public event EventHandler<DeviceInfo>? ConfigureRequested;
    public event EventHandler<DeviceInfo>? AlarmsRequested;

    // ── Constructor ────────────────────────────────────────────────────────
    public DeviceCard(DeviceInfo info)
    {
        Info        = info;
        Height      = 64;
        BorderStyle = BorderStyle.FixedSingle;
        Margin      = new Padding(6, 4, 6, 4);
        Padding     = new Padding(8, 6, 8, 6);
        BackColor   = SystemColors.Window;
        Anchor      = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

        _lblName  = new Label
        {
            AutoSize  = true,
            Font      = new Font("Segoe UI", 10, FontStyle.Bold),
            Location  = new Point(8, 6),
        };

        _lblHours = new Label
        {
            AutoSize  = true,
            Font      = new Font("Courier New", 20, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Color.DarkSlateBlue,
        };

        _lblDetail = new Label
        {
            AutoSize  = true,
            Font      = new Font("Segoe UI", 8),
            ForeColor = Color.Gray,
            Location  = new Point(8, 30),
        };

        _lblAlarm = new Label
        {
            AutoSize  = true,
            Font      = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.Red,
            Location  = new Point(8, 30),  // re-positioned in UpdateDisplay
        };

        _btnUpdate    = MakeButton("Update",    80, async (_, _) => await DoUpdateAsync());
        _btnConfigure = MakeButton("Configure", 86, (_, _) => ConfigureRequested?.Invoke(this, Info));
        _btnAlarms    = MakeButton("Alarms",    72, (_, _) => AlarmsRequested?.Invoke(this, Info));

        Controls.AddRange(new Control[]
        {
            _lblName, _lblHours, _lblDetail, _lblAlarm,
            _btnAlarms, _btnConfigure, _btnUpdate,
        });

        SizeChanged += (_, _) => PositionRightControls();
        UpdateDisplay();
        PositionRightControls();
    }

    // ── Layout ─────────────────────────────────────────────────────────────
    private void PositionRightControls()
    {
        int r = Width - 8;
        _btnUpdate.Location    = new Point(r - _btnUpdate.Width,    17);
        _btnConfigure.Location = new Point(r - _btnUpdate.Width - _btnConfigure.Width - 4, 17);
        _btnAlarms.Location    = new Point(r - _btnUpdate.Width - _btnConfigure.Width - _btnAlarms.Width - 8, 17);
        _lblHours.Location     = new Point(_btnAlarms.Left - _lblHours.Width - 16, 4);
    }

    // ── Public update ──────────────────────────────────────────────────────
    public void UpdateDisplay(DeviceInfo? fresh = null)
    {
        if (fresh != null) Info = fresh;

        _lblName.Text   = Info.Name;
        _lblHours.Text  = Info.HoursDisplay;
        _lblDetail.Text = $"{Info.Serial}   {Info.IP}";

        if (Info.HasFiredAlarm)
        {
            _lblAlarm.Text     = "  !! ALARM !!";
            _lblAlarm.Location = new Point(_lblDetail.Right, 30);
            BackColor          = Color.MistyRose;
        }
        else
        {
            _lblAlarm.Text = "";
            BackColor      = SystemColors.Window;
        }

        // Reposition hours (width may have changed)
        PositionRightControls();
    }

    // ── Update button ──────────────────────────────────────────────────────
    private async Task DoUpdateAsync()
    {
        if (string.IsNullOrEmpty(Info.IP))
        {
            MessageBox.Show("Device IP not known — run Scan first.",
                            "Offline", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _btnUpdate.Enabled = false;
        try
        {
            var fresh = await DeviceClient.GetInfoAsync(Info.IP);
            UpdateDisplay(fresh);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Update failed",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally { _btnUpdate.Enabled = true; }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static Button MakeButton(string text, int width, EventHandler handler)
    {
        var b = new Button
        {
            Text      = text,
            Width     = width,
            Height    = 28,
            FlatStyle = FlatStyle.System,
        };
        b.Click += handler;
        return b;
    }
}
