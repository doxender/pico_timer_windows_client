// SettingsForm.cs — client-side settings: AP hotspot password
// Access: right-click the main window → Settings

using System.Drawing;
using System.Windows.Forms;

namespace BoatTronClient;

public class SettingsForm : Form
{
    private readonly TextBox         _tbApPass;
    private readonly ClientSettings  _settings;

    public SettingsForm(ClientSettings settings)
    {
        _settings = settings;

        Text            = "Client Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ClientSize      = new Size(420, 170);

        // ── Explanation label ──────────────────────────────────────────────
        Controls.Add(new Label
        {
            Text      = "The AP hotspot password is used when this app connects\n" +
                        "to a BoatTron device's WiFi access point for setup.",
            Location  = new Point(12, 14),
            Size      = new Size(396, 38),
            Font      = new Font("Segoe UI", 8.5f),
            ForeColor = Color.Gray,
        });

        // ── Password row ───────────────────────────────────────────────────
        Controls.Add(MkLabel("Hotspot password:", 12, 66));
        _tbApPass = new TextBox
        {
            PasswordChar = '*',
            Width        = 220,
            MaxLength    = 64,
            Location     = new Point(150, 63),
        };
        Controls.Add(_tbApPass);
        Controls.Add(new Label
        {
            Text      = "(leave blank to keep current)",
            Location  = new Point(150, 86),
            Width     = 240,
            ForeColor = Color.Gray,
            Font      = new Font("Segoe UI", 8),
        });

        // ── Buttons ────────────────────────────────────────────────────────
        var btnSave   = new Button { Text = "Save",        Location = new Point(230, 126), Width = 80, Height = 28 };
        var btnCancel = new Button { Text = "Cancel",      Location = new Point(318, 126), Width = 80, Height = 28, DialogResult = DialogResult.Cancel };
        var btnUninstall = new Button
        {
            Text      = "Uninstall…",
            Location  = new Point(12, 126),
            Width     = 90,
            Height    = 28,
            ForeColor = Color.DarkRed,
        };

        btnSave.Click      += OnSave;
        btnUninstall.Click += OnUninstall;
        CancelButton        = btnCancel;
        Controls.Add(btnSave);
        Controls.Add(btnCancel);
        Controls.Add(btnUninstall);
    }

    private void OnSave(object? s, EventArgs e)
    {
        var newPass = _tbApPass.Text;
        if (!string.IsNullOrEmpty(newPass))
            _settings.ApPassHash = ClientSettings.HashPassword(newPass);

        _settings.Save();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnUninstall(object? s, EventArgs e)
    {
        var result = MessageBox.Show(
            "This will close BoatTron Monitor and run the uninstaller.\n\n" +
            "All app files and registry entries will be removed.\n" +
            "Your settings will also be deleted.\n\n" +
            "Continue?",
            "Uninstall BoatTron Monitor",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (result != DialogResult.Yes) return;

        var exeDir    = AppContext.BaseDirectory;
        var batPath   = Path.Combine(exeDir, "uninstall.bat");
        var uninstExe = Path.Combine(exeDir, "unins000.exe");

        if (File.Exists(batPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName        = batPath,
                UseShellExecute = true,
            });
        }
        else if (File.Exists(uninstExe))
        {
            System.Diagnostics.Process.Start(uninstExe);
        }
        else
        {
            MessageBox.Show("Uninstaller not found. Use Add/Remove Programs.",
                            "Not found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.Exit();
    }

    private static Label MkLabel(string text, int x, int y)
    {
        return new Label
        {
            Text      = text,
            Location  = new Point(x, y + 3),
            Width     = 134,
            Font      = new Font("Segoe UI", 9),
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
        };
    }
}
