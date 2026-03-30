// SettingsForm.cs — client-side settings: AP password hash, UDP port

using System.Drawing;
using System.Windows.Forms;

namespace BoatTronClient;

public class SettingsForm : Form
{
    private readonly TextBox  _tbApPass;
    private readonly TextBox  _tbUdpPort;
    private readonly ClientSettings _settings;

    public SettingsForm(ClientSettings settings)
    {
        _settings = settings;

        Text            = "Client Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition   = FormStartPosition.CenterParent;
        MaximizeBox     = false;
        MinimizeBox     = false;
        ClientSize      = new Size(400, 180);

        _tbApPass  = new TextBox { PasswordChar = '*', Width = 220, MaxLength = 64, Location = new Point(140, 20) };
        _tbUdpPort = new TextBox { Width = 80, Text = settings.UdpPort.ToString(), Location = new Point(140, 60) };

        Controls.Add(MkLabel("AP password:",  12, 23));
        Controls.Add(MkLabel("UDP port:",     12, 63));
        Controls.Add(MkLabel("(leave blank to keep current)", 140, 44, 240, Color.Gray, 8));
        Controls.Add(_tbApPass);
        Controls.Add(_tbUdpPort);

        var btnSave      = new Button { Text = "Save",      Location = new Point(220, 110), Width = 80, Height = 28 };
        var btnCancel    = new Button { Text = "Cancel",    Location = new Point(308, 110), Width = 80, Height = 28, DialogResult = DialogResult.Cancel };
        var btnUninstall = new Button { Text = "Uninstall", Location = new Point(12,  110), Width = 90, Height = 28, ForeColor = System.Drawing.Color.DarkRed };
        btnSave.Click      += OnSave;
        btnUninstall.Click += OnUninstall;
        CancelButton        = btnCancel;
        Controls.Add(btnSave);
        Controls.Add(btnCancel);
        Controls.Add(btnUninstall);
    }

    private void OnSave(object? s, EventArgs e)
    {
        if (!int.TryParse(_tbUdpPort.Text.Trim(), out int port) || port < 1024 || port > 65535)
        {
            MessageBox.Show("UDP port must be 1024–65535.", "Invalid",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var newPass = _tbApPass.Text;
        if (!string.IsNullOrEmpty(newPass))
            _settings.ApPassHash = ClientSettings.HashPassword(newPass);

        _settings.UdpPort = port;
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

        // Locate uninstall.bat in the same directory as the running exe
        var exeDir     = AppContext.BaseDirectory;
        var batPath    = Path.Combine(exeDir, "uninstall.bat");
        var uninstExe  = Path.Combine(exeDir, "unins000.exe");   // Inno Setup default name

        if (File.Exists(batPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName  = batPath,
                UseShellExecute = true,   // opens in a visible console window
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

        // Exit the app — the uninstaller will kill us anyway
        Application.Exit();
    }

    private static Label MkLabel(string text, int x, int y,
                                  int width = 120, Color? color = null, float size = 9f)
    {
        return new Label
        {
            Text      = text,
            Location  = new Point(x, y),
            Width     = width,
            Font      = new Font("Segoe UI", size),
            ForeColor = color ?? SystemColors.ControlText,
            TextAlign = System.Drawing.ContentAlignment.MiddleRight,
        };
    }
}
