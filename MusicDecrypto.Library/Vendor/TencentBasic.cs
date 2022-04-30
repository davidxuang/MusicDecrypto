using MusicDecrypto.Library.Media;

namespace MusicDecrypto.Library.Vendor
{
    public sealed class TencentBasic : TencentBase
    {
        private static readonly byte[] _header = { 0x00, 0x00, 0x00, 0x20, 0x66, 0x74, 0x79, 0x70 };

        public TencentBasic(MarshalMemoryStream buffer, string name, AudioTypes type) : base(buffer, name, type) { }

        protected override void Process()
        {
            _buffer.Write(_header);
        }
    }
}
