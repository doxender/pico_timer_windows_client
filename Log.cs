// Log.cs — simple append-to-file logger
//
// Log file: %APPDATA%\DigiTronSensors\BoatTronClient\boatron.log
// Rolls over at 1 MB to keep the file manageable.

using System.Text;

namespace BoatTronClient;

public static class Log
{
    private static readonly string _path;
    private static readonly object _lock = new();

    static Log()
    {
        var dir  = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DigiTronSensors", "BoatTronClient");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "boatron.log");
    }

    public static void Info(string msg)  => Write("INFO ", msg);
    public static void Warn(string msg)  => Write("WARN ", msg);
    public static void Error(string msg) => Write("ERROR", msg);
    public static void Error(string msg, Exception ex) => Write("ERROR", $"{msg}: {ex}");

    private static void Write(string level, string msg)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {msg}{Environment.NewLine}";
        lock (_lock)
        {
            try
            {
                // Roll over at 1 MB
                if (File.Exists(_path) && new FileInfo(_path).Length > 1_048_576)
                {
                    var archive = _path.Replace(".log", $"-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                    File.Move(_path, archive);
                }
                File.AppendAllText(_path, line, Encoding.UTF8);
            }
            catch { /* never let logging crash the app */ }
        }
    }

    public static string FilePath => _path;
}
