using System.Text.RegularExpressions;

namespace GSPLabelPrinter.Utilities;

public static partial class TextNormalizer
{
    public static string NormalizeSpaces(string? value) => MultiSpace().Replace((value ?? string.Empty).Trim(), " ");
    public static string NormalizeKey(string? value) => NormalizeForSearch(value).ToUpperInvariant();
    public static string NormalizeForSearch(string? value) => NormalizeSpaces(value).Replace('ё', 'е').Replace('Ё', 'Е');

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultiSpace();
}
