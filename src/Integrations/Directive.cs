using System.Globalization;

namespace CustomHUD.Integrations;

// Values written into directives (README, "What a page sends").
internal static class Directive
{
    // A value runs to the next ';', and a tab starts the next column.
    public static string Clean(string? text) => (text ?? string.Empty).Replace(';', ',').Replace('\t', ' ');

    // "4 812": the pages group thousands with a space whatever the server's culture.
    public static string Number(long value) => value.ToString("#,0", Grouped);

    public static string Decimal(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    // A share for a meter, 0-100.
    public static int Bar(double percent) => (int)Math.Clamp(Math.Round(percent), 0, 100);

    public static double Percent(long part, long whole) => whole <= 0 ? 0 : Math.Round(part * 100.0 / whole, 1);

    private static readonly NumberFormatInfo Grouped = new() { NumberGroupSeparator = " " };
}
