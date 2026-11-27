using System.Globalization;
using System.Text.RegularExpressions;

namespace HitlSendReply.Domain.Policy;

public sealed record FactCheckResult(IReadOnlyList<string> Issues)
{
    public bool Verified => Issues.Count == 0;
}

/// <summary>
/// Extracts the claims a reply makes about the order (order id, dates, amounts, status, time
/// commitments) and compares them with the system of record. It does not trust the model to
/// declare its claims. Anything it cannot extract or verify is an issue, which sends the draft
/// to a human.
/// </summary>
/// <remarks>
/// Deliberately narrow: it checks that a stated value exists in the record, not that it is
/// attached to the right field ("shipped on" vs "arrives on"). That gap is documented as a limitation.
/// </remarks>
public static class FactChecker
{
    public static FactCheckResult Check(string text, Order order, Language language = Language.En)
    {
        var rules = ClaimPatterns.For(language);
        var issues = new List<string>();
        var masked = text;

        masked = Consume(masked, ClaimPatterns.OrderRef, m =>
        {
            if (!string.Equals(m.Groups["id"].Value, order.Id, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Order reference '{m.Value}' does not match the ticket's order #{order.Id}.");
            }
        });

        masked = Consume(masked, ClaimPatterns.IsoDate, m =>
            CheckDate(DateOnly.TryParseExact(m.Value, "yyyy-MM-dd", out var d) ? d : null, m.Value, order, issues));

        foreach (var datePattern in rules.Dates)
        {
            masked = Consume(masked, datePattern, m =>
                CheckDate(ClaimPatterns.ToDate(m, rules, order), m.Value, order, issues));
        }

        masked = Consume(masked, rules.Money, m =>
        {
            var amount = ClaimPatterns.ParseAmount(m.Groups["amount"].Value, language);
            if (amount != order.Total)
            {
                issues.Add($"Amount '{m.Value.Trim()}' has no source in the order record (total is {order.Total:0.00} {order.Currency}).");
            }
        });

        masked = Consume(masked, rules.RelativeTime, m =>
            issues.Add($"Time commitment '{m.Value.Trim()}' cannot be verified against the record."));

        foreach (var (pattern, status) in rules.Statuses)
        {
            var current = masked;
            masked = Consume(masked, pattern, m =>
            {
                var negated = rules.NegationBefore.IsMatch(current[..m.Index]);
                if (!StatusClaimHolds(status, negated, order.Status))
                {
                    issues.Add($"Status claim '{(negated ? "not " : "")}{m.Value.Trim()}' contradicts the record (order is {order.Status}).");
                }
            });
        }

        // Anything numeric left over is a claim we could not parse: escalate rather than guess.
        foreach (Match m in ClaimPatterns.LeftoverNumber.Matches(masked))
        {
            issues.Add($"Unverifiable number '{m.Value}'.");
        }

        return new FactCheckResult(issues);
    }

    // Shipped → Delivered is a progression: "it shipped on the 3rd and was delivered on the 7th"
    // is true for a delivered order. "Not yet shipped" is only true before shipping.
    private static int Progress(OrderStatus s) => s switch
    {
        OrderStatus.Processing => 0,
        OrderStatus.Shipped => 1,
        OrderStatus.Delivered => 2,
        _ => -1,
    };

    internal static bool StatusClaimHolds(OrderStatus claimed, bool negated, OrderStatus actual)
    {
        if (claimed == OrderStatus.Cancelled || actual == OrderStatus.Cancelled)
        {
            return negated ? actual != claimed : actual == claimed;
        }

        if (claimed == OrderStatus.Processing)
        {
            return negated ? actual != OrderStatus.Processing : actual == OrderStatus.Processing;
        }

        return negated ? Progress(actual) < Progress(claimed) : Progress(actual) >= Progress(claimed);
    }

    private static void CheckDate(DateOnly? date, string raw, Order order, List<string> issues)
    {
        if (date is null)
        {
            issues.Add($"'{raw.Trim()}' is not a valid date.");
        }
        else if (date != order.ShippedOn && date != order.EstimatedDelivery)
        {
            issues.Add($"Date '{raw.Trim()}' does not match any date in the order record.");
        }
    }

    private static string Consume(string text, Regex regex, Action<Match> onMatch)
    {
        foreach (Match m in regex.Matches(text))
        {
            onMatch(m);
        }

        return regex.Replace(text, " ");
    }
}

/// <summary>Per-language claim patterns. Adding a language means adding one instance here.</summary>
internal sealed class ClaimPatterns
{
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static readonly Regex OrderRef = new(@"#(?<id>[A-Z0-9][A-Z0-9-]*)", Opts);
    public static readonly Regex IsoDate = new(@"\b\d{4}-\d{2}-\d{2}\b", Opts);
    public static readonly Regex LeftoverNumber = new(@"\d+(?:[.,]\d+)?", Opts);

    public required IReadOnlyList<Regex> Dates { get; init; }
    public required IReadOnlyDictionary<string, int> Months { get; init; }
    public required Regex Money { get; init; }
    public required Regex RelativeTime { get; init; }
    public required IReadOnlyList<(Regex Pattern, OrderStatus Status)> Statuses { get; init; }

    /// <summary>A negation ending right before a status phrase (at most two words in between).</summary>
    public required Regex NegationBefore { get; init; }

    public static ClaimPatterns For(Language language) => language == Language.Es ? Spanish : English;

    private static readonly ClaimPatterns English = new()
    {
        Months = MonthMap("january february march april may june july august september october november december", prefix: 3),
        Dates =
        [
            new(@"\b(?<month>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\.?\s+(?<day>\d{1,2})(?:st|nd|rd|th)?(?:,?\s+(?<year>\d{4}))?\b", Opts),
        ],
        Money = new(@"(?:[$€£]\s?(?<amount>\d[\d,]*(?:\.\d{1,2})?))|(?:(?<amount>\d[\d,]*(?:\.\d{1,2})?)\s?(?:USD|EUR|dollars?|euros?)\b)", Opts),
        RelativeTime = new(@"\b(?:today|tonight|tomorrow|right away|immediately|asap|as soon as possible|this week|within\s+\w+(?:\s+\w+)?|in\s+(?:a|an|one|two|three|four|five|a few|a couple of)\s+(?:hours?|days?|weeks?)|next\s+(?:week|month|business day|monday|tuesday|wednesday|thursday|friday)|by\s+(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday|end of (?:day|week)))\b", Opts),
        NegationBefore = new(@"(?:\bnot|n't|\bnever)(?:\s+(?:yet|been|be|have|has|was|is|it|already))*\s*$", Opts),
        Statuses =
        [
            (new(@"\b(?:has been |was |is )?(?:shipped|dispatched)\b", Opts), OrderStatus.Shipped),
            (new(@"\b(?:has been |was )?delivered\b", Opts), OrderStatus.Delivered),
            (new(@"\b(?:is )?(?:being prepared|being processed|processing)\b", Opts), OrderStatus.Processing),
            (new(@"\b(?:has been |was )?cancell?ed\b", Opts), OrderStatus.Cancelled),
        ],
    };

    private static readonly ClaimPatterns Spanish = new()
    {
        Months = MonthMap("enero febrero marzo abril mayo junio julio agosto septiembre octubre noviembre diciembre", prefix: 0),
        Dates =
        [
            new(@"\b(?<day>\d{1,2})\s+de\s+(?<month>enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)(?:\s+(?:de|del)\s+(?<year>\d{4}))?\b", Opts),
            new(@"\b(?<day>\d{1,2})/(?<monthnum>\d{1,2})(?:/(?<year>\d{4}))?\b", Opts),
        ],
        // "$1.500", "$89,90", "89,90 USD", "1.500 pesos", "20 €"
        Money = new(@"(?:[$€£]\s?(?<amount>\d[\d.]*(?:,\d{1,2})?))|(?:(?<amount>\d[\d.]*(?:,\d{1,2})?)\s?(?:USD|EUR|€|d[oó]lares|euros|pesos)(?!\w))", Opts),
        RelativeTime = new(@"(?<!\w)(?:hoy|esta noche|mañana|pasado mañana|de inmediato|inmediatamente|enseguida|lo antes posible|cuanto antes|esta semana|dentro de\s+\w+(?:\s+\w+)?|en\s+(?:un|una|uno|dos|tres|cuatro|cinco|unos|unas|pocos|pocas)\s+(?:horas?|d[ií]as?|semanas?)|la\s+pr[oó]xima\s+semana|el\s+pr[oó]ximo\s+(?:lunes|martes|mi[eé]rcoles|jueves|viernes|mes)|antes\s+del?\s+(?:lunes|martes|mi[eé]rcoles|jueves|viernes|s[aá]bado|domingo|fin de semana))(?!\w)", Opts),
        NegationBefore = new(@"(?<!\w)(?:no|nunca|jam[aá]s)(?:\s+(?:se|ha|han|ha sido|fue|está|esta|todavía|aún|aun|lo|le|sido))*\s*$", Opts),
        Statuses =
        [
            (new(@"(?<!\w)(?:se\s+despach[oó]|(?:fue|ha sido|est[aá])\s+(?:despachado|enviado)|despachado|enviado)(?!\w)", Opts), OrderStatus.Shipped),
            (new(@"(?<!\w)(?:se\s+entreg[oó]|(?:fue|ha sido)\s+entregado|entregado)(?!\w)", Opts), OrderStatus.Delivered),
            (new(@"(?<!\w)(?:en\s+preparaci[oó]n|se\s+est[aá]\s+(?:preparando|procesando)|en\s+proceso)(?!\w)", Opts), OrderStatus.Processing),
            (new(@"(?<!\w)(?:se\s+cancel[oó]|(?:fue|ha sido)\s+cancelado|cancelado)(?!\w)", Opts), OrderStatus.Cancelled),
        ],
    };

    public static DateOnly? ToDate(Match m, ClaimPatterns rules, Order order)
    {
        int month;
        if (m.Groups["monthnum"].Success)
        {
            month = int.Parse(m.Groups["monthnum"].Value, CultureInfo.InvariantCulture);
        }
        else
        {
            var name = m.Groups["month"].Value.ToLowerInvariant();
            if (name == "setiembre")
            {
                name = "septiembre";
            }

            if (!rules.Months.TryGetValue(name, out month) && !rules.Months.TryGetValue(name[..Math.Min(3, name.Length)], out month))
            {
                return null;
            }
        }

        var day = int.Parse(m.Groups["day"].Value, CultureInfo.InvariantCulture);
        var year = m.Groups["year"].Success
            ? int.Parse(m.Groups["year"].Value, CultureInfo.InvariantCulture)
            : (order.ShippedOn ?? order.EstimatedDelivery)?.Year ?? DateTime.UtcNow.Year;

        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month)
            ? new DateOnly(year, month, day)
            : null;
    }

    /// <summary>English: "1,500.50". Spanish: "1.500,50" (a dot followed by exactly 3 digits is a thousands separator).</summary>
    public static decimal ParseAmount(string raw, Language language)
    {
        var normalized = language == Language.Es
            ? Regex.Replace(raw, @"\.(?=\d{3}(?:\D|$))", "").Replace(',', '.')
            : raw.Replace(",", "", StringComparison.Ordinal);
        return decimal.Parse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, int> MonthMap(string names, int prefix)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var list = names.Split(' ');
        for (var i = 0; i < list.Length; i++)
        {
            map[list[i]] = i + 1;
            if (prefix > 0)
            {
                map[list[i][..prefix]] = i + 1;
            }
        }
        return map;
    }
}
