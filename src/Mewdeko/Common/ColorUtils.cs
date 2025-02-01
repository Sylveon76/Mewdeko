using System.Globalization;
using System.Text.RegularExpressions;
using Color = System.Drawing.Color;

namespace Mewdeko.Common;

/// <summary>
/// Tries to parse colors
/// </summary>
public static class ColorUtils
{
    private static readonly Dictionary<string, Color> NamedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Discord standard colors
        { "default", Color.FromArgb(0, 0, 0) },
        { "teal", Color.FromArgb(29, 147, 171) },
        { "green", Color.FromArgb(67, 181, 129) },
        { "blue", Color.FromArgb(114, 137, 218) },
        { "purple", Color.FromArgb(80, 44, 169) },
        { "magenta", Color.FromArgb(255, 0, 255) },
        { "gold", Color.FromArgb(250, 166, 26) },
        { "orange", Color.FromArgb(255, 140, 0) },
        { "red", Color.FromArgb(238, 66, 77) },

        // Additional common colors
        { "yellow", Color.FromArgb(255, 255, 0) },
        { "pink", Color.FromArgb(255, 192, 203) },
        { "cyan", Color.FromArgb(0, 255, 255) },
        { "brown", Color.FromArgb(165, 42, 42) },
        { "white", Color.FromArgb(255, 255, 255) },
        { "black", Color.FromArgb(0, 0, 0) },
        { "gray", Color.FromArgb(128, 128, 128) },
        { "grey", Color.FromArgb(128, 128, 128) },
        { "transparent", Color.FromArgb(0, 0, 0, 0) }
    };

    /// <summary>
    /// Tries to parse a color from various formats into a Discord.Color.
    /// Supports:
    /// - Named colors (red, blue, etc.)
    /// - Hex codes (#FF0000 or FF0000)
    /// - RGB format (rgb(255,0,0))
    /// - Comma separated values (255,0,0)
    /// </summary>
    public static bool TryParseColor(string input, out Discord.Color color)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            color = Discord.Color.Default;
            return false;
        }

        input = input.Trim();

        // Try named colors first
        if (NamedColors.TryGetValue(input, out var namedColor))
        {
            color = new Discord.Color(namedColor.R, namedColor.G, namedColor.B);
            return true;
        }

        // Try hex format
        if (input.StartsWith("#"))
            input = input.Substring(1);

        if (Regex.IsMatch(input, "^[0-9a-fA-F]{6}$"))
        {
            try
            {
                var value = int.Parse(input, NumberStyles.HexNumber);
                var r = (value >> 16) & 0xFF;
                var g = (value >> 8) & 0xFF;
                var b = value & 0xFF;
                color = new Discord.Color(r, g, b);
                return true;
            }
            catch
            {
                color = Discord.Color.Default;
                return false;
            }
        }

        // Try RGB format
        var rgbMatch = Regex.Match(input, @"^rgb\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)$");
        if (rgbMatch.Success)
        {
            if (TryParseRgb(rgbMatch.Groups[1].Value, rgbMatch.Groups[2].Value, rgbMatch.Groups[3].Value, out color))
                return true;
        }

        // Try comma separated values
        var parts = input.Split(',');
        if (parts.Length == 3)
        {
            if (TryParseRgb(parts[0], parts[1], parts[2], out color))
                return true;
        }

        color = Discord.Color.Default;
        return false;
    }

    private static bool TryParseRgb(string r, string g, string b, out Discord.Color color)
    {
        try
        {
            var red = Math.Clamp(int.Parse(r.Trim()), 0, 255);
            var green = Math.Clamp(int.Parse(g.Trim()), 0, 255);
            var blue = Math.Clamp(int.Parse(b.Trim()), 0, 255);

            color = new Discord.Color(red, green, blue);
            return true;
        }
        catch
        {
            color = Discord.Color.Default;
            return false;
        }
    }

    /// <summary>
    /// Converts a Discord.Color to its hex string representation.
    /// </summary>
    public static string ToHex(this Discord.Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    /// <summary>
    /// Converts a Discord.Color to an RGB string representation.
    /// </summary>
    public static string ToRgb(this Discord.Color color)
    {
        return $"rgb({color.R}, {color.G}, {color.B})";
    }

    /// <summary>
    /// Gets a random Discord.Color.
    /// </summary>
    public static Discord.Color Random()
    {
        var random = new Random();
        return new Discord.Color(random.Next(256), random.Next(256), random.Next(256));
    }

    /// <summary>
    /// Gets a lighter version of the color.
    /// </summary>
    public static Discord.Color Lighten(this Discord.Color color, float amount = 0.2f)
    {
        amount = Math.Clamp(amount, 0, 1);

        var r = (int)Math.Min(255, color.R + (255 - color.R) * amount);
        var g = (int)Math.Min(255, color.G + (255 - color.G) * amount);
        var b = (int)Math.Min(255, color.B + (255 - color.B) * amount);

        return new Discord.Color(r, g, b);
    }

    /// <summary>
    /// Gets a darker version of the color.
    /// </summary>
    public static Discord.Color Darken(this Discord.Color color, float amount = 0.2f)
    {
        amount = Math.Clamp(amount, 0, 1);

        var r = (int)(color.R * (1 - amount));
        var g = (int)(color.G * (1 - amount));
        var b = (int)(color.B * (1 - amount));

        return new Discord.Color(r, g, b);
    }
}