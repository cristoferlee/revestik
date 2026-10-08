using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Revestik.Api.Services.BankVouchers;

public sealed partial class BancoNacionalBankVoucherEmailParser
    : IBankVoucherEmailParser
{
    private static readonly string[] MerchantLabels =
    [
        "Comercio",
        "Establecimiento",
        "Afiliado"
    ];

    private static readonly string[] TransactionDateLabels =
    [
        "Fecha de transacción",
        "Fecha de la transacción",
        "Fecha transacción",
        "Fecha"
    ];

    private static readonly string[] CardLabels =
    [
        "Tarjeta",
        "Número de tarjeta"
    ];

    private static readonly string[] AuthorizationLabels =
    [
        "Autorización",
        "Número de autorización",
        "NRO. AUT",
        "NRO AUT"
    ];

    private static readonly string[] ReferenceLabels =
    [
        "Referencia",
        "Número de referencia",
        "REF"
    ];

    private static readonly string[] TotalLabels =
    [
        "Total",
        "Monto total",
        "Monto"
    ];

    private static readonly string[] CurrencyLabels =
    [
        "Moneda"
    ];

    private static readonly Dictionary<string, int> EnglishMonths =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Jan"] = 1,
            ["Feb"] = 2,
            ["Mar"] = 3,
            ["Apr"] = 4,
            ["May"] = 5,
            ["Jun"] = 6,
            ["Jul"] = 7,
            ["Aug"] = 8,
            ["Sep"] = 9,
            ["Oct"] = 10,
            ["Nov"] = 11,
            ["Dec"] = 12
        };

    public bool TryParse(
        string? subject,
        string body,
        out ParsedBankVoucher? voucher)
    {
        voucher = null;

        if (string.IsNullOrWhiteSpace(body))
            return false;

        var text = NormalizeBody(body);
        var lines = SplitLines(text);

        if (TryParseBancoNacionalVoucher(lines, out voucher))
            return true;

        return TryParseLabeledVoucher(lines, out voucher);
    }

    private static bool TryParseBancoNacionalVoucher(
        IReadOnlyList<string> lines,
        out ParsedBankVoucher? voucher)
    {
        voucher = null;

        for (var index = 1; index < lines.Count - 1; index++)
        {
            if (!TryParseBancoNacionalDate(
                    lines[index],
                    out var transactionDate))
            {
                continue;
            }

            var merchant = NormalizeMerchant(lines[index - 1]);
            if (string.IsNullOrWhiteSpace(merchant))
                continue;

            string? cardValue = null;

            for (var cardIndex = index + 1;
                 cardIndex < Math.Min(lines.Count, index + 4);
                 cardIndex++)
            {
                if (!CardBrandRegex().IsMatch(lines[cardIndex]))
                    continue;

                cardValue = lines[cardIndex];
                break;
            }

            var authorization = FindValue(
                lines,
                AuthorizationLabels);

            var reference = FindValue(
                lines,
                ReferenceLabels);

            var totalValue = FindValue(
                lines,
                TotalLabels);

            if (string.IsNullOrWhiteSpace(totalValue))
                return false;

            if (!TryParseAmount(totalValue, out var amount) ||
                amount <= 0m)
            {
                return false;
            }

            var currency = ParseCurrency(
                FindValue(lines, CurrencyLabels),
                totalValue);

            if (currency is null)
                return false;

            ParseCard(
                cardValue,
                out var cardBrand,
                out var cardLastFour);

            voucher = new ParsedBankVoucher(
                "Banco Nacional",
                merchant,
                amount,
                currency,
                transactionDate,
                cardBrand,
                cardLastFour,
                NormalizeOptional(authorization),
                NormalizeOptional(reference));

            return true;
        }

        return false;
    }

    private static bool TryParseLabeledVoucher(
        IReadOnlyList<string> lines,
        out ParsedBankVoucher? voucher)
    {
        voucher = null;

        var merchant = FindValue(lines, MerchantLabels);
        var transactionDateValue =
            FindValue(lines, TransactionDateLabels);
        var cardValue = FindValue(lines, CardLabels);
        var authorization = FindValue(
            lines,
            AuthorizationLabels);
        var reference = FindValue(
            lines,
            ReferenceLabels);
        var totalValue = FindValue(lines, TotalLabels);
        var currencyValue = FindValue(lines, CurrencyLabels);

        if (string.IsNullOrWhiteSpace(merchant) ||
            string.IsNullOrWhiteSpace(transactionDateValue) ||
            string.IsNullOrWhiteSpace(totalValue))
        {
            return false;
        }

        if (!TryParseTransactionDate(
                transactionDateValue,
                out var transactionDate))
        {
            return false;
        }

        if (!TryParseAmount(totalValue, out var amount) ||
            amount <= 0m)
        {
            return false;
        }

        var currency = ParseCurrency(
            currencyValue,
            totalValue);

        if (currency is null)
            return false;

        ParseCard(
            cardValue,
            out var cardBrand,
            out var cardLastFour);

        voucher = new ParsedBankVoucher(
            "Banco Nacional",
            NormalizeMerchant(merchant),
            amount,
            currency,
            transactionDate,
            cardBrand,
            cardLastFour,
            NormalizeOptional(authorization),
            NormalizeOptional(reference));

        return true;
    }

    internal static string NormalizeBody(string body)
    {
        var value = WebUtility.HtmlDecode(body);

        value = ScriptAndStyleRegex().Replace(value, " ");
        value = BreakLikeTagRegex().Replace(value, "\n");
        value = AnyHtmlTagRegex().Replace(value, "\n");
        value = WebUtility.HtmlDecode(value)
            .Replace('\u00A0', ' ');

        var normalizedLines = value
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line =>
                InlineWhitespaceRegex()
                    .Replace(line, " ")
                    .Trim())
            .Where(line => line.Length > 0);

        return string.Join('\n', normalizedLines);
    }

    private static IReadOnlyList<string> SplitLines(string text) =>
        text.Split(
            '\n',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

    private static string? FindValue(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> labels)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];

            foreach (var label in labels)
            {
                var normalizedLine =
                    NormalizeForComparison(line);

                var normalizedLabel =
                    NormalizeForComparison(label);

                if (!normalizedLine.StartsWith(
                        normalizedLabel,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf(':');
                var remainder = separatorIndex >= 0
                    ? line[(separatorIndex + 1)..].Trim()
                    : line.Length > label.Length
                        ? line[label.Length..]
                            .Trim()
                            .TrimStart('-', '–', '—')
                            .Trim()
                        : string.Empty;

                if (!string.IsNullOrWhiteSpace(remainder))
                    return remainder;

                if (index + 1 < lines.Count)
                    return lines[index + 1].Trim();
            }
        }

        return null;
    }

    private static bool TryParseBancoNacionalDate(
        string value,
        out DateTimeOffset transactionDate)
    {
        transactionDate = default;

        var match = BancoNacionalDateRegex().Match(value);

        if (!match.Success)
            return false;

        if (!EnglishMonths.TryGetValue(
                match.Groups["month"].Value,
                out var month))
        {
            return false;
        }

        if (!int.TryParse(
                match.Groups["day"].Value,
                CultureInfo.InvariantCulture,
                out var day) ||
            !int.TryParse(
                match.Groups["year"].Value,
                CultureInfo.InvariantCulture,
                out var year) ||
            !int.TryParse(
                match.Groups["hour"].Value,
                CultureInfo.InvariantCulture,
                out var hour) ||
            !int.TryParse(
                match.Groups["minute"].Value,
                CultureInfo.InvariantCulture,
                out var minute))
        {
            return false;
        }

        var period = match.Groups["period"]
            .Value
            .ToLowerInvariant();

        if (hour is < 0 or > 12 ||
            minute is < 0 or > 59)
        {
            return false;
        }

        if (period == "p" && hour < 12)
            hour += 12;
        else if (period == "a" && hour == 12)
            hour = 0;

        try
        {
            var localDateTime = new DateTime(
                year,
                month,
                day,
                hour,
                minute,
                0,
                DateTimeKind.Unspecified);

            transactionDate = new DateTimeOffset(
                localDateTime,
                TimeSpan.FromHours(-6));

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool TryParseTransactionDate(
        string value,
        out DateTimeOffset transactionDate)
    {
        if (TryParseBancoNacionalDate(
                value,
                out transactionDate))
        {
            return true;
        }

        transactionDate = default;

        var normalized = value
            .Replace(
                "a. m.",
                "AM",
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "a.m.",
                "AM",
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "p. m.",
                "PM",
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "p.m.",
                "PM",
                StringComparison.OrdinalIgnoreCase);

        normalized = InlineWhitespaceRegex()
            .Replace(normalized, " ")
            .Trim();

        var formats = new[]
        {
            "dd/MM/yyyy h:mm tt",
            "dd/MM/yyyy hh:mm tt",
            "dd/MM/yyyy H:mm",
            "dd/MM/yyyy HH:mm",
            "d/M/yyyy h:mm tt",
            "d/M/yyyy H:mm"
        };

        if (!DateTime.TryParseExact(
                normalized,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var localDateTime))
        {
            return false;
        }

        transactionDate = new DateTimeOffset(
            DateTime.SpecifyKind(
                localDateTime,
                DateTimeKind.Unspecified),
            TimeSpan.FromHours(-6));

        return true;
    }

    private static bool TryParseAmount(
        string value,
        out decimal amount)
    {
        amount = 0m;

        var numeric = AmountCharactersRegex()
            .Replace(value, string.Empty)
            .Trim();

        if (numeric.Length == 0)
            return false;

        var comma = numeric.LastIndexOf(',');
        var dot = numeric.LastIndexOf('.');

        if (comma >= 0 && dot >= 0)
        {
            if (comma > dot)
            {
                numeric = numeric
                    .Replace(".", string.Empty)
                    .Replace(',', '.');
            }
            else
            {
                numeric = numeric
                    .Replace(",", string.Empty);
            }
        }
        else if (comma >= 0)
        {
            var decimalDigits =
                numeric.Length - comma - 1;

            numeric = decimalDigits == 2
                ? numeric.Replace(',', '.')
                : numeric.Replace(",", string.Empty);
        }
        else if (dot >= 0)
        {
            var decimalDigits =
                numeric.Length - dot - 1;

            if (decimalDigits != 2)
                numeric = numeric.Replace(".", string.Empty);
        }

        return decimal.TryParse(
            numeric,
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out amount);
    }

    private static string? ParseCurrency(
        string? currencyValue,
        string totalValue)
    {
        var source =
            $"{currencyValue} {totalValue}"
                .ToUpperInvariant();

        if (source.Contains(
                "CRC",
                StringComparison.Ordinal) ||
            source.Contains('₡'))
        {
            return "CRC";
        }

        if (source.Contains(
                "USD",
                StringComparison.Ordinal))
        {
            return "USD";
        }

        return null;
    }

    private static void ParseCard(
        string? value,
        out string? cardBrand,
        out string? cardLastFour)
    {
        cardBrand = null;
        cardLastFour = null;

        if (string.IsNullOrWhiteSpace(value))
            return;

        var brandMatch = CardBrandRegex().Match(value);

        if (brandMatch.Success)
        {
            cardBrand = brandMatch.Value
                .ToUpperInvariant();
        }

        var digits = DigitsRegex()
            .Matches(value)
            .Select(match => match.Value)
            .SelectMany(item => item)
            .ToArray();

        if (digits.Length >= 4)
            cardLastFour = new string(digits[^4..]);
    }

    private static string NormalizeMerchant(string value) =>
        InlineWhitespaceRegex()
            .Replace(value, " ")
            .Trim();

    private static string? NormalizeOptional(
        string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private static string NormalizeForComparison(
        string value)
    {
        var decomposed =
            value.Normalize(NormalizationForm.FormD);

        var builder =
            new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                UnicodeCategory.NonSpacingMark)
            {
                builder.Append(
                    char.ToLowerInvariant(character));
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .Trim();
    }

    [GeneratedRegex(
        @"^(?<month>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+(?<day>\d{1,2}),\s+(?<year>\d{4})\s*-\s*(?<hour>\d{1,2}):(?<minute>\d{2})\s*(?<period>[ap])\.?\s*m\.?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex BancoNacionalDateRegex();

    [GeneratedRegex(
        @"<(script|style)\b[^>]*>.*?</\1>",
        RegexOptions.IgnoreCase |
        RegexOptions.Singleline)]
    private static partial Regex ScriptAndStyleRegex();

    [GeneratedRegex(
        @"</?(br|p|div|tr|td|th|li|table|section|article|header|footer)\b[^>]*>",
        RegexOptions.IgnoreCase)]
    private static partial Regex BreakLikeTagRegex();

    [GeneratedRegex(
        @"<[^>]+>",
        RegexOptions.Singleline)]
    private static partial Regex AnyHtmlTagRegex();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex InlineWhitespaceRegex();

    [GeneratedRegex(@"[^0-9,.\-]")]
    private static partial Regex AmountCharactersRegex();

    [GeneratedRegex(
        @"VISA|MASTERCARD|AMEX|AMERICAN EXPRESS",
        RegexOptions.IgnoreCase)]
    private static partial Regex CardBrandRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex DigitsRegex();
}
