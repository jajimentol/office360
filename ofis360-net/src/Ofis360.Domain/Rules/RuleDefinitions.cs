using Ofis360.Domain.Common;

namespace Ofis360.Domain.Rules;

public sealed record RuleDefinition(
    string Key,
    string Name,
    string Description,
    string Category,
    bool Enabled,
    bool EditableLimit,
    int? IntValue,
    int? SecondValue,
    string? Unit,
    int? MinValue,
    int? MaxValue,
    bool SystemRule,
    int Order);

/// <summary>Veritabani satirlarinin kural motoru icin gereken alt kumesi.</summary>
public sealed record RuleRow(string Key, bool Enabled, int? IntValue, int? SecondValue);

/// <summary>Kademe (istege bagli bina) bazli deger. Null alan ust seviyeden gelir.</summary>
public sealed record RuleOverrideRow(string RuleKey, string Grade, string? BuildingId, bool? Enabled, int? IntValue);

/// <summary>
/// Varsayilan adil kullanim kurallari (kaynak: defaults.ts). Seed bunlari veritabanina yazar;
/// systemRule = true olan kurallar kapatilamaz.
/// </summary>
public static class RuleDefinitions
{
    public const string AdvanceBookingDescription = "Rezervasyonlar en fazla bu kadar iş günü öncesinden yapılabilir. Hafta sonu ve resmi tatiller sayılmaz.";
    public const string AdvanceBookingUnit = "iş günü";

    public static readonly IReadOnlyList<RuleDefinition> All =
    [
        new(RuleKeys.OneDeskPerDay, "Aynı gün tek masa",
            "Bir çalışan aynı tarih için yalnızca bir masa rezervasyonu oluşturabilir. Böylece masa stoklama önlenir.",
            RuleCategories.Desk, Enabled: true, EditableLimit: false, IntValue: 1, SecondValue: null, Unit: "masa/gün",
            MinValue: null, MaxValue: null, SystemRule: false, Order: 1),
        new(RuleKeys.AdvanceBookingDays, "Önceden rezervasyon penceresi",
            AdvanceBookingDescription,
            RuleCategories.General, Enabled: true, EditableLimit: true, IntValue: 14, SecondValue: null, Unit: AdvanceBookingUnit,
            MinValue: 1, MaxValue: 90, SystemRule: false, Order: 2),
        new(RuleKeys.WeeklyDeskLimit, "Haftalık masa rezervasyon limiti",
            "Bir çalışan bir hafta içinde (Pazartesi - Pazar) en fazla bu kadar masa rezervasyonu oluşturabilir.",
            RuleCategories.Desk, Enabled: true, EditableLimit: true, IntValue: 3, SecondValue: null, Unit: "rezervasyon/hafta",
            MinValue: 1, MaxValue: 7, SystemRule: false, Order: 3),
        new(RuleKeys.PopularDeskStreakLimit, "Popüler masa ardışık kullanım limiti",
            "Popüler olarak işaretlenen bir masa aynı çalışan tarafından arka arkaya en fazla bu kadar ofis gününde kullanılabilir.",
            RuleCategories.Desk, Enabled: true, EditableLimit: true, IntValue: 2, SecondValue: null, Unit: "ardışık ofis günü",
            MinValue: 1, MaxValue: 10, SystemRule: false, Order: 4),
        new(RuleKeys.PopularShareLimit, "Popüler masa kullanım payı",
            "Son N rezervasyonun belirlenen sayıdan fazlası popüler / pencere yanı masalarda geçtiyse, çalışanın popüler masa seçimi geçici olarak sınırlandırılır.",
            RuleCategories.Desk, Enabled: true, EditableLimit: true, IntValue: 10, SecondValue: 4, Unit: "son N rezervasyonda en fazla M popüler",
            MinValue: 3, MaxValue: 30, SystemRule: false, Order: 5),
        new(RuleKeys.AccessibleDeskRestriction, "Erişilebilirlik masaları koruması",
            "Sağlık veya erişilebilirlik gerekçesiyle ayrılan masalar yalnızca yetkili profiller tarafından seçilebilir.",
            RuleCategories.Desk, Enabled: true, EditableLimit: false, IntValue: null, SecondValue: null, Unit: null,
            MinValue: null, MaxValue: null, SystemRule: false, Order: 6),
        new(RuleKeys.NoPastDate, "Geçmiş tarih koruması",
            "Geçmiş tarih ve saatler için rezervasyon oluşturulamaz. Bu kural devre dışı bırakılamaz.",
            RuleCategories.General, Enabled: true, EditableLimit: false, IntValue: null, SecondValue: null, Unit: null,
            MinValue: null, MaxValue: null, SystemRule: true, Order: 7),
        new(RuleKeys.RoomWorkingHours, "Toplantı odası mesai saatleri",
            "Toplantı odaları yalnızca tanımlı mesai aralığında rezerve edilebilir.",
            RuleCategories.Room, Enabled: true, EditableLimit: true, IntValue: DefaultWorkingHours.StartMinutes,
            SecondValue: DefaultWorkingHours.EndMinutes, Unit: "dakika (gün başından itibaren)",
            MinValue: 0, MaxValue: 1440, SystemRule: false, Order: 8),
        new(RuleKeys.RoomCapacityCheck, "Toplantı odası kapasite kontrolü",
            "Katılımcı sayısı oda kapasitesinden fazla olan rezervasyonlar oluşturulamaz.",
            RuleCategories.Room, Enabled: true, EditableLimit: false, IntValue: null, SecondValue: null, Unit: null,
            MinValue: null, MaxValue: null, SystemRule: false, Order: 9),
        new(RuleKeys.RoomMaxDurationMinutes, "Toplantı azami süresi",
            "Tek bir toplantı rezervasyonunun süresi bu değeri aşamaz.",
            RuleCategories.Room, Enabled: true, EditableLimit: true, IntValue: 240, SecondValue: null, Unit: "dakika",
            MinValue: 30, MaxValue: 600, SystemRule: false, Order: 10),
        new(RuleKeys.WorkingDaysOnly, "Yalnızca çalışma günleri",
            "Açıkken hafta sonu ve resmi tatillerde rezervasyon yapılamaz.",
            RuleCategories.General, Enabled: false, EditableLimit: false, IntValue: null, SecondValue: null, Unit: null,
            MinValue: null, MaxValue: null, SystemRule: false, Order: 11),
        new(RuleKeys.RoomSlotMinutes, "Rezervasyon slot aralığı",
            "Toplantı başlangıç ve bitiş saatleri bu aralığın katları olmalıdır (15, 30 veya 60 dakika).",
            RuleCategories.Room, Enabled: true, EditableLimit: true, IntValue: 15, SecondValue: null, Unit: "dakika",
            MinValue: 15, MaxValue: 60, SystemRule: false, Order: 12),
        new(RuleKeys.NoShowRelease, "Gelmeme (no-show) toleransı",
            "Masa rezervasyonunda başlangıç saatinden itibaren bu süre içinde giriş kaydı yoksa rezervasyon gelmedi sayılır ve masa serbest kalır.",
            RuleCategories.Desk, Enabled: true, EditableLimit: true, IntValue: 120, SecondValue: 540, Unit: "dakika",
            MinValue: 0, MaxValue: 240, SystemRule: false, Order: 13),
        new(RuleKeys.RoomStartTracking, "Boş toplantı takibi",
            "Süresi biten ve başlatılmamış toplantılar raporda boş toplantı olarak işaretlenir.",
            RuleCategories.Room, Enabled: true, EditableLimit: false, IntValue: null, SecondValue: null, Unit: null,
            MinValue: null, MaxValue: null, SystemRule: false, Order: 14),
    ];

    /// <summary>Kademe bazli degistirilebilen kurallar; digerleri herkes icin tektir.</summary>
    public static readonly IReadOnlyList<string> GradeScopedKeys =
    [
        RuleKeys.OneDeskPerDay, RuleKeys.AdvanceBookingDays, RuleKeys.WeeklyDeskLimit,
        RuleKeys.WorkingDaysOnly, RuleKeys.RoomMaxDurationMinutes, RuleKeys.RoomSlotMinutes,
    ];

    public static readonly IReadOnlyList<int> AllowedSlotMinutes = [15, 30, 60];

    /// <summary>
    /// Varsayilan kurallara kademe degerlerini bindirir. Oncelik: bina + kademe, kademe, varsayilan.
    /// Kademesi atanmamis kullanici varsayilani alir.
    /// </summary>
    public static IReadOnlyList<RuleRow> ApplyOverrides(
        IEnumerable<RuleRow> rows, IReadOnlyCollection<RuleOverrideRow> overrides, string? grade, string? buildingId)
    {
        if (grade is null) return rows.ToList();

        return rows.Select(row =>
        {
            var general = overrides.FirstOrDefault(o => o.RuleKey == row.Key && o.Grade == grade && o.BuildingId is null);
            var specific = buildingId is null
                ? null
                : overrides.FirstOrDefault(o => o.RuleKey == row.Key && o.Grade == grade && o.BuildingId == buildingId);
            return row with
            {
                Enabled = specific?.Enabled ?? general?.Enabled ?? row.Enabled,
                IntValue = specific?.IntValue ?? general?.IntValue ?? row.IntValue,
            };
        }).ToList();
    }

    /// <summary>Veritabani okunamadiginda kullanilan guvenli varsayilanlar.</summary>
    public static readonly RuleSettings DefaultSettings = new(
        OneDeskPerDay: new ToggleSetting(true),
        AdvanceBookingDays: new AdvanceBookingSetting(true, 14),
        WeeklyDeskLimit: new LimitSetting(true, 3),
        PopularDeskStreakLimit: new LimitSetting(true, 2),
        PopularShareLimit: new PopularShareSetting(true, 10, 4),
        AccessibleDeskRestriction: new ToggleSetting(true),
        NoPastDate: new ToggleSetting(true),
        RoomWorkingHours: new WorkingHoursSetting(true, DefaultWorkingHours.StartMinutes, DefaultWorkingHours.EndMinutes),
        RoomCapacityCheck: new ToggleSetting(true),
        RoomMaxDurationMinutes: new DurationSetting(true, 240),
        WorkingDaysOnly: new ToggleSetting(false),
        RoomSlotMinutes: new DurationSetting(true, 15),
        NoShowRelease: new NoShowSetting(false, 120, 540),
        RoomStartTracking: new ToggleSetting(false));

    /// <summary>Veritabani satirlarini kural motorunun bekledigi ayar nesnesine donusturur (kaynak: mapRulesToSettings).</summary>
    public static RuleSettings MapToSettings(IEnumerable<RuleRow> rows)
    {
        var byKey = new Dictionary<string, RuleRow>();
        foreach (var row in rows)
        {
            byKey[row.Key] = row; // kaynaktaki Map davranisi: son satir kazanir
        }
        RuleRow? Read(string key) => byKey.GetValueOrDefault(key);
        var b = DefaultSettings;

        var oneDesk = Read(RuleKeys.OneDeskPerDay);
        var advance = Read(RuleKeys.AdvanceBookingDays);
        var weekly = Read(RuleKeys.WeeklyDeskLimit);
        var streak = Read(RuleKeys.PopularDeskStreakLimit);
        var share = Read(RuleKeys.PopularShareLimit);
        var accessible = Read(RuleKeys.AccessibleDeskRestriction);
        var past = Read(RuleKeys.NoPastDate);
        var hours = Read(RuleKeys.RoomWorkingHours);
        var capacity = Read(RuleKeys.RoomCapacityCheck);
        var duration = Read(RuleKeys.RoomMaxDurationMinutes);
        var workingDays = Read(RuleKeys.WorkingDaysOnly);
        var slot = Read(RuleKeys.RoomSlotMinutes);
        var noShow = Read(RuleKeys.NoShowRelease);
        var roomStart = Read(RuleKeys.RoomStartTracking);

        return new RuleSettings(
            OneDeskPerDay: new ToggleSetting(oneDesk?.Enabled ?? b.OneDeskPerDay.Enabled),
            AdvanceBookingDays: new AdvanceBookingSetting(
                advance?.Enabled ?? b.AdvanceBookingDays.Enabled,
                advance?.IntValue ?? b.AdvanceBookingDays.Days),
            WeeklyDeskLimit: new LimitSetting(
                weekly?.Enabled ?? b.WeeklyDeskLimit.Enabled,
                weekly?.IntValue ?? b.WeeklyDeskLimit.Limit),
            PopularDeskStreakLimit: new LimitSetting(
                streak?.Enabled ?? b.PopularDeskStreakLimit.Enabled,
                streak?.IntValue ?? b.PopularDeskStreakLimit.Limit),
            PopularShareLimit: new PopularShareSetting(
                share?.Enabled ?? b.PopularShareLimit.Enabled,
                share?.IntValue ?? b.PopularShareLimit.WindowSize,
                share?.SecondValue ?? b.PopularShareLimit.MaxPopular),
            AccessibleDeskRestriction: new ToggleSetting(accessible?.Enabled ?? b.AccessibleDeskRestriction.Enabled),
            // Gecmis tarih korumasi sistem kuralidir; her zaman aciktir.
            NoPastDate: new ToggleSetting(past is not null || b.NoPastDate.Enabled),
            RoomWorkingHours: new WorkingHoursSetting(
                hours?.Enabled ?? b.RoomWorkingHours.Enabled,
                hours?.IntValue ?? b.RoomWorkingHours.StartMinutes,
                hours?.SecondValue ?? b.RoomWorkingHours.EndMinutes),
            RoomCapacityCheck: new ToggleSetting(capacity?.Enabled ?? b.RoomCapacityCheck.Enabled),
            RoomMaxDurationMinutes: new DurationSetting(
                duration?.Enabled ?? b.RoomMaxDurationMinutes.Enabled,
                duration?.IntValue ?? b.RoomMaxDurationMinutes.Minutes),
            WorkingDaysOnly: new ToggleSetting(workingDays?.Enabled ?? b.WorkingDaysOnly.Enabled),
            RoomSlotMinutes: new DurationSetting(
                slot?.Enabled ?? b.RoomSlotMinutes.Enabled,
                slot?.IntValue ?? b.RoomSlotMinutes.Minutes),
            NoShowRelease: new NoShowSetting(
                noShow?.Enabled ?? b.NoShowRelease.Enabled,
                noShow?.IntValue ?? b.NoShowRelease.ToleranceMinutes,
                noShow?.SecondValue ?? b.NoShowRelease.StartMinutes),
            RoomStartTracking: new ToggleSetting(roomStart?.Enabled ?? b.RoomStartTracking.Enabled));
    }
}
