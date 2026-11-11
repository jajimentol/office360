using Ofis360.Domain.Rules;

namespace Ofis360.Domain.Tests;

/// <summary>Kaynak: tests/unit/desk-rules.test.ts (30 test). Kural motoru saf oldugu icin veritabani gerekmez.</summary>
public class DeskRulesTests
{
    private static readonly RuleSettings Settings = RuleDefinitions.DefaultSettings;

    private static DeskRuleDesk MakeDesk() => new(
        Id: "d1", Code: "F1-A01", Active: true, Placed: true, AccessibleOnly: false, Popular: false,
        FloorName: "1. Kat", FloorReservable: true, ZoneName: "A Bölgesi", ZoneReservable: true);

    private static DeskRuleContext MakeContext(
        string dayKey = "2026-08-24",
        DeskRuleDesk? desk = null,
        DeskRuleUser? user = null,
        ClosureInfo? closure = null,
        OccupiedBy? occupiedBy = null,
        SameDayReservation? sameDay = null,
        int weeklyReservationCount = 0,
        IReadOnlyList<UserDeskHistoryItem>? history = null) => new(
        TodayKey: "2026-08-21",
        DayKey: dayKey,
        User: user ?? new DeskRuleUser("u1", "Test Kullanıcı", false),
        Desk: desk ?? MakeDesk(),
        Closure: closure ?? ClosureInfo.None,
        OccupiedBy: occupiedBy,
        SameDayReservation: sameDay,
        WeeklyReservationCount: weeklyReservationCount,
        History: history ?? []);

    private static bool HasCode(RuleEvaluation result, string code) => result.Violations.Any(v => v.Code == code);

    // --- evaluateDeskReservation - temel kurallar ---

    [Fact]
    public void Uygun_masa_icin_izin_verir()
    {
        var result = DeskRules.Evaluate(MakeContext(), Settings);
        Assert.True(result.Allowed);
        Assert.Empty(result.Violations);
    }

    [Fact]
    public void Gecmis_tarihi_engeller()
    {
        var result = DeskRules.Evaluate(MakeContext(dayKey: "2026-08-20"), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "PAST_DATE"));
    }

    [Fact]
    public void Bugun_icin_izin_verir()
    {
        Assert.True(DeskRules.Evaluate(MakeContext(dayKey: "2026-08-21"), Settings).Allowed);
    }

    [Fact]
    public void Pencere_disindaki_tarihi_engeller_14_is_gunu()
    {
        var result = DeskRules.Evaluate(MakeContext(dayKey: "2026-09-11"), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "ADVANCE_WINDOW_EXCEEDED"));
    }

    [Fact]
    public void Pencerenin_tam_sinirina_izin_verir()
    {
        Assert.True(DeskRules.Evaluate(MakeContext(dayKey: "2026-09-10"), Settings).Allowed);
    }

    [Fact]
    public void Baska_calisanin_masasini_engeller()
    {
        var result = DeskRules.Evaluate(MakeContext(occupiedBy: new OccupiedBy("u2", "Diğer Çalışan")), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "DESK_OCCUPIED"));
    }

    [Fact]
    public void Kendi_masasini_tekrar_rezerve_etmeyi_engeller()
    {
        var result = DeskRules.Evaluate(MakeContext(occupiedBy: new OccupiedBy("u1", "Test Kullanıcı")), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "ALREADY_OWNED"));
    }

    [Fact]
    public void Pasif_masayi_engeller()
    {
        var result = DeskRules.Evaluate(MakeContext(desk: MakeDesk() with { Active = false }), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "DESK_INACTIVE"));
    }

    [Fact]
    public void Kapatma_kaydi_olan_tarihi_engeller()
    {
        var result = DeskRules.Evaluate(MakeContext(closure: new ClosureInfo(true, "Bakım çalışması")), Settings);
        Assert.False(result.Allowed);
        var violation = result.Violations.Single(v => v.Code == "CLOSURE_ACTIVE");
        Assert.Contains("Bakım çalışması", violation.Message);
    }

    [Fact]
    public void Rezervasyona_kapali_kattaki_masayi_engeller()
    {
        var result = DeskRules.Evaluate(MakeContext(desk: MakeDesk() with { FloorReservable = false }), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "FLOOR_NOT_RESERVABLE"));
    }

    [Fact]
    public void Hafta_sonu_icin_uyari_uretir_ama_engellemez()
    {
        var result = DeskRules.Evaluate(MakeContext(dayKey: "2026-08-22"), Settings);
        Assert.True(result.Allowed);
        Assert.Contains(result.Warnings, w => w.Code == "WEEKEND_NOTICE");
    }

    // --- Ayni gun tek masa ---

    [Fact]
    public void Ayni_gun_ikinci_masayi_engeller()
    {
        var result = DeskRules.Evaluate(MakeContext(sameDay: new SameDayReservation("F2-D03", "r1")), Settings);
        Assert.False(result.Allowed);
        var violation = result.Violations.SingleOrDefault(v => v.Code == "ONE_DESK_PER_DAY");
        Assert.NotNull(violation);
        Assert.Contains("F2-D03", violation.Message);
    }

    [Fact]
    public void Kural_kapaliyken_ikinci_rezervasyona_izin_verir()
    {
        var relaxed = Settings with { OneDeskPerDay = new ToggleSetting(false) };
        var result = DeskRules.Evaluate(MakeContext(sameDay: new SameDayReservation("F2-D03", "r1")), relaxed);
        Assert.True(result.Allowed);
    }

    // --- Haftalik limit ---

    [Fact]
    public void Haftalik_limit_doldugunda_engeller()
    {
        var result = DeskRules.Evaluate(MakeContext(weeklyReservationCount: 3), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "WEEKLY_LIMIT_REACHED"));
    }

    [Fact]
    public void Haftalik_limit_dolmadiginda_izin_verir()
    {
        Assert.True(DeskRules.Evaluate(MakeContext(weeklyReservationCount: 2), Settings).Allowed);
    }

    [Fact]
    public void Admin_limiti_degistirdiginde_yeni_limit_uygulanir()
    {
        var updated = RuleDefinitions.MapToSettings([new RuleRow("WEEKLY_DESK_LIMIT", true, 5, null)]);
        var result = DeskRules.Evaluate(MakeContext(weeklyReservationCount: 4), updated);
        Assert.Equal(5, updated.WeeklyDeskLimit.Limit);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void Kural_pasifken_limit_uygulanmaz()
    {
        var relaxed = Settings with { WeeklyDeskLimit = new LimitSetting(false, 3) };
        Assert.True(DeskRules.Evaluate(MakeContext(weeklyReservationCount: 9), relaxed).Allowed);
    }

    // --- Populer masa ardisik kullanim limiti ---

    private static DeskRuleContext PopularDeskContext(IReadOnlyList<UserDeskHistoryItem> history)
        => MakeContext(dayKey: "2026-08-24", history: history, desk: MakeDesk() with { Popular = true, Id = "popular-1" });

    [Fact]
    public void Art_arda_iki_gun_sonrasi_ucuncu_kullanimi_engeller()
    {
        var history = new List<UserDeskHistoryItem>
        {
            new("2026-08-20", "popular-1", true),
            new("2026-08-21", "popular-1", true),
        };
        var result = DeskRules.Evaluate(PopularDeskContext(history), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "POPULAR_STREAK_LIMIT"));
    }

    [Fact]
    public void Araya_farkli_masa_girince_seriyi_sifirlar()
    {
        var history = new List<UserDeskHistoryItem>
        {
            new("2026-08-19", "popular-1", true),
            new("2026-08-20", "other", false),
            new("2026-08-21", "popular-1", true),
        };
        Assert.True(DeskRules.Evaluate(PopularDeskContext(history), Settings).Allowed);
    }

    [Fact]
    public void Populer_olmayan_masada_seri_kurali_uygulanmaz()
    {
        var history = new List<UserDeskHistoryItem>
        {
            new("2026-08-20", "d1", false),
            new("2026-08-21", "d1", false),
        };
        Assert.True(DeskRules.Evaluate(MakeContext(history: history), Settings).Allowed);
    }

    // --- computeConsecutiveDeskStreak ---

    [Fact]
    public void Streak_hedef_gun_dahil_hesaplanir()
    {
        var history = new List<UserDeskHistoryItem> { new("2026-08-17", "A", true), new("2026-08-19", "A", true) };
        Assert.Equal(3, DeskRules.ComputeConsecutiveDeskStreak(history, "2026-08-21", "A"));
    }

    [Fact]
    public void Streak_araya_farkli_masa_girince_kesilir()
    {
        var history = new List<UserDeskHistoryItem> { new("2026-08-17", "A", true), new("2026-08-19", "B", false) };
        Assert.Equal(1, DeskRules.ComputeConsecutiveDeskStreak(history, "2026-08-21", "A"));
    }

    [Fact]
    public void Streak_hedef_gun_sonrasindaki_kayitlari_dahil_eder()
    {
        var history = new List<UserDeskHistoryItem> { new("2026-08-25", "A", true), new("2026-08-26", "A", true) };
        Assert.Equal(3, DeskRules.ComputeConsecutiveDeskStreak(history, "2026-08-24", "A"));
    }

    // --- Populer masa kullanim payi ---

    private static List<UserDeskHistoryItem> BuildHistory(int popularCount, int total) =>
        Enumerable.Range(0, total)
            .Select(index => new UserDeskHistoryItem($"2026-08-{index + 1:00}", $"desk-{index}", index < popularCount))
            .ToList();

    [Fact]
    public void Son_10da_4_populer_sinir_icindedir()
    {
        var context = MakeContext(history: BuildHistory(4, 10), desk: MakeDesk() with { Popular = true });
        Assert.True(DeskRules.Evaluate(context, Settings).Allowed);
    }

    [Fact]
    public void Son_10da_5_populer_siniri_asar()
    {
        var context = MakeContext(history: BuildHistory(5, 10), desk: MakeDesk() with { Popular = true });
        var result = DeskRules.Evaluate(context, Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "POPULAR_SHARE_LIMIT"));
    }

    [Fact]
    public void Sinir_asilsa_bile_populer_olmayan_masa_secilebilir()
    {
        var context = MakeContext(history: BuildHistory(8, 10), desk: MakeDesk() with { Popular = false });
        Assert.True(DeskRules.Evaluate(context, Settings).Allowed);
    }

    [Fact]
    public void Pencere_boyutuna_uyar()
    {
        var (_, sampleSize) = DeskRules.ComputePopularUsageInWindow(BuildHistory(6, 20), "2026-09-01", 10);
        Assert.Equal(10, sampleSize);
    }

    // --- Erisilebilirlik masalari korumasi ---

    [Fact]
    public void Erisilebilirlik_yetkisiz_kullaniciyi_engeller()
    {
        var result = DeskRules.Evaluate(MakeContext(desk: MakeDesk() with { AccessibleOnly = true }), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "ACCESSIBLE_ONLY"));
    }

    [Fact]
    public void Erisilebilirlik_yetkili_kullaniciya_izin_verir()
    {
        var context = MakeContext(
            desk: MakeDesk() with { AccessibleOnly = true },
            user: new DeskRuleUser("u1", "Test Kullanıcı", true));
        Assert.True(DeskRules.Evaluate(context, Settings).Allowed);
    }

    // --- buildFairUseSummary ---

    [Fact]
    public void FairUse_ozeti_kalan_hakki_ve_populer_payi_hesaplar()
    {
        var summary = DeskRules.BuildFairUseSummary(
            Settings,
            [new("2026-08-20", "a", true), new("2026-08-19", "b", false)],
            weeklyReservationCount: 1,
            referenceDayKey: "2026-08-21");

        Assert.Equal(3, summary.WeeklyLimit);
        Assert.Equal(1, summary.WeeklyUsed);
        Assert.Equal(2, summary.WeeklyRemaining);
        Assert.Equal(1, summary.PopularUsed);
        Assert.False(summary.PopularRestricted);
        Assert.Equal("2026-08-17", summary.WeekStartKey);
    }
}
