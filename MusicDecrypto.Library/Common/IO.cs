using System;
using System.IO;

namespace MusicDecrypto.Library.Common
{
    public sealed class ExtendedMemoryStream : Stream, TagLib.File.IFileAbstraction
    {
        private readonly MemoryStream _inst;
        private int _origin;
        private bool _isOpen;

        // Constructors
        public ExtendedMemoryStream()
        {
            _inst = new MemoryStream();
            _isOpen = true;
        }

        // Properties
        public long Origin
        {
            get => _origin;
            set => _origin = value >= _origin ? Convert.ToInt32(value) : throw new ArgumentOutOfRangeException(nameof(value));
        }

        public void ResetPosition()
        {
            Position = 0;
        }

        public void WriteTo(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!_isOpen)
                throw new ObjectDisposedException(nameof(_inst));

            stream.Write(_inst.GetBuffer(), _origin, Convert.ToInt32(Length));
        }

        // Methods
        public void PerformEach(Func<byte, byte> func)
        {
            if (func == null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            byte[] buffer = _inst.GetBuffer();

            for (int i = _origin; i < _inst.Length; i++)
            {
                buffer[i] = func(buffer[i]);
            }
        }

        public void PerformEach(Func<byte, int, byte> func)
        {
            if (func == null)
            {
                throw new ArgumentNullException(nameof(func));
            }

            byte[] buffer = _inst.GetBuffer();

            for (int i = _origin; i < _inst.Length; i++)
            {
                buffer[i] = func(buffer[i], i - _origin);
            }
        }

        public byte[] ToArray()
        {
            int count = Convert.ToInt32(Length);
            if (count == 0)
                return Array.Empty<byte>();
            var copy = new byte[count];
            Buffer.BlockCopy(_inst.GetBuffer(), _origin, copy, 0, count);
            return copy;
        }

        // Override Stream
        public override long Position
        {
            get => _inst.Position - _origin;
            set => _inst.Position = value >= 0 ? Convert.ToInt32(value) + _origin : throw new ArgumentOutOfRangeException(nameof(value));
        }
        public override long Length => _inst.Length - _origin;
        public override bool CanWrite => _inst.CanWrite;
        public override bool CanTimeout => _inst.CanTimeout;
        public override bool CanSeek => _inst.CanSeek;
        public override bool CanRead => _inst.CanRead;
        public override int ReadTimeout { get => _inst.ReadTimeout; set => _inst.ReadTimeout = value; }
        public override int WriteTimeout { get => _inst.WriteTimeout; set => _inst.WriteTimeout = value; }

        public override void Close()
        {
            _inst.Close();
            base.Close();
        }

        public override void Flush()
            => _inst.Flush();

        public override int Read(byte[] buffer, int offset, int count)
            => _inst.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer)
            => _inst.Read(buffer);

        public override int ReadByte()
            => _inst.ReadByte();

        public override long Seek(long offset, SeekOrigin origin)
            => origin == SeekOrigin.Current ? _inst.Seek(offset, origin)
                                            : _inst.Seek(offset + _origin, origin);

        public override void SetLength(long value)
            => _inst.SetLength(value + _origin);

        public override void Write(byte[] buffer, int offset, int count)
            => _inst.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer)
            => _inst.Write(buffer);

        public override void WriteByte(byte value)
            => _inst.WriteByte(value);

        protected override void Dispose(bool disposing)
        {
            try
            {
                _isOpen = false;
                _inst.Dispose();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

        // Implement IFileAbstraction
        public string Name { get; set; }
        public Stream ReadStream => this;
        public Stream WriteStream => this;

        public void CloseStream(Stream stream)
        {
            stream.Position = 0;
        }
    }
}
