using System.Diagnostics;

namespace GSPLabelPrinter.Utilities;

public static class AppEnvironment
{
    public const string AppRootVariable = "GSP_LABEL_PRINTER_APP_ROOT";
    public const string TestModeVariable = "GSP_LABEL_PRINTER_TEST_MODE";

    public static string GetApplicationRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(AppRootVariable);
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return EnsureTrailingSeparator(Path.GetFullPath(overrideRoot));
        }

        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            processPath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        var directory = !string.IsNullOrWhiteSpace(processPath)
            ? Path.GetDirectoryName(processPath)
            : AppContext.BaseDirectory;

        return EnsureTrailingSeparator(Path.GetFullPath(directory ?? AppContext.BaseDirectory));
    }

    public static bool IsTestMode() =>
        string.Equals(Environment.GetEnvironmentVariable(TestModeVariable), "true", StringComparison.OrdinalIgnoreCase);

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
