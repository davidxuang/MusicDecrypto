using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ByteSizeLib;
using MusicDecrypto.Avalonia.Helpers;
using MusicDecrypto.Library;

namespace MusicDecrypto.Avalonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [GeneratedRegex("^(?:.+ - |\\d+\\. )?(.+) - (.+)$")]
    private static partial Regex NameRegex();
    private static readonly Regex _regex = NameRegex();

    private const int _imageWidth = 72;
    private readonly int _imageSize;

    public ObservableCollection<Item> Items { get; private set; } = [];

    public bool IsEmpty => Items.Count == 0;

    public MainViewModel() : this(1) { }

    public MainViewModel(double scaling)
    {
        _imageSize = (int)MathHelper.RoundToEven(_imageWidth * 2 * scaling);
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
        else
        {
            file.Dispose();
        }
    }

    public async ValueTask DecryptFileAsync(Item item)
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
                item.AddMessage);

            item.State = Item.States.Working;
            var info = await decrypto.DecryptAsync();
            item.Title = info.Title ?? item.Title;
            item.Performers = info.Performers ?? item.Performers;
            item.Album = info.Album ?? item.Album;
            
            var newFile = await (await item.File.GetParentAsync())!.CreateFileAsync(info.NewName);
            if (info.Cover != null)
            {
                using var stream = new MemoryStream(info.Cover);
                // https://github.com/mono/SkiaSharp/issues/2645
                // item.Cover = Bitmap.DecodeToWidth(stream, (int)(72 * 2 * _scaling));
                var bm = new Bitmap(stream);
                var size = Math.Max(bm.Size.Width, bm.Size.Height);
                if (size > _imageSize)
                {
                    item.Cover = bm.Size.Width > bm.Size.Height
                        ? bm.CreateScaledBitmap(new(_imageSize, (int)Math.Round(_imageSize * bm.Size.Height / bm.Size.Width)))
                        : bm.CreateScaledBitmap(new((int)Math.Round(_imageSize * bm.Size.Width / bm.Size.Height), _imageSize));
                    bm.Dispose();
                }
                else
                {
                    item.Cover = bm;
                }
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

    public class Item(IStorageFile file) : INotifyPropertyChanged, IDisposable
    {
        private static readonly Bitmap _coverFallback = new(AssetLoader.Open(new Uri("avares://musicdecrypto-avalonia/Assets/MusicNote.png")));

        public void Dispose()
        {
            File.Dispose();
            _cover?.Dispose();
            GC.SuppressFinalize(this);
        }

        public IStorageFile File { get; init; } = file;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void PropertyHasChanged(string propName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

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
        public string? Messages => _messages.Count > 0 ? string.Join('\n', _messages) : null;
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
