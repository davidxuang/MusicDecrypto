using System.Text.Json;
using System.Text.RegularExpressions;

namespace MusicDecrypto.Library.Json;

internal sealed partial class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    [GeneratedRegex("[A-Z]")]
    private static partial Regex UppercaseRegex();

    private readonly Regex _regex = UppercaseRegex();

    public override string ConvertName(string name)
    {
        var camel = CamelCase.ConvertName(name);
        return _regex.Replace(camel, m => $"_{m.Value.ToLowerInvariant()}");
    }
}
