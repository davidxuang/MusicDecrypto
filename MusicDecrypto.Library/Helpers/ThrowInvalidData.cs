using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace MusicDecrypto.Library.Helpers;

internal static class ThrowInvalidData
{
    [DoesNotReturn]
    public static void True(string subject) => throw new InvalidDataException($"{subject} is invalid.");
    [DoesNotReturn]
    public static T True<T>(string subject) => throw new InvalidDataException($"{subject} is invalid.");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void If([DoesNotReturnIf(true)] bool condition, string subject)
    {
        if (condition)
        {
            True(subject);
        }
    }

    [DoesNotReturn]
    private static void Null(string subject) => throw new InvalidDataException($"{subject} should not be null.");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNull<T>(T? value, string subject)
        where T : notnull
    {
        if (value is null)
        {
            Null(subject);
        }
    }

    [DoesNotReturn]
    private static void Zero(string subject) => throw new InvalidDataException($"{subject} should not be zero.");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfZero<T>(T? value, string subject)
        where T : INumber<T>
    {
        if (value is null)
        {
            Zero(subject);
        }
    }

    [DoesNotReturn]
    private static void Negative(string subject) => throw new InvalidDataException($"{subject} should not be negative.");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNegative<T>(T value, string subject)
        where T : INumber<T>
    {
        if (value < T.Zero)
        {
            Negative(subject);
        }
    }

    [DoesNotReturn]
    private static void NotEqual(string subject) => throw new InvalidDataException($"{subject} is unexpected.");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNotEqual<T>(T value, T other, string subject)
        where T : INumber<T>
    {
        if (value != other)
        {
            NotEqual(subject);
        }
    }

    [DoesNotReturn]
    private static void LessThan(string subject) => throw new InvalidDataException($"{subject} is too small.");
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfLessThan<T>(T value, T other, string subject)
        where T : INumber<T>
    {
        if (value < other)
        {
            LessThan(subject);
        }
    }
}
