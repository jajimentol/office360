using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ofis360.Domain.Common;

/// <summary>Kaynak: src/lib/utils.ts (cn haric; o Tailwind'e ozeldi).</summary>
public static partial class TextUtils
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("[\";\r\n]")]
    private static partial Regex CsvSpecial();

    /// <summary>Ad soyaddan bas harfleri uretir (avatar rozetleri icin).</summary>
    public static string InitialsOf(string fullName)
    {
        var parts = Whitespace().Split(fullName.Trim());
        if (parts.Length == 0) return string.Empty;
        if (parts.Length == 1)
        {
            var first = parts[0];
            return (first.Length > 2 ? first[..2] : first).ToUpper(Tr);
        }
        var a = parts[0].Length > 0 ? parts[0][..1] : string.Empty;
        var b = parts[^1].Length > 0 ? parts[^1][..1] : string.Empty;
        return (a + b).ToUpper(Tr);
    }

    /// <summary>Yuzde degerini "%73" bicimine cevirir.</summary>
    public static string FormatPercent(double value, int fractionDigits = 0)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "%0";
        return "%" + value.ToString("F" + fractionDigits, CultureInfo.InvariantCulture);
    }

    public static string FormatNumber(double value) => value.ToString("N0", Tr);

    /// <summary>0 bolen guvenli oran hesabi (yuzde olarak).</summary>
    public static double Ratio(double numerator, double denominator)
    {
        if (denominator == 0) return 0;
        return numerator / denominator * 100;
    }

    /// <summary>Basit CSV uretimi: degerler kacislanir, ';' ayraci, CRLF ve UTF-8 BOM (Excel uyumu).</summary>
    public static string ToCsv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
    {
        static string Escape(object? value)
        {
            if (value is null) return string.Empty;
            var text = value switch
            {
                double d => d.ToString(CultureInfo.InvariantCulture),
                float f => f.ToString(CultureInfo.InvariantCulture),
                decimal m => m.ToString(CultureInfo.InvariantCulture),
                _ => value.ToString() ?? string.Empty,
            };
            // Excel formul enjeksiyonu: metin hucresi formul gibi baslamasin.
            if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
            return CsvSpecial().IsMatch(text) ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
        }

        var sb = new StringBuilder();
        sb.Append('﻿');
        sb.Append(string.Join(";", headers.Select(Escape)));
        foreach (var row in rows)
        {
            sb.Append("\r\n");
            sb.Append(string.Join(";", row.Select(Escape)));
        }
        return sb.ToString();
    }
}

/// <summary>Deterministik sozde-rastgele sayi uretici (LCG; seed verilerinin tekrarlanabilir olmasi icin).</summary>
public sealed class SeededRandom(uint seed)
{
    private uint _state = seed;

    /// <summary>[0, 1] araliginda deger (kaynak: state / 0xffffffff).</summary>
    public double Next()
    {
        unchecked
        {
            _state = _state * 1_664_525u + 1_013_904_223u;
        }
        return _state / (double)0xffff_ffff;
    }
}

public static class Statistics
{
    /// <summary>Gini katsayisi (kaynak: analytics.service.ts computeGini).</summary>
    public static double ComputeGini(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        var total = sorted.Sum();
        if (total == 0) return 0;
        double cumulative = 0;
        for (var i = 0; i < sorted.Length; i++)
        {
            cumulative += (i + 1) * sorted[i];
        }
        return 2 * cumulative / (sorted.Length * total) - (double)(sorted.Length + 1) / sorted.Length;
    }

    public static double ComputeGini(IEnumerable<int> values) => ComputeGini(values.Select(v => (double)v).ToList());
}
