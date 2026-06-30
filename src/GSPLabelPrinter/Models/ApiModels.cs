using GSPLabelPrinter.Configuration;
namespace GSPLabelPrinter.Models;
public sealed record ApiError(bool Success, string Message, string ErrorCode);
public sealed record ApiSuccess<T>(bool Success, string Message, T? Data);
public sealed record AddEmployeeRequest(string? FullName, string? Position);
public sealed record PrintRequest(string? FullName, string? Position, int Copies);
public sealed record PrinterInfo(string Name, bool IsDefault, bool IsAvailable, string Status);
public sealed record SettingsDto(ServerSettings Server, DataSettings Data, PrintingSettings Printing);
