using System.IO;
using System.Text.RegularExpressions;
using MusicDecrypto.Library.Media;
using TagLib;

namespace MusicDecrypto.Library.Vendor
{
    public abstract class TencentBase : DecryptoBase
    {
        private static readonly Regex _regex = new("^[0-9A-F]{16,}$");

        protected TencentBase(MarshalMemoryStream buffer, string name, AudioTypes type) : base(buffer, name, type) { }

        protected override bool MetadataMisc(Tag tag)
        {
            if (tag == null) return false;

            var baseName = Path.GetFileNameWithoutExtension(Name);

            if (_regex.IsMatch(baseName))
            {
                if (tag.Title != null && tag.AlbumArtists.Length > 0)
                {
                    _newBaseName = string.Join(" - ", tag.AlbumArtists[0], tag.Title);
                    RaiseWarn($"New filename “{_newBaseName}”");
                }
                else if (tag.Title != null && tag.Performers.Length > 0)
                {
                    _newBaseName = string.Join(" - ", tag.Performers[0], tag.Title);
                    RaiseWarn($"New filename “{_newBaseName}”");
                }
                else RaiseWarn("Detected hashed filename but failed to determine new name.");
            }

            if (tag.Pictures.Length > 0)
            {
                if (tag.Pictures[0].Type != PictureType.FrontCover)
                {
                    tag.Pictures[0].Type = PictureType.FrontCover;
                    return true;
                }
            }

            return false;
        }
    }
}
