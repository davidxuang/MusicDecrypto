using System.IO;
using System.Text.RegularExpressions;

namespace MusicDecrypto.Library.Helpers;

internal static class IOHelpers
{
    private static readonly Regex _reBN = new(string.Format("[{0}]", Path.GetInvalidFileNameChars()), RegexOptions.Compiled);

    public static string SantizeBaseName(this string name)
    {
        return _reBN.Replace(name, "_");
    }
}
