using System.Globalization;
using System.Text.RegularExpressions;

namespace Ofis360.Domain.Common;

/// <summary>
/// Tarih yardimcilari (kaynak: src/lib/date.ts).
/// Gun anahtari "yyyy-MM-dd" dizgisidir; takvim gunu saat diliminden bagimsizdir.
/// Kaynakta takvim gunu UTC gece yarisi Date idi; hedefte DateOnly / Oracle DATE'e karsilik gelir.
/// </summary>
public static partial class DayKeys
{
    public static readonly string[] TrMonths =
        ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"];

    public static readonly string[] TrDays = ["Pazar", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi"];
    public static readonly string[] TrDaysShort = ["Paz", "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt"];

    private const long MillisPerDay = 86_400_000;

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")]
    private static partial Regex DayKeyPattern();

    [GeneratedRegex(@"^([01]\d|2[0-3]):([0-5]\d)$")]
    private static partial Regex TimePattern();

    private static string Pad(int value) => value.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>Verilen gun anahtarindan UTC gece yarisi DateTime uretir (kaynak: dayKeyToDate).</summary>
    public static DateTime ToUtcDate(string dayKey)
    {
        var parts = dayKey.Split('-');
        var year = int.Parse(parts[0], CultureInfo.InvariantCulture);
        var month = parts.Length > 1 && parts[1].Length > 0 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 1;
        var day = parts.Length > 2 && parts[2].Length > 0 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 1;
        // JS Date.UTC tasan ay/gun degerlerini ileri sarar; isValidDayKey bu davranisa dayanir.
        var date = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return date.AddMonths(month - 1).AddDays(day - 1);
    }

    /// <summary>DateTime'i (UTC bazli) gun anahtarina cevirir (kaynak: dateToDayKey).</summary>
    public static string FromUtcDate(DateTime date)
    {
        var utc = date.Kind == DateTimeKind.Utc ? date : date.ToUniversalTime();
        return $"{utc.Year}-{Pad(utc.Month)}-{Pad(utc.Day)}";
    }

    public static DateOnly ToDateOnly(string dayKey) => DateOnly.FromDateTime(ToUtcDate(dayKey));

    public static string FromDateOnly(DateOnly date) => $"{date.Year}-{Pad(date.Month)}-{Pad(date.Day)}";

    /// <summary>Verilen yerel "simdi" degerine gore bugunun anahtari (kaynak: todayKey).</summary>
    public static string Today(DateTime localNow) => $"{localNow.Year}-{Pad(localNow.Month)}-{Pad(localNow.Day)}";

    public static string AddDays(string dayKey, int days) => FromUtcDate(ToUtcDate(dayKey).AddDays(days));

    public static int DiffInDays(string fromKey, string toKey)
    {
        var from = ToUtcDate(fromKey).Ticks / TimeSpan.TicksPerMillisecond;
        var to = ToUtcDate(toKey).Ticks / TimeSpan.TicksPerMillisecond;
        return (int)Math.Round((double)(to - from) / MillisPerDay, MidpointRounding.AwayFromZero);
    }

    public static bool IsValid(object? value)
    {
        if (value is not string text) return false;
        if (!DayKeyPattern().IsMatch(text)) return false;
        // Asiri tarihler sonraki tarih aritmetiginde tasma uretir.
        if (string.CompareOrdinal(text, "2000-01-01") < 0 || string.CompareOrdinal(text, "2100-01-01") >= 0) return false;
        try
        {
            return FromUtcDate(ToUtcDate(text)) == text;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>0 = Pazar ... 6 = Cumartesi</summary>
    public static int WeekdayIndex(string dayKey) => (int)ToUtcDate(dayKey).DayOfWeek;

    public static bool IsWeekend(string dayKey)
    {
        var index = WeekdayIndex(dayKey);
        return index == 0 || index == 6;
    }

    /// <summary>Haftanin ilk gunu (Pazartesi) anahtarini dondurur.</summary>
    public static string StartOfWeek(string dayKey)
    {
        var index = WeekdayIndex(dayKey);
        var offset = index == 0 ? -6 : 1 - index;
        return AddDays(dayKey, offset);
    }

    public static string EndOfWeek(string dayKey) => AddDays(StartOfWeek(dayKey), 6);

    public static IReadOnlyList<string> Range(string startKey, string endKey)
    {
        var result = new List<string>();
        var total = DiffInDays(startKey, endKey);
        for (var i = 0; i <= total; i++)
        {
            result.Add(AddDays(startKey, i));
        }
        return result;
    }

    /// <summary>"21 Ağustos 2026 Cuma" bicimi.</summary>
    public static string FormatLong(string dayKey, bool withWeekday = true)
    {
        var date = ToUtcDate(dayKey);
        var baseText = $"{date.Day} {TrMonths[date.Month - 1]} {date.Year}";
        return withWeekday ? $"{baseText} {TrDays[(int)date.DayOfWeek]}" : baseText;
    }

    /// <summary>"21 Ağu" kisa bicimi.</summary>
    public static string FormatShort(string dayKey)
    {
        var date = ToUtcDate(dayKey);
        var month = TrMonths[date.Month - 1];
        return $"{date.Day} {(month.Length > 3 ? month[..3] : month)}";
    }

    public static string WeekdayLabel(string dayKey, bool shortForm = false)
    {
        var index = WeekdayIndex(dayKey);
        return shortForm ? TrDaysShort[index] : TrDays[index];
    }

    public static string MonthLabel(int monthIndex) =>
        monthIndex >= 0 && monthIndex < TrMonths.Length ? TrMonths[monthIndex] : string.Empty;

    /// <summary>Dakika degerini "09:30" bicimine cevirir.</summary>
    public static string MinutesToTime(int minutes) => $"{Pad(minutes / 60)}:{Pad(minutes % 60)}";

    /// <summary>"09:30" degerini dakikaya cevirir.</summary>
    public static int TimeToMinutes(string time)
    {
        var parts = time.Split(':');
        var hours = parts.Length > 0 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h) ? h : 0;
        var minutes = parts.Length > 1 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? m : 0;
        return hours * 60 + minutes;
    }

    public static bool IsValidTime(object? value) => value is string text && TimePattern().IsMatch(text);

    /// <summary>Gun anahtari + dakika degerinden UTC tabanli tam zaman uretir.</summary>
    public static DateTime WithMinutes(string dayKey, int minutes) => ToUtcDate(dayKey).AddMinutes(minutes);

    /// <summary>Belirli gun icin su anki dakika; gun gecmisteyse buyuk (1441), gelecekteyse -1 doner.</summary>
    public static int CurrentMinutesForDay(string dayKey, DateTime localNow)
    {
        var today = Today(localNow);
        if (string.CompareOrdinal(dayKey, today) > 0) return -1;
        if (string.CompareOrdinal(dayKey, today) < 0) return 24 * 60 + 1;
        return localNow.Hour * 60 + localNow.Minute;
    }

    public static string FormatDateTimeLabel(DateTime utcDate)
    {
        var utc = utcDate.Kind == DateTimeKind.Utc ? utcDate : utcDate.ToUniversalTime();
        var minutes = utc.Hour * 60 + utc.Minute;
        return $"{FormatLong(FromUtcDate(utc), withWeekday: false)} {MinutesToTime(minutes)}";
    }

    /// <summary>Bildirim listelerinde kullanilan "3 saat önce" bicimi.</summary>
    public static string RelativeTimeLabel(DateTimeOffset date, DateTimeOffset now)
    {
        var diffMs = (now - date).TotalMilliseconds;
        var minutes = (long)Math.Round(diffMs / 60_000, MidpointRounding.AwayFromZero);
        if (minutes < 1) return "Az önce";
        if (minutes < 60) return $"{minutes} dakika önce";
        var hours = (long)Math.Round(minutes / 60.0, MidpointRounding.AwayFromZero);
        if (hours < 24) return $"{hours} saat önce";
        var days = (long)Math.Round(hours / 24.0, MidpointRounding.AwayFromZero);
        if (days < 30) return $"{days} gün önce";
        var months = (long)Math.Round(days / 30.0, MidpointRounding.AwayFromZero);
        return $"{months} ay önce";
    }
}
