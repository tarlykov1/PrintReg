namespace GSPLabelPrinter.Services;

public sealed class CsvBackupService
{
    private readonly FileLogger _log;

    public CsvBackupService(FileLogger log) => _log = log;

    public string CreateBackup(string csvPath, string backupDir, int maxBackups)
    {
        if (!File.Exists(csvPath))
        {
            throw new FileNotFoundException("Основной CSV отсутствует, резервная копия не создана.", csvPath);
        }

        Directory.CreateDirectory(backupDir);
        string dest;
        do
        {
            dest = Path.Combine(backupDir, $"employees_{DateTime.Now:yyyy-MM-dd_HHmmss_fffffff}.csv");
        } while (File.Exists(dest));

        File.Copy(csvPath, dest, false);
        _log.Info($"Создана резервная копия CSV: {dest}");
        Cleanup(backupDir, maxBackups);
        return dest;
    }

    public void Cleanup(string backupDir, int maxBackups)
    {
        if (!Directory.Exists(backupDir)) return;
        if (maxBackups < 0) maxBackups = 0;

        foreach (var file in new DirectoryInfo(backupDir)
            .GetFiles("employees_*.csv")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Skip(maxBackups))
        {
            file.Delete();
        }
    }
}
