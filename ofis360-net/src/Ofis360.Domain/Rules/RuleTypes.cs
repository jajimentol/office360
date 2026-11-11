namespace Ofis360.Domain.Rules;

// Kaynak: src/domain/rules/types.ts

public sealed record RuleEvaluation(bool Allowed, IReadOnlyList<RuleViolation> Violations, IReadOnlyList<RuleViolation> Warnings);

public sealed record ToggleSetting(bool Enabled);

public sealed record AdvanceBookingSetting(bool Enabled, int Days);

public sealed record LimitSetting(bool Enabled, int Limit);

public sealed record PopularShareSetting(bool Enabled, int WindowSize, int MaxPopular);

public sealed record WorkingHoursSetting(bool Enabled, int StartMinutes, int EndMinutes);

public sealed record DurationSetting(bool Enabled, int Minutes);

public sealed record NoShowSetting(bool Enabled, int ToleranceMinutes, int StartMinutes);

/// <summary>Admin panelinden yonetilen kural ayarlarinin uygulama ici gorunumu.</summary>
public sealed record RuleSettings(
    ToggleSetting OneDeskPerDay,
    AdvanceBookingSetting AdvanceBookingDays,
    LimitSetting WeeklyDeskLimit,
    LimitSetting PopularDeskStreakLimit,
    PopularShareSetting PopularShareLimit,
    ToggleSetting AccessibleDeskRestriction,
    ToggleSetting NoPastDate,
    WorkingHoursSetting RoomWorkingHours,
    ToggleSetting RoomCapacityCheck,
    DurationSetting RoomMaxDurationMinutes,
    // Acikken hafta sonu ve resmi tatillerde rezervasyon yapilamaz.
    ToggleSetting WorkingDaysOnly,
    DurationSetting RoomSlotMinutes,
    NoShowSetting NoShowRelease,
    ToggleSetting RoomStartTracking);

public sealed record DeskRuleUser(string Id, string FullName, bool AccessibilityApproved, bool ActorIsSecretary = false);

public sealed record DeskRuleDesk(
    string Id,
    string Code,
    bool Active,
    bool Placed,
    bool AccessibleOnly,
    bool Popular,
    string FloorName,
    bool FloorReservable,
    string ZoneName,
    bool ZoneReservable,
    // Katin bagli oldugu bina calisan erisimine acik mi? null ise acik sayilir.
    bool? BuildingActive = null,
    string? BuildingName = null);

/// <summary>Kullanicinin gecmis/gelecek masa rezervasyonlarinin sadelestirilmis gorunumu.</summary>
public sealed record UserDeskHistoryItem(string DayKey, string DeskId, bool Popular);

public sealed record ClosureInfo(bool Active, string? Note = null, string? ScopeLabel = null)
{
    public static readonly ClosureInfo None = new(false);
}

public sealed record OccupiedBy(string UserId, string UserName);

public sealed record SameDayReservation(string DeskCode, string ReservationId);

public sealed record DeskRuleContext(
    string TodayKey,
    string DayKey,
    DeskRuleUser User,
    DeskRuleDesk Desk,
    ClosureInfo Closure,
    OccupiedBy? OccupiedBy,
    SameDayReservation? SameDayReservation,
    int WeeklyReservationCount,
    IReadOnlyList<UserDeskHistoryItem> History,
    WorkCalendar? Calendar = null);

public sealed record RoomRuleRoom(
    string Id,
    string Code,
    string Name,
    int Capacity,
    bool Active,
    string FloorName,
    bool FloorReservable,
    bool? BuildingActive = null,
    string? BuildingName = null);

public sealed record RoomConflict(string Id, string Title, int StartMinutes, int EndMinutes);

public sealed record RoomRuleContext(
    string TodayKey,
    string DayKey,
    // Ayni gun icin su anki dakika degeri; gelecekteki gunlerde -1.
    int NowMinutes,
    RoomRuleRoom Room,
    ClosureInfo Closure,
    int StartMinutes,
    int EndMinutes,
    int AttendeeCount,
    IReadOnlyList<RoomConflict> Conflicts,
    WorkCalendar? Calendar = null,
    RoomPolicy? Policy = null,
    RoomRequester? Requester = null);

public sealed record TimeRange(int StartMinutes, int EndMinutes);
