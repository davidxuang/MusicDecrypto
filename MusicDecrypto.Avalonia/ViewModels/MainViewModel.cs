using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ByteSizeLib;
using FluentAvalonia.UI.Controls;
using MusicDecrypto.Avalonia.Controls;
using MusicDecrypto.Library;

namespace MusicDecrypto.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [GeneratedRegex("^(?:.+ - |\\d+\\. )?(.+) - (.+)$")]
    private static partial Regex NameRegex();
    private static readonly Regex _regex = NameRegex();
    private static readonly SemaphoreSlim _dialogLock = new(1);

    public ObservableCollection<Item> Items { get; private set; } = new();

    public bool IsEmpty => Items.Count == 0;

    public MainViewModel()
    {
        Items.CollectionChanged += (s, e) => RaisePropertyChanged(nameof(IsEmpty));
    }

    public void AddFile(IStorageFile file)
    {
        if (file.CanBookmark && DecryptoFactory.KnownExtensions.Contains(Path.GetExtension(file.Name)))
        {
            var item = new Item(file);
            Items.Add(item);
            Task.Run(async () => await DecryptFileAsync(item));
        }
    }

    public static async ValueTask DecryptFileAsync(Item item)
    {
        try
        {
            using var buffer = new MarshalMemoryStream();

            var match = _regex.Match(Path.GetFileNameWithoutExtension(item.File.Name));
            if (match.Success)
            {
                item.Performers = match.Groups[1].Value;
                item.Title = match.Groups[2].Value;
            }
            else item.Title = Path.GetFileNameWithoutExtension(item.File.Name);

            item.State = Item.States.Loading;
            await using (var file = await item.File.OpenReadAsync())
            {
                buffer.SetLengthWithPadding(file.Length);
                await file.CopyToAsync(buffer);
            }
            item.Size = buffer.Length;

            using var decrypto = DecryptoFactory.Create(
                buffer,
                Path.GetFileName(item.File.Name),
                item.AddMessage,
                OnRequestMatchAsync);

            item.State = Item.States.Working;
            var info = await decrypto.DecryptAsync();
            item.Title = info.Title ?? item.Title;
            item.Performers = info.Performers ?? item.Performers;
            item.Album = info.Album ?? item.Album;
            
            var newFile = await (await item.File.GetParentAsync())!.CreateFileAsync(info.NewName);
            if (info.Cover != null)
            {
                using var stream = new MemoryStream(info.Cover);
                item.Cover = Bitmap.DecodeToWidth(stream, 256);
            }

            await using (var file = await newFile!.OpenWriteAsync())
            {
                await buffer.CopyToAsync(file);
            }

            item.State = string.IsNullOrEmpty(item.Messages) ? Item.States.Finished : Item.States.Warn;
        }
        catch (Exception e)
        {
            item.State = Item.States.Error;
            item.AddMessage($"{e.GetType().FullName}\n{e.Message}");
        }
    }

    private static async ValueTask<bool> OnRequestMatchAsync(string message, IEnumerable<DecryptoBase.MatchInfo> properties)
    {
        await _dialogLock.WaitAsync();
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var dialog = new ContentDialog()
                {
                    Title = message,
                    PrimaryButtonText = "Confirm",
                    CloseButtonText = "Cancel",
                    Content = new MatchDialogContent()
                    {
                        DataContext = new MatchViewModel(properties)
                    }
                };

                return (await dialog.ShowAsync()) == ContentDialogResult.Primary;
            });
        }
        catch
        {
            return false;
        }
        finally
        {
            _dialogLock.Release();
        }
    }

    public class Item : INotifyPropertyChanged
    {
        private static readonly Bitmap _coverFallback = new Bitmap(AssetLoader.Open(new Uri("avares://musicdecrypto-avalonia/Assets/MusicNote.png")));

        public Item(IStorageFile file)
        {
            File = file;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public void PropertyHasChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public IStorageFile File { get; init; }

        private Bitmap? _cover;
        public Bitmap Cover
        {
            get => _cover ?? _coverFallback;
            set
            {
                _cover = value;
                PropertyHasChanged(nameof(Cover));
            }
        }

        private string? _title;
        public string Title
        {
            get => _title ?? "N/A";
            set
            {
                _title = value;
                PropertyHasChanged(nameof(Title));
            }
        }

        private string? _performers;
        public string Performers
        {
            get => _performers ?? "N/A";
            set
            {
                _performers = value;
                PropertyHasChanged(nameof(Performers));
            }
        }

        private string? _album;
        public string Album
        {
            get => _album ?? "N/A";
            set
            {
                _album = value;
                PropertyHasChanged(nameof(Album));
            }
        }

        public string Info => string.Join(
            " · ",
            new[]
            {
                Enum.GetName(typeof(States), State),
                _size == 0 ? null : ByteSize.FromBytes(Size).ToBinaryString(),
            }
            .Where(s => !string.IsNullOrEmpty(s)));

        private States _state;
        public States State
        {
            get => _state;
            set
            {
                _state = value;
                PropertyHasChanged(nameof(Info));
            }
        }

        private long _size;
        public long Size
        {
            get => _size;
            set
            {
                _size = value;
                PropertyHasChanged(nameof(Info));
            }
        }

        private readonly LinkedList<string> _messages = new();
        public string? Messages => _messages.Any() ? string.Join('\n', _messages) : null;
        public void AddMessage(string message)
        {
            _messages.AddLast(message);
            PropertyHasChanged(nameof(Messages));
        }

        public enum States : byte
        {
            Queued,
            Loading,
            Working,
            Finished,
            Warn,
            Error,
        }
    }
}
