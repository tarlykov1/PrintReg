using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using GSPLabelPrinter.Models;
using GSPLabelPrinter.Utilities;

namespace GSPLabelPrinter.Services;

public sealed class EmployeeCsvService
{
    private readonly CsvBackupService _backup;
    private readonly FileLogger _log;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public EmployeeCsvService(CsvBackupService backup, FileLogger log)
    {
        _backup = backup;
        _log = log;
    }

    private static CsvConfiguration Config => new(CultureInfo.InvariantCulture)
    {
        Delimiter = ";",
        HasHeaderRecord = true,
        IgnoreBlankLines = true,
        TrimOptions = TrimOptions.Trim,
        BadDataFound = null,
        MissingFieldFound = null,
        HeaderValidated = null,
        PrepareHeaderForMatch = args => TextNormalizer.NormalizeKey(args.Header)
    };

    public void EnsureDemoCsv(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path))
        {
            WriteAll(path, new[]
            {
                new Employee { FullName = "Иванов Иван Иванович", Position = "Главный специалист" },
                new Employee { FullName = "Петрова Анна Сергеевна", Position = "Начальник отдела" }
            });
        }
    }

    public List<Employee> ReadAll(string path)
    {
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length == 0) return [];

        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8, true);
            using var csv = new CsvReader(reader, Config);
            csv.Context.RegisterClassMap<EmployeeMap>();
            var rows = csv.GetRecords<Employee>()
                .Select(e => new Employee
                {
                    FullName = TextNormalizer.NormalizeSpaces(e.FullName),
                    Position = TextNormalizer.NormalizeSpaces(e.Position)
                })
                .Where(e => e.FullName.Length > 0 || e.Position.Length > 0)
                .ToList();
            _log.Info($"Загружено сотрудников: {rows.Count}");
            return rows;
        }
        catch (Exception ex) when (ex is CsvHelperException or IOException or DecoderFallbackException)
        {
            _log.Error("Ошибка чтения CSV", ex);
            throw new InvalidDataException("Не удалось прочитать employees.csv. Проверьте структуру CSV-файла.", ex);
        }
    }

    public async Task<(bool ok, string message, string code, Employee? employee, Employee? duplicate)> AddAsync(string path, string backupDir, int maxBackups, Employee employee)
    {
        employee = new Employee
        {
            FullName = TextNormalizer.NormalizeSpaces(employee.FullName),
            Position = TextNormalizer.NormalizeSpaces(employee.Position)
        };

        if (employee.FullName.Length == 0) return (false, "Заполните ФИО.", "VALIDATION_ERROR", null, null);
        if (employee.Position.Length == 0) return (false, "Заполните должность.", "VALIDATION_ERROR", null, null);
        if (employee.FullName.Length > 200 || employee.Position.Length > 200) return (false, "Значения не должны превышать 200 символов.", "VALIDATION_ERROR", null, null);

        await _writeLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (!File.Exists(path)) EnsureDemoCsv(path);

            var all = ReadAll(path);
            var duplicate = all.FirstOrDefault(e => TextNormalizer.NormalizeKey(e.FullName) == TextNormalizer.NormalizeKey(employee.FullName));
            if (duplicate is not null) return (false, "Сотрудник с таким ФИО уже есть в CSV.", "DUPLICATE_EMPLOYEE", null, duplicate);

            _backup.CreateBackup(path, backupDir, maxBackups);
            all.Add(employee);

            var tempPath = Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path) + $".{Guid.NewGuid():N}.tmp");
            WriteAll(tempPath, all);
            _ = ReadAll(tempPath);

            ReplaceCsv(tempPath, path);
            _log.Info($"Добавлен сотрудник, хэш ФИО: {TextHasher.Hash(employee.FullName)}");
            return (true, "Сотрудник сохранён.", "OK", employee, null);
        }
        catch (IOException ex)
        {
            _log.Error("Ошибка записи CSV", ex);
            return (false, "Не удалось сохранить сотрудника. Возможно, файл employees.csv открыт в Excel. Закройте файл и повторите попытку.", "CSV_FILE_LOCKED", null, null);
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.Error("Нет прав на запись CSV", ex);
            return (false, "Не удалось сохранить сотрудника. Нет прав на запись в папку приложения.", "CSV_ACCESS_DENIED", null, null);
        }

        finally
        {
            _writeLock.Release();
        }
    }

    private static void ReplaceCsv(string tempPath, string path)
    {
        if (File.Exists(path))
        {
            try
            {
                File.Replace(tempPath, path, null, true);
                return;
            }
            catch (PlatformNotSupportedException)
            {
                File.Move(tempPath, path, true);
                return;
            }
        }

        File.Move(tempPath, path, false);
    }

    private static void WriteAll(string path, IEnumerable<Employee> employees)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        using var csv = new CsvWriter(writer, Config);
        csv.Context.RegisterClassMap<EmployeeMap>();
        csv.WriteRecords(employees);
    }

    private sealed class EmployeeMap : CsvHelper.Configuration.ClassMap<Employee>
    {
        public EmployeeMap()
        {
            Map(m => m.FullName).Name("ФИО", "FullName", "fullName");
            Map(m => m.Position).Name("Должность", "Position", "position");
        }
    }
}
