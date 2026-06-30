using System.Drawing.Printing;
using GSPLabelPrinter.Configuration;
using GSPLabelPrinter.Models;
using GSPLabelPrinter.Services;
using GSPLabelPrinter.Utilities;

namespace GSPLabelPrinter.Printing;

public sealed class WindowsPrinterService : IPrinterService
{
    private readonly LabelLayoutService _layout;
    private readonly FileLogger _log;
    private readonly object _printLock = new();

    public WindowsPrinterService(LabelLayoutService layout, FileLogger log)
    {
        _layout = layout;
        _log = log;
    }

    public IReadOnlyList<PrinterInfo> GetPrinters(string selectedPrinter)
    {
        var list = new List<PrinterInfo>();
        var defaultPrinter = new PrinterSettings().PrinterName;
        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            var printerSettings = new PrinterSettings { PrinterName = name };
            list.Add(new PrinterInfo(name, string.Equals(name, defaultPrinter, StringComparison.OrdinalIgnoreCase), printerSettings.IsValid, printerSettings.IsValid ? "Доступен" : "Недоступен"));
        }
        return list;
    }

    public Task<(bool ok, string message, string code)> PrintAsync(Employee employee, int copies, PrintingSettings settings, CancellationToken ct = default)
    {
        lock (_printLock)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(settings.PrinterName)) return Task.FromResult((false, "Выберите принтер в настройках.", "PRINTER_NOT_SELECTED"));
                if (!PrinterSettings.InstalledPrinters.Cast<string>().Any(p => string.Equals(p, settings.PrinterName, StringComparison.Ordinal))) return Task.FromResult((false, "Выбранный принтер не найден или недоступен.", "PRINTER_NOT_FOUND"));

                var printerSettings = new PrinterSettings
                {
                    PrinterName = settings.PrinterName
                };
                if (!printerSettings.IsValid) return Task.FromResult((false, "Выбранный принтер не найден или недоступен.", "PRINTER_NOT_FOUND"));

                using var document = new PrintDocument
                {
                    PrinterSettings = printerSettings
                };

                var width = MmToHundredthsInch(settings.LabelWidthMm);
                var height = MmToHundredthsInch(settings.LabelHeightMm);
                document.PrinterSettings.Copies = (short)Math.Clamp(copies, 1, 10);
                document.DefaultPageSettings.Landscape = settings.Orientation.Equals("Landscape", StringComparison.OrdinalIgnoreCase);
                document.DefaultPageSettings.PaperSize = new PaperSize("GSPLabel 40x60mm", width, height);
                var margin = MmToHundredthsInch(settings.MarginMm);
                document.DefaultPageSettings.Margins = new Margins(margin, margin, margin, margin);

                if (Math.Abs(document.DefaultPageSettings.PaperSize.Width - width) > 2 || Math.Abs(document.DefaultPageSettings.PaperSize.Height - height) > 2)
                {
                    return Task.FromResult((false, "Драйвер принтера не принял размер наклейки 40 × 60 мм. Создайте этот размер бумаги в настройках драйвера принтера и повторите печать.", "CUSTOM_PAPER_SIZE_NOT_SUPPORTED"));
                }

                PrintPageEventHandler printPageHandler = (_, e) =>
                {
                    var graphics = e.Graphics
                        ?? throw new InvalidOperationException("Не удалось получить графический контекст принтера.");

                    _log.Info($"PrintPage: printer={settings.PrinterName}; paper={width}x{height}; orientation={settings.Orientation}; copies={document.PrinterSettings.Copies}; bounds={e.MarginBounds}; employeeHash={TextHasher.Hash(employee.FullName)}");
                    _layout.Draw(graphics, e.MarginBounds, employee, settings);
                    e.HasMorePages = false;
                };

                document.PrintPage += printPageHandler;

                _log.Info($"Попытка печати: printer={settings.PrinterName}; paper={width}x{height}; orientation={settings.Orientation}; copies={document.PrinterSettings.Copies}; employeeHash={TextHasher.Hash(employee.FullName)}");
                try
                {
                    document.Print();
                }
                finally
                {
                    document.PrintPage -= printPageHandler;
                }
                _log.Info("Наклейка отправлена на печать");
                return Task.FromResult((true, "Наклейка отправлена на печать", "OK"));
            }
            catch (InvalidPrinterException ex)
            {
                _log.Error("Принтер недоступен", ex);
                return Task.FromResult((false, "Выбранный принтер не найден или недоступен.", "PRINTER_NOT_FOUND"));
            }
            catch (Exception ex)
            {
                _log.Error("Ошибка печати", ex);
                return Task.FromResult((false, "Не удалось отправить наклейку на печать. Проверьте принтер и очередь печати.", "PRINT_ERROR"));
            }
        }
    }

    public static int MmToHundredthsInch(float millimeters) => (int)Math.Round(millimeters / 25.4f * 100f);
}
