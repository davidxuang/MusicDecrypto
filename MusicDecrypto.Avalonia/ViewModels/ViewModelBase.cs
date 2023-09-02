using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MusicDecrypto.Avalonia.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool RaiseAndSetIfChanged<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
        return false;
    }

    protected void RaisePropertyChanged(string propName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
    }
}
