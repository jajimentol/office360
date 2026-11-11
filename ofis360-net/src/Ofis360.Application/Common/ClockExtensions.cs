using Ofis360.Domain.Common;

namespace Ofis360.Application.Common;

public static class ClockExtensions
{
    /// <summary>Kaynak: todayKey() — sunucu yerel saatine gore bugun. Hedefte Europe/Istanbul.</summary>
    public static string TodayKey(this IClock clock) => DayKeys.FromDateOnly(clock.Today);

    public static DateTime LocalNow(this IClock clock) => TimeZoneInfo.ConvertTime(clock.UtcNow, clock.TimeZone).DateTime;

    /// <summary>Kaynak: currentMinutesForDay(dayKey).</summary>
    public static int CurrentMinutesForDay(this IClock clock, string dayKey) => DayKeys.CurrentMinutesForDay(dayKey, clock.LocalNow());
}

/// <summary>Sayfali liste sonucu (kaynak: { items, total, take, skip }).</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Take, int Skip);
