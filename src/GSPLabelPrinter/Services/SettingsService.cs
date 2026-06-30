using System.Text.Json;
using GSPLabelPrinter.Configuration;
namespace GSPLabelPrinter.Services;
public sealed class SettingsService
{
    private readonly string _path; private readonly FileLogger _log; private readonly object _sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public SettingsService(string root, FileLogger log) { _path = Path.Combine(root, "config.json"); _log = log; Current = Load(); }
    public AppSettings Current { get; private set; }
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path)) { var d = new AppSettings(); Save(d); return d; }
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            _log.Error("Повреждена конфигурация, восстанавливаются настройки по умолчанию.", ex);
            if (File.Exists(_path)) File.Copy(_path, _path + $".broken_{DateTime.Now:yyyy-MM-dd_HHmmss}", true);
            var d = new AppSettings(); Save(d); return d;
        }
    }
    public void Save(AppSettings settings)
    {
        lock (_sync)
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
            Current = settings;
        }
    }
}
