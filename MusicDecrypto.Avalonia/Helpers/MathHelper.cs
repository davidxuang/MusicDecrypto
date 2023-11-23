using System;

namespace MusicDecrypto.Avalonia.Helpers;

internal static class MathHelper
{
    public static double RoundToEven(double v)
    {
        return Math.Round(v / 2, MidpointRounding.AwayFromZero) * 2;
    }
}
