namespace GSPLabelPrinter.Services;
public sealed class LogCleanupService
{
    public static void Cleanup(string root) { var d = Path.Combine(root,"logs"); if (!Directory.Exists(d)) return; foreach (var f in Directory.GetFiles(d,"app-*.log")) if (File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-30)) File.Delete(f); }
}
