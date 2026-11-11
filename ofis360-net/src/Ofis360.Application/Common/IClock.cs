namespace Ofis360.Application.Common;

/// <summary>
/// "Bugun" kavrami kaynakta sunucu yerel saatiydi. Hedefte Europe/Istanbul sabitlenir ve
/// testlerde degistirilebilir.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
    TimeZoneInfo TimeZone { get; }
    DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, TimeZone).DateTime);
    int CurrentMinutesOfDay
    {
        get
        {
            var local = TimeZoneInfo.ConvertTime(UtcNow, TimeZone);
            return local.Hour * 60 + local.Minute;
        }
    }
}

public sealed class SystemClock : IClock
{
    public static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public TimeZoneInfo TimeZone => Istanbul;
}

/// <summary>Testler icin sabitlenebilir saat.</summary>
public sealed class FixedClock(DateTimeOffset utcNow, TimeZoneInfo? timeZone = null) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
    public TimeZoneInfo TimeZone { get; } = timeZone ?? SystemClock.Istanbul;
}
