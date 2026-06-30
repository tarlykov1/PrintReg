using System.Diagnostics;
using System.Net;
using System.Windows.Forms;
using GSPLabelPrinter.Configuration;
using GSPLabelPrinter.Models;
using GSPLabelPrinter.Printing;
using GSPLabelPrinter.Services;
using GSPLabelPrinter.Utilities;

var root = AppEnvironment.GetApplicationRoot();
var logger = new FileLogger(root);
var settingsBootstrap = new SettingsService(root, logger).Current;
var url = $"http://127.0.0.1:{settingsBootstrap.Server.Port}";
var pathsBootstrap = new AppPaths(root);

using var appLock = new ApplicationLockService(pathsBootstrap);
if (!appLock.TryAcquire())
{
    OpenBrowser(url);
    return;
}

try
{
    LogCleanupService.Cleanup(root);
    logger.Info("Запуск приложения");

    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        ContentRootPath = root,
        WebRootPath = Path.Combine(root, "wwwroot")
    });
    builder.WebHost.UseUrls(url);

    builder.Services.AddSingleton(pathsBootstrap);
    builder.Services.AddSingleton(logger);
    builder.Services.AddSingleton(sp => new SettingsService(root, sp.GetRequiredService<FileLogger>()));
    builder.Services.AddSingleton<CsvBackupService>();
    builder.Services.AddSingleton<EmployeeCsvService>();
    builder.Services.AddSingleton<EmployeeSearchService>();
    builder.Services.AddSingleton<LabelLayoutService>();
    builder.Services.AddSingleton<IPrinterService, WindowsPrinterService>();

    var app = builder.Build();
    ConfigureApplication(app, root, url, logger);
    app.Run();
}
catch (IOException ex) when (ex.Message.Contains("address", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("port", StringComparison.OrdinalIgnoreCase))
{
    logger.Error("Не удалось запустить сервер. Возможно, порт 5187 занят.", ex);
    ShowStartupError("Не удалось запустить GSPLabelPrinter. Возможно, порт 5187 уже занят другим приложением.");
}
catch (Exception ex)
{
    logger.Error("Необработанное исключение", ex);
    ShowStartupError("GSPLabelPrinter завершился с ошибкой. Подробности записаны в папку logs.");
    throw;
}

static void ConfigureApplication(WebApplication app, string root, string url, FileLogger logger)
{
    var settings = app.Services.GetRequiredService<SettingsService>();
    var paths = app.Services.GetRequiredService<AppPaths>();
    var csv = app.Services.GetRequiredService<EmployeeCsvService>();
    var csvPath = paths.ResolveInsideRoot(settings.Current.Data.CsvPath);
    var backupDir = paths.ResolveInsideRoot(settings.Current.Data.BackupDirectory);

    Directory.CreateDirectory(backupDir);
    Directory.CreateDirectory(Path.Combine(root, "logs"));
    csv.EnsureDemoCsv(csvPath);
    logger.Info($"Путь к CSV: {csvPath}");

    app.Use(async (context, next) =>
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is not null && !IPAddress.IsLoopback(remote))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new ApiError(false, "Приложение доступно только локально.", "LOCAL_ONLY"));
            return;
        }
        await next();
    });

    app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new ApiError(false, "Некорректный JSON или запрос.", "BAD_REQUEST"));
    }));

    app.UseDefaultFiles();
    app.UseStaticFiles();

    static IResult Err(int code, string message, string errorCode) => Results.Json(new ApiError(false, message, errorCode), statusCode: code);
    static IResult Ok<T>(string message, T? data) => Results.Ok(new ApiSuccess<T>(true, message, data));

    app.MapGet("/api/health", (SettingsService s, AppPaths p, EmployeeCsvService c) => Results.Ok(new
    {
        success = true,
        csvPath = s.Current.Data.CsvPath,
        employees = c.ReadAll(p.ResolveInsideRoot(s.Current.Data.CsvPath)).Count,
        printer = s.Current.Printing.PrinterName
    }));

    app.MapGet("/api/employees", (SettingsService s, AppPaths p, EmployeeCsvService c) => Results.Ok(c.ReadAll(p.ResolveInsideRoot(s.Current.Data.CsvPath))));
    app.MapGet("/api/employees/search", (string? q, SettingsService s, AppPaths p, EmployeeCsvService c, EmployeeSearchService search) => Results.Ok(search.Search(c.ReadAll(p.ResolveInsideRoot(s.Current.Data.CsvPath)), q)));

    app.MapPost("/api/employees", async (AddEmployeeRequest request, SettingsService s, AppPaths p, EmployeeCsvService c) =>
    {
        var data = s.Current.Data;
        var result = await c.AddAsync(p.ResolveInsideRoot(data.CsvPath), p.ResolveInsideRoot(data.BackupDirectory), data.MaximumBackups, new Employee { FullName = request.FullName ?? string.Empty, Position = request.Position ?? string.Empty });
        return result.ok
            ? Ok("Сотрудник сохранён.", result.employee)
            : Results.Json(new { success = false, message = result.message, errorCode = result.code, duplicate = result.duplicate }, statusCode: result.code == "DUPLICATE_EMPLOYEE" ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest);
    });

    app.MapGet("/api/printers", (SettingsService s, IPrinterService printer) => Results.Ok(printer.GetPrinters(s.Current.Printing.PrinterName)));
    app.MapGet("/api/settings", (SettingsService s) => Results.Ok(s.Current));

    app.MapPut("/api/settings", (AppSettings incoming, SettingsService s, FileLogger log) =>
    {
        incoming = AppSettingsValidator.SanitizeForLocalApplication(incoming);
        var validation = AppSettingsValidator.Validate(incoming);
        if (!validation.Ok) return Err(StatusCodes.Status400BadRequest, validation.Message, validation.Code);
        s.Save(incoming);
        log.Info("Изменены настройки");
        return Ok("Настройки сохранены.", incoming);
    });

    app.MapPost("/api/print", async (PrintRequest request, SettingsService s, IPrinterService printer) => await Print(request, s, printer));
    app.MapPost("/api/print/test", async (SettingsService s, IPrinterService printer) => await Print(new PrintRequest("Тестовая наклейка", "Проверка печати 40 × 60 мм", s.Current.Printing.Copies), s, printer));

    if (AppEnvironment.IsTestMode())
    {
        app.MapPost("/__test/shutdown", (IHostApplicationLifetime lifetime) =>
        {
            lifetime.StopApplication();
            return Results.Ok(new { success = true });
        });
    }

    app.Lifetime.ApplicationStopping.Register(() => logger.Info("Завершение приложения"));
    if (settings.Current.Server.OpenBrowserOnStart && !AppEnvironment.IsTestMode()) app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(url));

    static async Task<IResult> Print(PrintRequest request, SettingsService settings, IPrinterService printer)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) return Err(StatusCodes.Status400BadRequest, "Заполните ФИО.", "VALIDATION_ERROR");
        if (string.IsNullOrWhiteSpace(request.Position)) return Err(StatusCodes.Status400BadRequest, "Заполните должность.", "VALIDATION_ERROR");
        if (request.FullName.Length > 200 || request.Position.Length > 200) return Err(StatusCodes.Status400BadRequest, "Значения не должны превышать 200 символов.", "VALIDATION_ERROR");
        if (request.Copies < 1 || request.Copies > 10) return Err(StatusCodes.Status400BadRequest, "Количество копий должно быть от 1 до 10.", "VALIDATION_ERROR");
        var result = await printer.PrintAsync(new Employee { FullName = request.FullName, Position = request.Position }, request.Copies, settings.Current.Printing);
        return result.ok ? Ok<object>(result.message, null) : Err(StatusCodes.Status400BadRequest, result.message, result.code);
    }
}

static void OpenBrowser(string url)
{
    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
    catch (Exception ex) { Debug.WriteLine(ex); }
}

static void ShowStartupError(string message)
{
    try { MessageBox.Show(message, "GSPLabelPrinter", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    catch (Exception ex) { Debug.WriteLine(ex); }
}

public partial class Program { }
