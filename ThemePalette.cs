using System.Drawing.Drawing2D;

namespace RoletaDaDaily;

internal sealed record ThemePalette(
    string Name,
    Color[] Segments,
    Color RingStart,
    Color RingEnd,
    Color HubStart,
    Color HubEnd,
    Color[] Confetti)
{
    public static IReadOnlyList<ThemePalette> All { get; } = new[]
    {
        Signature,
        Make("Cassino clássico", ["#7B202D", "#173E3B", "#BD8B35", "#253554", "#6A2F62", "#315C4D"]),
        Make("Neon cyberpunk", ["#111A43", "#6C1BE6", "#00A8A8", "#DB147B", "#254BE8", "#8A3CF1"]),
        Make("Pastel suave", ["#D9A9B7", "#A9CBD1", "#E9CD8C", "#B8B0DD", "#B7D1A6", "#E4B99C"]),
        Make("Oceano", ["#123D62", "#176C79", "#2497A0", "#315795", "#16838B", "#567EAE"]),
        Make("Floresta", ["#234C37", "#527143", "#8D7938", "#315D55", "#697C48", "#456C61"]),
        Make("Pôr do sol", ["#AD443F", "#D67342", "#E6A348", "#8C4365", "#BC5575", "#66507D"]),
        Make("Realeza", ["#40236F", "#772C81", "#A27A2D", "#33417D", "#632F68", "#90613A"]),
        Make("Candy pop", ["#E34E87", "#7155D9", "#35A9C3", "#E48643", "#5C9B5C", "#CE4E62"]),
        Make("Monocromático", ["#242832", "#444A55", "#646B76", "#303640", "#777E88", "#515762"]),
        Make("Retrô arcade", ["#16365E", "#C24635", "#D39421", "#356C4B", "#623D80", "#187A7A"]),
        Make("Tons terrosos", ["#604534", "#93623D", "#AD884F", "#63734F", "#855A5D", "#52646B"]),
        Make("Gelo e prata", ["#56718B", "#87A8B8", "#A7BBC5", "#496185", "#7895A7", "#617B9E"])
    };

    /// <summary>Identidade visual do aplicativo: azul-marinho com aro dourado.</summary>
    private static ThemePalette Signature => new(
        "Azul e dourado",
        new[] { "#142C63", "#1E4489", "#0F2350", "#2A5AA8", "#18376E", "#23498C" }
            .Select(ParseHex).ToArray(),
        ParseHex("#F5C542"),
        ParseHex("#B8891F"),
        ParseHex("#1E4489"),
        ParseHex("#0B1836"),
        new[] { "#F5C542", "#F7E6B8", "#2FB3B3", "#2A5AA8", "#E0609A", "#F3F0E6" }
            .Select(ParseHex).ToArray());

    private static ThemePalette Make(string name, string[] colors)
    {
        Color[] segments = colors.Select(ParseHex).ToArray();
        Color[] confetti = segments.Concat([ParseHex("#F3C978"), ParseHex("#F7E9C8")]).ToArray();
        Color light = segments.OrderByDescending(c => c.GetBrightness()).First();
        Color dark = segments.OrderBy(c => c.GetBrightness()).First();
        return new ThemePalette(name, segments, Lighten(dark, 0.48f), dark, Lighten(light, 0.12f), dark, confetti);
    }

    private static Color ParseHex(string value) => Color.FromArgb(
        Convert.ToInt32(value.Substring(1, 2), 16),
        Convert.ToInt32(value.Substring(3, 2), 16),
        Convert.ToInt32(value.Substring(5, 2), 16));

    public static Color Lighten(Color color, float amount) => Color.FromArgb(
        255,
        (int)(color.R + (255 - color.R) * amount),
        (int)(color.G + (255 - color.G) * amount),
        (int)(color.B + (255 - color.B) * amount));

    public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal readonly record struct AppTheme(
    Color BackgroundTop,
    Color BackgroundBottom,
    Color Panel,
    Color PanelBorder,
    Color Text,
    Color Muted,
    Color Field,
    Color FieldBorder,
    Color Gold,
    Color GoldDark,
    Color Selection,
    bool IsLight)
{
    public static AppTheme Dark => new(
        Color.FromArgb(16, 34, 79), Color.FromArgb(7, 15, 43), Color.FromArgb(20, 41, 92),
        Color.FromArgb(44, 79, 147), Color.FromArgb(241, 245, 255), Color.FromArgb(155, 176, 216),
        Color.FromArgb(11, 26, 66), Color.FromArgb(42, 71, 133), Color.FromArgb(245, 197, 66),
        Color.FromArgb(207, 156, 30), Color.FromArgb(28, 59, 120), false);

    public static AppTheme Light => new(
        Color.FromArgb(238, 243, 253), Color.FromArgb(220, 230, 247), Color.FromArgb(255, 255, 255),
        Color.FromArgb(198, 212, 236), Color.FromArgb(18, 32, 72), Color.FromArgb(91, 107, 144),
        Color.FromArgb(245, 248, 254), Color.FromArgb(194, 209, 234), Color.FromArgb(185, 138, 23),
        Color.FromArgb(142, 106, 16), Color.FromArgb(217, 228, 248), true);
}
