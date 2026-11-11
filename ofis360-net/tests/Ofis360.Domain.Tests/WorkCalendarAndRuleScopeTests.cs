using Ofis360.Domain.Common;
using Ofis360.Domain.Rules;

namespace Ofis360.Domain.Tests;

public class WorkCalendarAndRuleScopeTests
{
    private const string Friday = "2026-08-21";

    private static RuleSettings Settings(int advance = 2, bool workingDaysOnly = false, int slot = 30) =>
        RuleDefinitions.DefaultSettings with
        {
            AdvanceBookingDays = new AdvanceBookingSetting(true, advance),
            WorkingDaysOnly = new ToggleSetting(workingDaysOnly),
            RoomSlotMinutes = new DurationSetting(true, slot),
        };

    private static DeskRuleContext Desk(string dayKey, WorkCalendar? calendar = null) => new(
        Friday, dayKey, new DeskRuleUser("u1", "Test", false),
        new DeskRuleDesk("d1", "M-1", true, true, false, false, "1. Kat", true, "A", true),
        ClosureInfo.None, null, null, 0, [], calendar);

    private static RoomRuleContext Room(string dayKey, int start = 600, int end = 660, WorkCalendar? calendar = null) => new(
        Friday, dayKey, -1, new RoomRuleRoom("r1", "T1", "Oda", 10, true, "1. Kat", true), ClosureInfo.None, start, end, 4, [], calendar);

    private static bool Has(RuleEvaluation result, string code) => result.Violations.Any(v => v.Code == code);

    [Theory]
    [InlineData("2026-08-21", 1, "2026-08-24")] // Cuma, 1: Pazartesi
    [InlineData("2026-08-21", 2, "2026-08-25")] // Cuma, 2: Sali
    [InlineData("2026-08-22", 2, "2026-08-25")] // Cumartesi bugun: Pzt ve Sali
    [InlineData("2026-08-24", 2, "2026-08-26")] // Pazartesi: bugun sayilmaz
    [InlineData("2026-08-21", 5, "2026-08-28")]
    [InlineData("2026-08-21", 15, "2026-09-11")]
    public void Son_uygun_gun_is_gunu_sayar(string today, int days, string expected) =>
        Assert.Equal(expected, WorkCalendar.Default.LatestBookableDay(today, days));

    [Fact]
    public void Resmi_tatil_is_gunu_sayilmaz()
    {
        var calendar = new WorkCalendar(["2026-08-24"]);
        Assert.False(calendar.IsWorkingDay("2026-08-24"));
        Assert.Equal("2026-08-25", calendar.LatestBookableDay(Friday, 1));
    }

    [Fact]
    public void Calisma_disi_gunleri_listeler()
    {
        var calendar = new WorkCalendar(["2026-08-24"]);
        Assert.Equal(["2026-08-22", "2026-08-23", "2026-08-24"], calendar.NonWorkingDays(Friday, "2026-08-25"));
    }

    [Fact]
    public void Cuma_gunu_2_is_gunu_ile_Pazartesi_ve_Sali_secilebilir()
    {
        Assert.False(Has(DeskRules.Evaluate(Desk("2026-08-24"), Settings()), "ADVANCE_WINDOW_EXCEEDED"));
        Assert.False(Has(DeskRules.Evaluate(Desk("2026-08-25"), Settings()), "ADVANCE_WINDOW_EXCEEDED"));
        Assert.True(Has(DeskRules.Evaluate(Desk("2026-08-26"), Settings()), "ADVANCE_WINDOW_EXCEEDED"));
        Assert.True(Has(RoomRules.Evaluate(Room("2026-08-26"), Settings()), "ADVANCE_WINDOW_EXCEEDED"));
    }

    [Fact]
    public void Yalnizca_calisma_gunleri_hafta_sonu_ve_tatili_engeller()
    {
        var calendar = new WorkCalendar(["2026-08-24"]);
        var settings = Settings(advance: 5, workingDaysOnly: true);
        Assert.True(Has(DeskRules.Evaluate(Desk("2026-08-22", calendar), settings), "NON_WORKING_DAY"));
        Assert.True(Has(DeskRules.Evaluate(Desk("2026-08-24", calendar), settings), "NON_WORKING_DAY"));
        Assert.True(Has(RoomRules.Evaluate(Room("2026-08-23", calendar: calendar), settings), "NON_WORKING_DAY"));
        Assert.False(Has(DeskRules.Evaluate(Desk("2026-08-25", calendar), settings), "NON_WORKING_DAY"));
    }

    [Fact]
    public void Kural_kapaliyken_hafta_sonu_serbest_ve_uyari_verir()
    {
        var result = DeskRules.Evaluate(Desk("2026-08-22"), Settings(workingDaysOnly: false));
        Assert.True(result.Allowed);
        Assert.Contains(result.Warnings, w => w.Code == "WEEKEND_NOTICE");
    }

    [Theory]
    [InlineData(600, 660, true)]
    [InlineData(630, 690, true)]
    [InlineData(615, 660, false)]
    [InlineData(600, 645, false)]
    public void Slot_aralik_katlarini_zorlar(int start, int end, bool allowed) =>
        Assert.Equal(!allowed, Has(RoomRules.Evaluate(Room("2026-08-24", start, end), Settings()), "INVALID_SLOT"));

    [Fact]
    public void Bos_araliklar_slota_hizalanir()
    {
        var slots = RoomRules.ComputeFreeSlots(480, 1140, [new TimeRange(600, 645)], 30, notBeforeMinutes: 487, slotMinutes: 30);
        Assert.Equal([new TimeRange(510, 600), new TimeRange(660, 1140)], slots);
    }

    private static readonly RuleRow[] Rows = [new(RuleKeys.AdvanceBookingDays, true, 2, null), new(RuleKeys.WorkingDaysOnly, true, null, null)];

    [Fact]
    public void Kademesiz_kullanici_varsayilani_alir()
    {
        RuleOverrideRow[] overrides = [new(RuleKeys.AdvanceBookingDays, Grades.Gm, null, null, 15)];
        Assert.Equal(2, RuleDefinitions.ApplyOverrides(Rows, overrides, null, null)[0].IntValue);
    }

    [Fact]
    public void Kademe_degeri_varsayilani_ezer_digerlerine_dokunmaz()
    {
        RuleOverrideRow[] overrides = [new(RuleKeys.AdvanceBookingDays, Grades.Gm, null, null, 15)];
        var result = RuleDefinitions.ApplyOverrides(Rows, overrides, Grades.Gm, null);
        Assert.Equal(15, result[0].IntValue);
        Assert.True(result[1].Enabled);
        Assert.Equal(2, RuleDefinitions.ApplyOverrides(Rows, overrides, Grades.Specialist, null)[0].IntValue);
    }

    [Fact]
    public void Bina_degeri_genel_kademe_degerini_ezer()
    {
        RuleOverrideRow[] overrides =
        [
            new(RuleKeys.AdvanceBookingDays, Grades.Director, null, null, 5),
            new(RuleKeys.AdvanceBookingDays, Grades.Director, "b1", null, 7),
        ];
        Assert.Equal(7, RuleDefinitions.ApplyOverrides(Rows, overrides, Grades.Director, "b1")[0].IntValue);
        Assert.Equal(5, RuleDefinitions.ApplyOverrides(Rows, overrides, Grades.Director, "b2")[0].IntValue);
        Assert.Equal(5, RuleDefinitions.ApplyOverrides(Rows, overrides, Grades.Director, null)[0].IntValue);
    }

    [Fact]
    public void Acik_kapali_kademe_degeri_uygulanir_ve_ayarlara_yansir()
    {
        RuleOverrideRow[] overrides = [new(RuleKeys.WorkingDaysOnly, Grades.Manager, null, false, null)];
        var settings = RuleDefinitions.MapToSettings(RuleDefinitions.ApplyOverrides(Rows, overrides, Grades.Manager, null));
        Assert.False(settings.WorkingDaysOnly.Enabled);
        Assert.Equal(2, settings.AdvanceBookingDays.Days);
    }
}
