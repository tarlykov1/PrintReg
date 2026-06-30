using System.Security.Cryptography;
using System.Text;

namespace GSPLabelPrinter.Utilities;

public static class TextHasher
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];
}
