using System.Collections.Generic;
using MusicDecrypto.Library;

namespace MusicDecrypto.Avalonia.ViewModels;

public class MatchViewModel : ViewModelBase
{
    public MatchViewModel(IEnumerable<DecryptoBase.MatchInfo> properties)
    {
        Properties = properties;
    }

    public IEnumerable<DecryptoBase.MatchInfo> Properties { get; init; }
}
