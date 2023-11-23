using System.Collections.Generic;
using MusicDecrypto.Library;

namespace MusicDecrypto.Avalonia.ViewModels;

public class MatchViewModel(IEnumerable<DecryptoBase.MatchInfo> properties) : ViewModelBase
{
    public IEnumerable<DecryptoBase.MatchInfo> Properties { get; init; } = properties;
}
