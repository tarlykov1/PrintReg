namespace GSPLabelPrinter.Services;
public sealed class FileLogger
{
    private readonly string _dir; private readonly object _sync = new();
    public FileLogger(string root) { _dir = Path.Combine(root, "logs"); Directory.CreateDirectory(_dir); }
    public void Info(string message) => Write("INFO", message, null);
    public void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);
    private void Write(string level, string message, Exception? ex)
    {
        lock (_sync)
        {
            var file = Path.Combine(_dir, $"app-{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{(ex is null ? "" : Environment.NewLine + ex)}{Environment.NewLine}");
        }
    }
}
