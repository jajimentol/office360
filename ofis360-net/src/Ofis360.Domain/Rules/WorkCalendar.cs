using Ofis360.Domain.Common;

namespace Ofis360.Domain.Rules;

/// <summary>Is gunu hesabi: hafta sonu ve admin'in tanimladigi resmi tatiller is gunu sayilmaz.</summary>
public sealed class WorkCalendar(IEnumerable<string> holidayDayKeys)
{
    /// <summary>Tatil tanimi olmayan takvim: yalnizca hafta sonlari is gunu degildir.</summary>
    public static readonly WorkCalendar Default = new([]);

    private const int MaxScanDays = 400;

    private readonly HashSet<string> _holidays = holidayDayKeys.ToHashSet(StringComparer.Ordinal);

    public bool IsHoliday(string dayKey) => _holidays.Contains(dayKey);

    public bool IsWorkingDay(string dayKey) => !DayKeys.IsWeekend(dayKey) && !IsHoliday(dayKey);

    /// <summary>Bugunden sonraki N. is gunu; bugun sayilmaz. Cuma, 2 is gunu: Sali.</summary>
    public string LatestBookableDay(string todayKey, int workingDays)
    {
        var cursor = todayKey;
        var remaining = workingDays;
        for (var scanned = 0; remaining > 0 && scanned < MaxScanDays; scanned++)
        {
            cursor = DayKeys.AddDays(cursor, 1);
            if (IsWorkingDay(cursor)) remaining--;
        }
        return cursor;
    }

    /// <summary>Hedef gunun, "son N is gunu" kuraliyla rezerve edilebilir hale geldigi en erken gun.</summary>
    public string OpensOn(string dayKey, int workingDays)
    {
        var candidate = dayKey;
        for (var scanned = 0; scanned < MaxScanDays; scanned++)
        {
            var previous = DayKeys.AddDays(candidate, -1);
            if (string.CompareOrdinal(LatestBookableDay(previous, workingDays), dayKey) < 0) break;
            candidate = previous;
        }
        return candidate;
    }

    /// <summary>Verilen aralikta is gunu olmayan gunler (uclar dahil).</summary>
    public IReadOnlyList<string> NonWorkingDays(string startKey, string endKey) =>
        DayKeys.Range(startKey, endKey).Where(day => !IsWorkingDay(day)).ToList();
}
