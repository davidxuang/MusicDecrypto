using System;
using System.IO;
using Cysharp.Collections;
using MusicDecrypto.Library.Numerics;

namespace MusicDecrypto.Library
{
    public sealed class MarshalMemoryStream : Stream, TagLib.File.IFileAbstraction
    {
        #region Fields
        private NativeMemoryArray<byte> _inst;
        private long _offset;
        private long _position;
        private long _length;
        private long _capacity;

        private bool _isOpen;
        #endregion

        #region Lifetime
        public MarshalMemoryStream(long capacity = 0, bool skipZeroClear = true)
        {
            _capacity = capacity;
            if (capacity > 0) // SIMD padding
                _ = checked(capacity += Simd.LaneCount - 1);
            _inst = new NativeMemoryArray<byte>(capacity, skipZeroClear, true);
            _isOpen = true;
        }

        ~MarshalMemoryStream()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _isOpen = false;
                _inst.Dispose();
            }
        }
        #endregion

        #region Properties
        public long Origin
        {
            get => _offset;
            set
            {
                if (value > _length)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _offset = value;
                if (_offset > _position) _position = _offset;
            }
        }
        #endregion

        #region Helpers
        private void AssertNotClosed()
        {
            if (!_isOpen)
                throw new ObjectDisposedException(nameof(MarshalMemoryStream));
        }

        private bool EnsureCapacity(long value)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (value > _capacity)
            {
                long n = Math.Max(value, checked(_capacity + 0x100));

                if (_inst.Length > 0)
                {
                    var a = new NativeMemoryArray<byte>(n, true, true);
                    _inst.AsStream().CopyTo(a.AsStream(FileAccess.Write));
                    _inst.Dispose();
                    _inst = a;
                    _capacity = n;
                }
                else
                {
                    _inst = new NativeMemoryArray<byte>(n, true, true);
                }

                _capacity = n;
                return true;
            }
            return false;
        }

        //private static void ValidateBufferArguments(byte[] buffer, int offset, int count)
        //{
        //    if (buffer is null)
        //        throw new ArgumentNullException(nameof(buffer));

        //    if (offset < 0)
        //        throw new ArgumentOutOfRangeException(nameof(offset));

        //    if ((uint)count > buffer.Length - offset)
        //        throw new ArgumentOutOfRangeException(nameof(count));
        //}
        #endregion

        #region Methods
        public void ResetPosition()
        {
            Position = 0;
        }

        public Span<byte> AsSpan() => _inst.AsSpan(_offset, checked((int)(_length - _offset)));
        public Span<byte> AsSpan(long start)
        {
            if (start < 0 || _offset + start > _length)
                throw new ArgumentOutOfRangeException(nameof(start));
            return _inst.AsSpan(_offset + start, checked((int)(_length - _offset - start)));
        }
        public Span<byte> AsSpan(long start, int length)
        {
            if (start < 0 || _offset + start > _length)
                throw new ArgumentOutOfRangeException(nameof(start));
            if (_offset + start + length > _length)
                throw new ArgumentOutOfRangeException(nameof(length));
            return _inst.AsSpan(_offset + start, length);
        }
        public Span<byte> AsSimdPaddedSpan() =>
            _inst.AsSpan(
                _offset,
                checked((int)(((_length - _offset - 1) / Simd.LaneCount + 1) * Simd.LaneCount)));
        public Span<byte> AsSimdPaddedSpan(long start) =>
            _inst.AsSpan(
                _offset + start,
                checked((int)(((_length - _offset - start - 1) / Simd.LaneCount + 1) * Simd.LaneCount)));
        #endregion

        #region Implements `Stream`
        public override bool CanRead => _isOpen;
        public override bool CanSeek => _isOpen;
        public override bool CanWrite => _isOpen;

        public override long Length
        {
            get
            {
                AssertNotClosed();
                return _length - _offset;
            }
        }
        public override void SetLength(long value)
        {
            AssertNotClosed();
            var n = checked(_offset + value);
            _ = EnsureCapacity(n + Simd.LaneCount - 1);
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _length = n;
            if (_position > _length) _position = _length;
        }
        public void SetLengthWithPadding(long value)
        {
            AssertNotClosed();
            var n = checked(_offset + value);
            _ = EnsureCapacity(n + 0x1000); // 4KiB reserved
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            _length = n;
            if (_position > _length) _position = _length;
        }

        public override long Position
        {
            get
            {
                AssertNotClosed();
                return _position - _offset;
            }

            set
            {
                AssertNotClosed();
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _position = checked(_offset + value);
            }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            AssertNotClosed();

            var n = (int)Math.Min(_length - _position, buffer.Length);
            if (n <= 0) return 0;

            _inst.AsSpan(_position, n).CopyTo(buffer);
            _position += n;
            return n;
        }

        public override int ReadByte()
        {
            AssertNotClosed();

            if (_position >= _length) return -1;

            return _inst[_position++];
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            AssertNotClosed();

            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize));
            if (!destination.CanWrite)
            {
                if (destination.CanSeek)
                    throw new NotSupportedException();
                else
                    throw new ObjectDisposedException(nameof(destination));
            }

            if (_length - _position > 0) destination.Write(AsSpan());
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            AssertNotClosed();

            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length + offset;
                    break;
                default: throw new ArgumentException("Invalid seek origin.", nameof(origin));
            }

            return Position;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            AssertNotClosed();
            ValidateBufferArguments(buffer, offset, count);

            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            AssertNotClosed();

            var n = checked(_position + buffer.Length);

            if (n > _length)
            {
                _ = EnsureCapacity(n + Simd.LaneCount - 1);
                _length = n;
            }

            buffer.CopyTo(_inst.AsSpan(_position, buffer.Length));
            _position = n;
        }

        public override void WriteByte(byte value)
        {
            AssertNotClosed();

            var n = checked(_position + 1);

            if (n > _length)
            {
                _ = EnsureCapacity(n + Simd.LaneCount - 1);
                _length = n;
            }

            _inst[_position++] = value;
        }
        #endregion

        #region Implements `TagLib.File.IFileAbstraction`
        public string Name { get; set; } = default!;
        public Stream ReadStream => this;
        public Stream WriteStream => this;

        public void CloseStream(Stream stream)
        {
            stream.Position = 0;
        }
        #endregion
    }
}
