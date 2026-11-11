using Ofis360.Domain.Rules;

namespace Ofis360.Domain.Tests;

/// <summary>Kaynak: tests/unit/room-rules.test.ts (20 test).</summary>
public class RoomRulesTests
{
    private static readonly RuleSettings Settings = RuleDefinitions.DefaultSettings;

    private static RoomRuleContext MakeContext(
        string dayKey = "2026-08-24",
        int nowMinutes = -1,
        int startMinutes = 10 * 60,
        int endMinutes = 11 * 60,
        int attendeeCount = 6,
        ClosureInfo? closure = null,
        IReadOnlyList<RoomConflict>? conflicts = null) => new(
        TodayKey: "2026-08-21",
        DayKey: dayKey,
        NowMinutes: nowMinutes,
        Room: new RoomRuleRoom("r1", "T101", "Boğaziçi", 10, true, "1. Kat", true),
        Closure: closure ?? ClosureInfo.None,
        StartMinutes: startMinutes,
        EndMinutes: endMinutes,
        AttendeeCount: attendeeCount,
        Conflicts: conflicts ?? []);

    private static bool HasCode(RuleEvaluation result, string code) => result.Violations.Any(v => v.Code == code);

    [Fact]
    public void Uygun_talebe_izin_verir()
    {
        Assert.True(RoomRules.Evaluate(MakeContext(), Settings).Allowed);
    }

    [Fact]
    public void Bitis_baslangictan_once_olamaz()
    {
        var result = RoomRules.Evaluate(MakeContext(startMinutes: 11 * 60, endMinutes: 10 * 60), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "INVALID_RANGE"));
    }

    [Fact]
    public void Mesai_disindaki_talebi_engeller()
    {
        var result = RoomRules.Evaluate(MakeContext(startMinutes: 7 * 60, endMinutes: 8 * 60), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "OUTSIDE_WORKING_HOURS"));
    }

    [Fact]
    public void Mesai_bitiminden_sonraya_tasan_talebi_engeller()
    {
        var result = RoomRules.Evaluate(MakeContext(startMinutes: 18 * 60 + 30, endMinutes: 19 * 60 + 30), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "OUTSIDE_WORKING_HOURS"));
    }

    [Fact]
    public void Kapasiteden_fazla_katilimciyi_engeller()
    {
        var result = RoomRules.Evaluate(MakeContext(attendeeCount: 12), Settings);
        Assert.False(result.Allowed);
        var violation = result.Violations.Single(v => v.Code == "CAPACITY_EXCEEDED");
        Assert.Contains("10", violation.Message);
    }

    [Fact]
    public void Kapasiteye_esit_katilimciya_izin_verir()
    {
        Assert.True(RoomRules.Evaluate(MakeContext(attendeeCount: 10), Settings).Allowed);
    }

    [Fact]
    public void Cakisan_rezervasyonu_engeller()
    {
        var result = RoomRules.Evaluate(
            MakeContext(conflicts: [new RoomConflict("x", "Sprint planlama", 10 * 60 + 30, 11 * 60 + 30)]), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "ROOM_CONFLICT"));
    }

    [Fact]
    public void Bitisik_rezervasyona_izin_verir()
    {
        var result = RoomRules.Evaluate(
            MakeContext(conflicts: [new RoomConflict("x", "Önceki toplantı", 9 * 60, 10 * 60)]), Settings);
        Assert.True(result.Allowed);
    }

    [Fact]
    public void Gecmis_tarihi_engeller()
    {
        var result = RoomRules.Evaluate(MakeContext(dayKey: "2026-08-20"), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "PAST_DATE"));
    }

    [Fact]
    public void Bugun_icin_gecmis_saati_engeller()
    {
        var result = RoomRules.Evaluate(
            MakeContext(dayKey: "2026-08-21", nowMinutes: 14 * 60, startMinutes: 9 * 60, endMinutes: 10 * 60), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "PAST_TIME"));
    }

    [Fact]
    public void Azami_sureyi_asan_toplantiyi_engeller()
    {
        var result = RoomRules.Evaluate(MakeContext(startMinutes: 9 * 60, endMinutes: 18 * 60), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "DURATION_EXCEEDED"));
    }

    [Fact]
    public void Kapali_odayi_engeller()
    {
        var result = RoomRules.Evaluate(MakeContext(closure: new ClosureInfo(true, "Eğitim programı")), Settings);
        Assert.False(result.Allowed);
        Assert.True(HasCode(result, "CLOSURE_ACTIVE"));
    }

    // --- overlaps ---

    [Fact]
    public void Overlaps_kesisen_araliklari_tespit_eder() => Assert.True(RoomRules.Overlaps(600, 660, 630, 690));

    [Fact]
    public void Overlaps_bitisik_araliklari_kesisim_saymaz() => Assert.False(RoomRules.Overlaps(600, 660, 660, 720));

    [Fact]
    public void Overlaps_kapsayan_araligi_tespit_eder() => Assert.True(RoomRules.Overlaps(600, 720, 630, 660));

    // --- computeFreeSlots ---

    [Fact]
    public void FreeSlots_dolu_araliklar_arasindaki_bosluklari_dondurur()
    {
        var slots = RoomRules.ComputeFreeSlots(480, 1140, [new TimeRange(540, 600), new TimeRange(660, 720)], 30);
        Assert.Equal([new TimeRange(480, 540), new TimeRange(600, 660), new TimeRange(720, 1140)], slots);
    }

    [Fact]
    public void FreeSlots_minimum_sureden_kisa_bosluklari_eler()
    {
        var slots = RoomRules.ComputeFreeSlots(480, 600, [new TimeRange(510, 540)], 60);
        Assert.Equal([new TimeRange(540, 600)], slots);
    }

    [Fact]
    public void FreeSlots_hicbir_aralik_uymuyorsa_bos_doner()
    {
        var slots = RoomRules.ComputeFreeSlots(480, 570, [new TimeRange(510, 540)], 60);
        Assert.Empty(slots);
    }

    [Fact]
    public void FreeSlots_gecmis_saatleri_disarida_birakir()
    {
        var slots = RoomRules.ComputeFreeSlots(480, 1140, [], 30, notBeforeMinutes: 900);
        Assert.Equal([new TimeRange(900, 1140)], slots);
    }

    [Fact]
    public void FreeSlots_ust_uste_binen_dolu_araliklari_birlestirir()
    {
        var slots = RoomRules.ComputeFreeSlots(480, 1140, [new TimeRange(540, 660), new TimeRange(600, 720)], 30);
        Assert.Equal([new TimeRange(480, 540), new TimeRange(720, 1140)], slots);
    }
}
