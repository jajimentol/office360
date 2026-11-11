namespace Ofis360.Domain.Common;

// Kaynak: src/lib/enums.ts. SQLite enum desteklemedigi icin kaynakta string sabitlerdi;
// hedefte de string sabit + Oracle CHECK kisitlari kullanilir (API sozlesmesi degismez).

public static class DeskReservationStatus
{
    public const string Booked = "BOOKED";
    public const string CheckedIn = "CHECKED_IN";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public const string NoShow = "NO_SHOW";

    public static readonly IReadOnlyList<string> All = [Booked, CheckedIn, Completed, Cancelled, NoShow];

    /// <summary>Masayi dolu sayan durumlar.</summary>
    public static readonly IReadOnlyList<string> Occupying = [Booked, CheckedIn, Completed, NoShow];

    /// <summary>Masayi fiilen tutan durumlar. "Gelmedi" sayilan masa serbest kalir.</summary>
    public static readonly IReadOnlyList<string> Holding = [Booked, CheckedIn, Completed];
}

public static class RoomReservationStatus
{
    public const string Booked = "BOOKED";
    public const string Completed = "COMPLETED";
    public const string Cancelled = "CANCELLED";
    public const string NoShow = "NO_SHOW";

    public static readonly IReadOnlyList<string> All = [Booked, Completed, Cancelled, NoShow];

    /// <summary>Odayi dolu sayan durumlar.</summary>
    public static readonly IReadOnlyList<string> Occupying = [Booked, Completed, NoShow];
}

public static class ReservationStatusLabels
{
    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>
    {
        ["BOOKED"] = "Rezerve",
        ["CHECKED_IN"] = "Giriş yapıldı",
        ["COMPLETED"] = "Tamamlandı",
        ["CANCELLED"] = "İptal edildi",
        ["NO_SHOW"] = "Kullanılmadı",
    };
}

public static class ReservationSources
{
    public const string Employee = "EMPLOYEE";
    public const string Admin = "ADMIN";
    public const string Seed = "SEED";

    public static readonly IReadOnlyList<string> All = [Employee, Admin, Seed];
}

public static class ZoneKinds
{
    public const string Work = "WORK";
    public const string Meeting = "MEETING";
    public const string Social = "SOCIAL";
    public const string Corridor = "CORRIDOR";
    public const string Core = "CORE";
    public const string Restricted = "RESTRICTED";

    public static readonly IReadOnlyList<string> All = [Work, Meeting, Social, Corridor, Core, Restricted];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Work] = "Çalışma alanı",
        [Meeting] = "Toplantı odası",
        [Social] = "Mutfak / sosyal alan",
        [Corridor] = "Koridor",
        [Core] = "Asansör ve merdiven",
        [Restricted] = "Rezervasyona kapalı alan",
    };
}

public static class ClosureScopes
{
    public const string Floor = "FLOOR";
    public const string Desk = "DESK";
    public const string Room = "ROOM";

    public static readonly IReadOnlyList<string> All = [Floor, Desk, Room];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Floor] = "Kat",
        [Desk] = "Masa",
        [Room] = "Toplantı odası",
    };
}

public static class ClosureReasons
{
    public const string Maintenance = "MAINTENANCE";
    public const string Event = "EVENT";
    public const string ReservedUse = "RESERVED_USE";
    public const string Other = "OTHER";

    public static readonly IReadOnlyList<string> All = [Maintenance, Event, ReservedUse, Other];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Maintenance] = "Bakım / onarım",
        [Event] = "Etkinlik",
        [ReservedUse] = "Özel kullanım",
        [Other] = "Diğer",
    };
}

/// <summary>Masa gorsel durumlari (kat planinda kullanilir).</summary>
public static class DeskViewStates
{
    public const string Available = "AVAILABLE";
    public const string Selected = "SELECTED";
    public const string Reserved = "RESERVED";
    public const string Closed = "CLOSED";
    public const string Accessible = "ACCESSIBLE";
    public const string Mine = "MINE";
    public const string RestrictedByRule = "RESTRICTED_BY_RULE";

    public static readonly IReadOnlyList<string> All =
        [Available, Selected, Reserved, Closed, Accessible, Mine, RestrictedByRule];
}

public static class NotificationTypes
{
    public const string DeskReservationCreated = "DESK_RESERVATION_CREATED";
    public const string DeskReservationCancelled = "DESK_RESERVATION_CANCELLED";
    public const string DeskReservationCancelledByAdmin = "DESK_RESERVATION_CANCELLED_BY_ADMIN";
    public const string RoomReservationCreated = "ROOM_RESERVATION_CREATED";
    public const string RoomReservationCancelled = "ROOM_RESERVATION_CANCELLED";
    public const string RoomReservationCancelledByAdmin = "ROOM_RESERVATION_CANCELLED_BY_ADMIN";
    public const string UpcomingReservationReminder = "UPCOMING_RESERVATION_REMINDER";
    public const string ResourceClosed = "RESOURCE_CLOSED";
    public const string DeskNoShowReleased = "DESK_NO_SHOW_RELEASED";
    public const string AlternativeDeskSuggestion = "ALTERNATIVE_DESK_SUGGESTION";
    public const string Announcement = "ANNOUNCEMENT";
}

public static class NotificationSeverities
{
    public const string Info = "INFO";
    public const string Success = "SUCCESS";
    public const string Warning = "WARNING";

    public static readonly IReadOnlyList<string> All = [Info, Success, Warning];
}

public static class AuditActions
{
    public const string Login = "AUTH_LOGIN";
    public const string Logout = "AUTH_LOGOUT";
    public const string DeskReservationCreate = "DESK_RESERVATION_CREATE";
    public const string DeskReservationCancel = "DESK_RESERVATION_CANCEL";
    public const string RoomReservationCreate = "ROOM_RESERVATION_CREATE";
    public const string RoomReservationCancel = "ROOM_RESERVATION_CANCEL";
    public const string BuildingCreate = "BUILDING_CREATE";
    public const string BuildingUpdate = "BUILDING_UPDATE";
    public const string FloorCreate = "FLOOR_CREATE";
    public const string FloorUpdate = "FLOOR_UPDATE";
    public const string ZoneUpdate = "ZONE_UPDATE";
    public const string DeskCreate = "DESK_CREATE";
    public const string DeskUpdate = "DESK_UPDATE";
    public const string DeskDelete = "DESK_DELETE";
    public const string RoomCreate = "ROOM_CREATE";
    public const string RoomUpdate = "ROOM_UPDATE";
    public const string RoomDelete = "ROOM_DELETE";
    public const string RoomPolicyUpdate = "ROOM_POLICY_UPDATE";
    public const string RuleUpdate = "RULE_UPDATE";
    public const string RuleMatrixUpdate = "RULE_MATRIX_UPDATE";
    public const string HolidayCreate = "HOLIDAY_CREATE";
    public const string HolidayDelete = "HOLIDAY_DELETE";
    public const string NoShowRun = "NO_SHOW_RUN";
    public const string UserCreate = "USER_CREATE";
    public const string UserUpdate = "USER_UPDATE";
    public const string UserImport = "USER_IMPORT";
    public const string ClosureCreate = "CLOSURE_CREATE";
    public const string ClosureUpdate = "CLOSURE_UPDATE";
    public const string SettingUpdate = "SETTING_UPDATE";
    public const string FloorPlanSave = "FLOOR_PLAN_SAVE";
    public const string ReportExport = "REPORT_EXPORT";

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Login] = "Oturum açıldı",
        [Logout] = "Oturum kapatıldı",
        [DeskReservationCreate] = "Masa rezervasyonu oluşturuldu",
        [DeskReservationCancel] = "Masa rezervasyonu iptal edildi",
        [RoomReservationCreate] = "Toplantı odası rezervasyonu oluşturuldu",
        [RoomReservationCancel] = "Toplantı odası rezervasyonu iptal edildi",
        [BuildingCreate] = "Bina eklendi",
        [BuildingUpdate] = "Bina güncellendi",
        [FloorCreate] = "Kat eklendi",
        [FloorUpdate] = "Kat güncellendi",
        [ZoneUpdate] = "Bölge güncellendi",
        [DeskCreate] = "Masa eklendi",
        [DeskUpdate] = "Masa güncellendi",
        [DeskDelete] = "Masa silindi",
        [RoomCreate] = "Toplantı odası eklendi",
        [RoomUpdate] = "Toplantı odası güncellendi",
        [RoomDelete] = "Toplantı odası silindi",
        [RoomPolicyUpdate] = "Oda kuralları güncellendi",
        [RuleUpdate] = "Rezervasyon kuralı güncellendi",
        [RuleMatrixUpdate] = "Kademe bazlı kurallar güncellendi",
        [HolidayCreate] = "Resmi tatil eklendi",
        [HolidayDelete] = "Resmi tatil silindi",
        [NoShowRun] = "Otomatik no-show işlemi",
        [UserCreate] = "Kullanıcı eklendi",
        [UserUpdate] = "Kullanıcı güncellendi",
        [UserImport] = "Kullanıcılar toplu yüklendi",
        [ClosureCreate] = "Kapatma kaydı oluşturuldu",
        [ClosureUpdate] = "Kapatma kaydı güncellendi",
        [SettingUpdate] = "Sistem ayarı güncellendi",
        [FloorPlanSave] = "Kat krokisi kaydedildi",
        [ReportExport] = "Rapor dışa aktarıldı",
    };
}

public static class RuleKeys
{
    public const string OneDeskPerDay = "ONE_DESK_PER_DAY";
    public const string AdvanceBookingDays = "ADVANCE_BOOKING_DAYS";
    public const string WeeklyDeskLimit = "WEEKLY_DESK_LIMIT";
    public const string PopularDeskStreakLimit = "POPULAR_DESK_STREAK_LIMIT";
    public const string PopularShareLimit = "POPULAR_SHARE_LIMIT";
    public const string AccessibleDeskRestriction = "ACCESSIBLE_DESK_RESTRICTION";
    public const string NoPastDate = "NO_PAST_DATE";
    public const string RoomWorkingHours = "ROOM_WORKING_HOURS";
    public const string RoomCapacityCheck = "ROOM_CAPACITY_CHECK";
    public const string RoomMaxDurationMinutes = "ROOM_MAX_DURATION_MINUTES";
    public const string WorkingDaysOnly = "WORKING_DAYS_ONLY";
    public const string RoomSlotMinutes = "ROOM_SLOT_MINUTES";
    public const string NoShowRelease = "NO_SHOW_RELEASE";
    public const string RoomStartTracking = "ROOM_START_TRACKING";
}

public static class RuleCategories
{
    public const string Desk = "DESK";
    public const string Room = "ROOM";
    public const string General = "GENERAL";

    public static readonly IReadOnlyList<string> All = [Desk, Room, General];
}

public static class ShortfallSources
{
    public const string System = "SYSTEM";
    public const string Seed = "SEED";

    public static readonly IReadOnlyList<string> All = [System, Seed];
}
