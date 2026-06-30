namespace GSPLabelPrinter.Configuration;

public sealed class AppSettings
{
    public ServerSettings Server { get; set; } = new();
    public DataSettings Data { get; set; } = new();
    public PrintingSettings Printing { get; set; } = new();
}
public sealed class ServerSettings { public int Port { get; set; } = 5187; public bool OpenBrowserOnStart { get; set; } = true; }
public sealed class DataSettings { public string CsvPath { get; set; } = "data/employees.csv"; public string BackupDirectory { get; set; } = "backup"; public int MaximumBackups { get; set; } = 50; }
public sealed class PrintingSettings
{
    public string PrinterName { get; set; } = string.Empty;
    public int LabelWidthMm { get; set; } = 40;
    public int LabelHeightMm { get; set; } = 60;
    public string Orientation { get; set; } = "Portrait";
    public int Copies { get; set; } = 1;
    public bool ShowPrintDialog { get; set; }
    public float FullNameFontSize { get; set; } = 15;
    public float PositionFontSize { get; set; } = 9;
    public float MarginMm { get; set; } = 3;
}
