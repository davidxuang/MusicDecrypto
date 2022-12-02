using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ByteSizeLib;
using MusicDecrypto.Library;

namespace MusicDecrypto.Avalonia.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private static readonly Regex _nameRegex = new(@"^(?:.+ - |\d+\. )?(.+) - (.+)$");

        public ObservableCollection<Item> Items { get; private set; } = new();

        public bool IsEmpty => Items.Count == 0;

        public MainViewModel()
        {
            Items.CollectionChanged += (s, e) => PropertyHasChanged(nameof(IsEmpty));
        }

        public void AddFile(string path)
        {
            if (File.Exists(path) && DecryptoFactory.KnownExtensions.Contains(Path.GetExtension(path)))
            {
                var item = new Item(path);
                Items.Add(item);
                _ = ThreadPool.QueueUserWorkItem(DecryptFile, item, false);
            }
        }

        public static void DecryptFile(Item item)
        {
            try
            {
                using var buffer = new MarshalMemoryStream();
                string outPath;

                var match = _nameRegex.Match(Path.GetFileNameWithoutExtension(item.FilePath));
                if (match.Success)
                {
                    item.Artist = match.Groups[1].Value;
                    item.Title = match.Groups[2].Value;
                }
                else item.Title = Path.GetFileNameWithoutExtension(item.FilePath);

                item.State = Item.States.Loading;
                using (var file = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    buffer.SetLengthWithPadding(file.Length);
                    file.CopyTo(buffer);
                }
                item.Size = buffer.Length;

                using var decrypto = DecryptoFactory.Create(
                    buffer,
                    Path.GetFileName(item.FilePath),
                    item.AddMessage);

                item.State = Item.States.Working;
                var info = decrypto.Decrypt();
                item.Title = info.Title ?? item.Title;
                item.Artist = info.Artist ?? item.Artist;
                item.Album = info.Album ?? item.Album;
                outPath = Path.Combine(Path.GetDirectoryName(item.FilePath)!, info.NewName);
                if (info.Cover != null)
                {
                    using var stream = new MemoryStream(info.Cover);
                    item.Cover = Bitmap.DecodeToWidth(stream, 256);
                }

                using (var file = new FileStream(
                    outPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None))
                {
                    buffer.CopyTo(file);
                }

                item.State = string.IsNullOrEmpty(item.Messages) ? Item.States.Finished : Item.States.Warn;
            }
            catch (Exception e)
            {
                item.State = Item.States.Error;
                item.AddMessage($"{e.GetType().FullName}\n{e.Message}");
            }
        }

        public class Item : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            public void PropertyHasChanged(string propName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }

            static Item()
            {
                var assets = AvaloniaLocator.Current.GetService<IAssetLoader>();
                _coverFallback = new Bitmap(assets!.Open(new Uri("avares://musicdecrypto-avalonia/Assets/MusicNote.png")));
            }

            public Item(string path)
            {
                FilePath = path;
            }

            public string FilePath { get; init; }

            private static readonly Bitmap _coverFallback;

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

            private string? _artist;
            public string Artist
            {
                get => _artist ?? "N/A";
                set
                {
                    _artist = value;
                    PropertyHasChanged(nameof(Artist));
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
                " ¡¤ ",
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
}
