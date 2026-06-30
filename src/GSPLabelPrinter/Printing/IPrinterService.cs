using GSPLabelPrinter.Configuration;
using GSPLabelPrinter.Models;
namespace GSPLabelPrinter.Printing;
public interface IPrinterService
{
    IReadOnlyList<PrinterInfo> GetPrinters(string selectedPrinter);
    Task<(bool ok,string message,string code)> PrintAsync(Employee employee, int copies, PrintingSettings settings, CancellationToken ct = default);
}
