using System.Collections.Generic;
using System.Collections.ObjectModel;
using MusicDecrypto.Library;

namespace MusicDecrypto.Avalonia.ViewModels;

public class MatchViewModel : ViewModelBase
{
    public MatchViewModel(DecryptoBase.MatchInfo local, DecryptoBase.MatchInfo remote)
    {
        Items = new ObservableCollection<KeyValuePair<string, DecryptoBase.MatchInfo>>
        {
            KeyValuePair.Create("Local", local),
            KeyValuePair.Create("Remote", remote),
        };
    }

    public IEnumerable<KeyValuePair<string, DecryptoBase.MatchInfo>> Items { get; init; }
}
