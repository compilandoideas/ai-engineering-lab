using System.Globalization;
using System.Text.RegularExpressions;
using HitlSendReply.Domain;

namespace HitlSendReply.Policy;

public sealed record FactCheckResult(IReadOnlyList<string> Issues)
{
    public bool Verified => Issues.Count == 0;
}

/// <summary>
/// Extracts the claims a reply makes about the order (order id, dates, amounts, status) and
/// compares them with the system of record. It does not trust the model to declare its claims.
/// Anything it cannot extract or verify is reported as an issue, which sends the draft to a human.
/// </summary>
/// <remarks>
/// Deliberately narrow: it checks that a stated value exists in the record, not that it is
/// attached to the right field ("shipped on" vs "arrives on"). That gap is covered by the judge
/// and documented as a limitation.
/// </remarks>
public static partial class FactChecker
{
    private static readonly (Regex Pattern, OrderStatus Status)[] StatusPhrases =
    [
        (StatusRegex(@"\b(?:has been |was |is )?(?:shipped|dispatched)\b"), OrderStatus.Shipped),
        (StatusRegex(@"\b(?:has been |was )?delivered\b"), OrderStatus.Delivered),
        (StatusRegex(@"\b(?:is )?(?:being prepared|being processed|processing)\b"), OrderStatus.Processing),
        (StatusRegex(@"\b(?:has been |was )?(?:cancell?ed)\b"), OrderStatus.Cancelled),
    ];

    public static FactCheckResult Check(string text, Order order)
    {
        var issues = new List<string>();
        var masked = text;

        masked = Consume(masked, OrderRefRegex(), m =>
        {
            if (!string.Equals(m.Groups["id"].Value, order.Id, StringComparison.OrdinalIgnoreCase))
                issues.Add($"Order reference '{m.Value}' does not match the ticket's order #{order.Id}.");
        });

        masked = Consume(masked, IsoDateRegex(), m =>
        {
            var date = DateOnly.ParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            CheckDate(date, m.Value, order, issues);
        });

        masked = Consume(masked, MonthDayRegex(), m =>
        {
            var month = DateTime.ParseExact(m.Groups["month"].Value[..3], "MMM", CultureInfo.InvariantCulture).Month;
            var day = int.Parse(m.Groups["day"].Value, CultureInfo.InvariantCulture);
            var year = m.Groups["year"].Success
                ? int.Parse(m.Groups["year"].Value, CultureInfo.InvariantCulture)
                : (order.ShippedOn ?? order.EstimatedDelivery)?.Year ?? DateTime.UtcNow.Year;

            if (!DateOnly.TryParseExact($"{year:D4}-{month:D2}-{day:D2}", "yyyy-MM-dd", out var date))
            {
                issues.Add($"'{m.Value}' is not a valid date.");
                return;
            }

            CheckDate(date, m.Value, order, issues);
        });

        masked = Consume(masked, MoneyRegex(), m =>
        {
            var raw = m.Groups["amount"].Value.Replace(",", "", StringComparison.Ordinal);
            var amount = decimal.Parse(raw, CultureInfo.InvariantCulture);
            if (amount != order.Total)
                issues.Add($"Amount '{m.Value}' has no source in the order record (total is {order.Total:0.00} {order.Currency}).");
        });

        masked = Consume(masked, RelativeTimeRegex(), m =>
            issues.Add($"Time commitment '{m.Value}' cannot be verified against the record."));

        foreach (var (pattern, status) in StatusPhrases)
        {
            masked = Consume(masked, pattern, m =>
            {
                if (status != order.Status)
                    issues.Add($"Status claim '{m.Value.Trim()}' contradicts the record (order is {order.Status}).");
            });
        }

        // Anything numeric left over is a claim we could not parse: escalate rather than guess.
        foreach (Match m in LeftoverNumberRegex().Matches(masked))
            issues.Add($"Unverifiable number '{m.Value}'.");

        return new FactCheckResult(issues);
    }

    private static void CheckDate(DateOnly date, string raw, Order order, List<string> issues)
    {
        if (date != order.ShippedOn && date != order.EstimatedDelivery)
            issues.Add($"Date '{raw}' does not match any date in the order record.");
    }

    private static string Consume(string text, Regex regex, Action<Match> onMatch)
    {
        foreach (Match m in regex.Matches(text))
            onMatch(m);
        return regex.Replace(text, " ");
    }

    private static Regex StatusRegex(string pattern) =>
        new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"#(?<id>[A-Z0-9][A-Z0-9-]*)", RegexOptions.IgnoreCase)]
    private static partial Regex OrderRefRegex();

    [GeneratedRegex(@"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(@"\b(?<month>Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\.?\s+(?<day>\d{1,2})(?:st|nd|rd|th)?(?:,?\s+(?<year>\d{4}))?\b", RegexOptions.IgnoreCase)]
    private static partial Regex MonthDayRegex();

    [GeneratedRegex(@"(?:[$€£]\s?(?<amount>\d[\d,]*(?:\.\d{1,2})?))|(?:(?<amount>\d[\d,]*(?:\.\d{1,2})?)\s?(?:USD|EUR|dollars?|euros?)\b)", RegexOptions.IgnoreCase)]
    private static partial Regex MoneyRegex();

    [GeneratedRegex(@"\b(?:today|tonight|tomorrow|right away|immediately|asap|as soon as possible|within\s+\w+(?:\s+\w+)?|next\s+(?:week|month|business day)|by\s+(?:monday|tuesday|wednesday|thursday|friday|saturday|sunday|end of (?:day|week)))\b", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeTimeRegex();

    [GeneratedRegex(@"\d+(?:[.,]\d+)?")]
    private static partial Regex LeftoverNumberRegex();
}
