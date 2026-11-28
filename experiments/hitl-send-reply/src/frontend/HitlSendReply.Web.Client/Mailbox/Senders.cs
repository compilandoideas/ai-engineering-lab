using System.Globalization;

namespace HitlSendReply.Web.Client.Mailbox;

/// <summary>How a sender is shown: a name from the address, an initial and a soft color of its own.</summary>
public static class Senders
{
    private static readonly string[] Generic = ["billing", "support", "noreply", "no-reply", "info", "hello", "sales", "team", "partnerships", "facturacion", "contacto", "ventas"];

    // Soft tints (background, text) that read in light and dark; the same address always gets the same one.
    private static readonly string[] Tints = ["tint-blue", "tint-violet", "tint-green", "tint-amber", "tint-rose", "tint-teal", "tint-slate"];

    /// <summary>"ana.perez@example.com" is Ana Perez; "billing@contoso.example" is Contoso Billing.</summary>
    public static string DisplayName(string address)
    {
        var at = address.IndexOf('@');
        if (at <= 0)
        {
            return address;
        }

        var local = address[..at];
        var domain = address[(at + 1)..].Split('.')[0];
        var words = local.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 1 && Generic.Contains(local.ToLowerInvariant()))
        {
            words = [domain, local];
        }

        return string.Join(' ', words.Select(Title));
    }

    public static string Initial(string address) => DisplayName(address) is { Length: > 0 } name ? name[..1].ToUpperInvariant() : "?";

    public static string Tint(string address) =>
        Tints[(int)((uint)StableHash(address.ToLowerInvariant()) % (uint)Tints.Length)];

    /// <summary>Today's mail shows the time; older mail the day, as a mail client does.</summary>
    public static string When(DateTimeOffset at, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language == "es" ? "es-CL" : "en-US");
        var local = at.ToLocalTime();
        if (local.Date == DateTime.Today)
        {
            return local.ToString(language == "es" ? "HH:mm" : "h:mm tt", culture);
        }

        return local.ToString(language == "es" ? "d MMM" : "MMM d", culture).Replace(".", "");
    }

    public static string Stamp(DateTimeOffset at, string language)
    {
        var culture = CultureInfo.GetCultureInfo(language == "es" ? "es-CL" : "en-US");
        var local = at.ToLocalTime();
        return local.ToString(language == "es" ? "d MMM, HH:mm" : "MMM d, h:mm tt", culture).Replace(".", "");
    }

    private static string Title(string word) => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

    // string.GetHashCode changes per process; this one does not, so a sender keeps its color.
    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 23;
            foreach (var c in value)
            {
                hash = (hash * 31) + c;
            }

            return hash;
        }
    }
}
