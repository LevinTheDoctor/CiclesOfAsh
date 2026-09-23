using System.Globalization;

namespace CirclesOfAsh.Core;

public static class ColorUtil
{
    /// <summary>Parst "#RRGGBB" aus den JSON-Daten. Bei Fehlern greift der Fallback statt eines Absturzes.</summary>
    public static Color FromHex(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        string digits = hex.TrimStart('#');
        if (digits.Length != 6 ||
            !uint.TryParse(digits, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
        {
            return fallback;
        }
        // ">>" = Bit-Verschiebung nach rechts, "& 0xFF" = nur die unteren 8 Bit behalten.
        // 0xRRGGBB >> 16 ergibt RR, >> 8 ergibt RRGG (& 0xFF -> GG), ohne Shift & 0xFF -> BB.
        return new Color((int)(rgb >> 16) & 0xFF, (int)(rgb >> 8) & 0xFF, (int)rgb & 0xFF);
    }

    /// <summary>Multipliziert zwei Farben kanalweise (wie ein Tint auf einem Tint). 255 * 255 / 255 = 255.</summary>
    public static Color Multiply(Color first, Color second) =>
        new(first.R * second.R / 255, first.G * second.G / 255, first.B * second.B / 255, first.A * second.A / 255);
}
