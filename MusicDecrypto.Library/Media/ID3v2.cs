using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MusicDecrypto.Library.Helpers;
using MusicDecrypto.Library.Numerics.Synchsafe;

namespace MusicDecrypto.Library.Media;

internal static class ID3v2
{
    internal readonly ref struct Tag
    {
        public readonly int Size;
        public readonly Header Header;
        public readonly ExtendedHeader ExtendedHeader;
        private readonly ReadOnlySpan<byte> _frames;

        public Tag(ReadOnlySpan<byte> data)
        {
            Header = new(data);
            if (Header.Version < 3 || Header.Version > 4)
            {
                throw new NotImplementedException();
            }
            if (Header.Flags.HasFlag(TagLib.Id3v2.HeaderFlags.ExtendedHeader))
            {
                ExtendedHeader = new(data[10..], Header.Version);
                _frames = data.Slice(Header.HeaderSize + 4 + ExtendedHeader.Size, Header.Size - 4 - ExtendedHeader.Size);
            }
            else
            {
                _frames = data.Slice(Header.HeaderSize, Header.Size);
            }
            Size = 10 + Header.Size + Convert.ToInt32(Header.Flags.HasFlag(TagLib.Id3v2.HeaderFlags.FooterPresent)) * 10;
        }

        public readonly Frames GetEnumerator() => new(_frames);

        internal ref struct Frames(ReadOnlySpan<byte> data)
        {
            private readonly ReadOnlySpan<byte> _data = data;
            private Frame _frame;
            private int _position;

            public readonly Frame Current => _frame;

            public bool MoveNext()
            {
                if (Frame.Create(_data[_position..], out _frame))
                {
                    _position += Frame.FrameHeader.HeaderSize + Current.Header.Size;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public void Reset() => _position = 0;
        }
    }

    [InlineArray(10)]
    private struct B10
    {
#pragma warning disable IDE0044, IDE0051
        private byte _byte;
#pragma warning restore IDE0044, IDE0051
    }

    [InlineArray(3)]
    private struct B3
    {
#pragma warning disable IDE0044, IDE0051
        private byte _byte;
#pragma warning restore IDE0044, IDE0051
    }

    [StructLayout(LayoutKind.Explicit, Size = 10)]
    internal readonly struct Header
    {
        public const int HeaderSize = 10;

        [FieldOffset(0)] private readonly B10 _span;

        [FieldOffset(0)] private readonly B3 Identifier;
        [FieldOffset(3)] public readonly byte Version;
        [FieldOffset(3)] public readonly byte Revision;
        [FieldOffset(5)] public readonly TagLib.Id3v2.HeaderFlags Flags;
        [FieldOffset(6)] public readonly Int32S Size;

        internal Header(ReadOnlySpan<byte> data)
        {
            data[..10].CopyTo(_span);
            ThrowInvalidData.If(!MemoryExtensions.SequenceEqual(Identifier, "ID3"u8), "ID3 header");
            if (Flags.HasFlag(TagLib.Id3v2.HeaderFlags.Unsynchronisation)) throw new NotImplementedException();
        }
    }


    [StructLayout(LayoutKind.Explicit, Size = 10)]
    internal readonly struct ExtendedHeader
    {
        [FieldOffset(0)] private readonly B10 _span;

        [FieldOffset(0)] public readonly Int32S Size;
        [FieldOffset(4)] public readonly Flags3 FlagsL;
        [FieldOffset(5)] public readonly Flags4 FlagsH;
        [FieldOffset(6)] private readonly Int32S _omitted;

        internal ExtendedHeader(ReadOnlySpan<byte> data, byte major)
        {
            data[..10].CopyTo(_span[..]);
            ThrowInvalidData.If(Size != 6 && Size != 10, "ID3 extended header");
            if (major == 4)
            {
                ThrowInvalidData.IfNotEqual((byte)FlagsL, 1, "ID3 extended header");
            }
        }

        [Flags]
        internal enum Flags3 : byte
        {
            CrcPresent = 0x80,
        }

        [Flags]
        internal enum Flags4 : byte
        {
            IsUpdate = 0x40,
            CrcPresent = 0x20,
            TagRestrictions = 0x10,
        }
    }

    internal ref struct Frame
    {
        public FrameHeader Header;
        public ReadOnlySpan<byte> Span;

        public string Text => Header.Id[0] switch
        {
            (byte)'T' when !MemoryExtensions.SequenceEqual(Header.Id, "TXXX"u8) => Span switch
            {
                [0, ..]             => Encoding.Latin1.GetString(Span[1..]),
                [1, 0xff, 0xfe, ..] => Encoding.Unicode.GetString(Span[3..]),
                [1, 0xfe, 0xff, ..] => Encoding.BigEndianUnicode.GetString(Span[3..]),
                [2, ..]             => Encoding.BigEndianUnicode.GetString(Span[1..]),
                [3, ..]             => Encoding.UTF8.GetString(Span[1..]),
                _                   => ThrowInvalidData.True<string>(Encoding.Latin1.GetString(Header.Id)),
            },
            (byte)'W' when !MemoryExtensions.SequenceEqual(Header.Id, "WXXX"u8) => Encoding.Latin1.GetString(Span),
            _ => throw new NotSupportedException(),
        };

        internal static bool Create(ReadOnlySpan<byte> data, out Frame frame)
        {
            if (data.Length < FrameHeader.HeaderSize)
            {
                frame = default;
                return false;
            }

            frame = new()
            {
                Header = new(data)
            };
            if (MemoryExtensions.SequenceEqual(frame.Header.Id, "\0\0\0\0"u8))
            {
                frame = default;
                return false;
            }
            else
            {
                frame.Span = data.Slice(FrameHeader.HeaderSize, frame.Header.Size);
                return true;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 10)]
        internal struct FrameHeader
        {
            public const int HeaderSize = 10;

            [FieldOffset(0)] private readonly B10 _span;

            [FieldOffset(0)] public FrameId Id;
            [FieldOffset(4)] public Int32S Size;
            [FieldOffset(8)] public byte FlagsH;
            [FieldOffset(9)] public byte FlagsL;

            internal FrameHeader(ReadOnlySpan<byte> data)
            {
                data[..HeaderSize].CopyTo(_span);
            }

            [InlineArray(4)]
            internal struct FrameId
            {
                /// <summary>
                /// Album
                /// </summary>
                public static readonly FrameId TALB = new("TALB"u8);
                /// <summary>
                /// Title
                /// </summary>
                public static readonly FrameId TIT2 = new("TIT2"u8);
                /// <summary>
                /// Lead artist
                /// </summary>
                public static readonly FrameId TPE1 = new("TPE1"u8);

                /// <summary>
                /// Encoded by
                /// </summary>
                public static readonly FrameId TENC = new("TENC"u8);
                /// <summary>
                /// Track number
                /// </summary>
                public static readonly FrameId TRCK = new("TRCK"u8);
                /// <summary>
                /// Size
                /// </summary>
                public static readonly FrameId TSIZ = new("TSIZ"u8);
                /// <summary>
                /// ISRC
                /// </summary>
                public static readonly FrameId TSRC = new("TSRC"u8);
                /// <summary>
                /// Software/Hardware and settings used for encoding
                /// </summary>
                public static readonly FrameId TSSE = new("TSSE"u8);

#pragma warning disable IDE0044, IDE0051
                private byte _byte;
#pragma warning restore IDE0044, IDE0051

                internal FrameId(ReadOnlySpan<byte> data)
                {
                    data.CopyTo(this);
                }

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static bool operator ==(FrameId left, FrameId right)
                    => MemoryExtensions.SequenceEqual<byte>(left, right);

                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static bool operator !=(FrameId left, FrameId right)
                    => !MemoryExtensions.SequenceEqual<byte>(left, right);

                public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is FrameId id && this == id;

                public override readonly int GetHashCode() => HashCode.Combine(this[0], this[1], this[2], this[3]);

                public override string ToString() => Encoding.Latin1.GetString(this);
            }
        }
    }
}
