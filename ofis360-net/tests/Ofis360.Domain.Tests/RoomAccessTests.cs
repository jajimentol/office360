using Ofis360.Domain.Common;
using Ofis360.Domain.Rules;

namespace Ofis360.Domain.Tests;

public class RoomAccessTests
{
    private const string Friday = "2026-08-21";
    private const string Tuesday = "2026-08-25";
    private static readonly WorkCalendar Calendar = WorkCalendar.Default;

    private static readonly RoomPolicy Entrance = new(false, [new(Grades.Manager, 5), new(Grades.Gm, 1)], ["bolge"], 1);
    private static readonly RoomPolicy Team = new(false, [new(Grades.SeniorManager, 3), new(Grades.Gm, 1)], [], null);
    private static readonly RoomPolicy Large = new(true, [], [], null);

    private static IReadOnlyList<RuleViolation> Check(RoomPolicy policy, string userId, string? grade, string day, int? attendees = null, bool secretary = false) =>
        RoomAccess.Evaluate(policy, new RoomRequester(userId, grade, secretary), day, Friday, Calendar, attendees);

    private static string[] Codes(IReadOnlyList<RuleViolation> result) => result.Select(v => v.Code).ToArray();

    [Fact]
    public void Kurali_olmayan_oda_herkese_aciktir() =>
        Assert.Empty(Check(RoomPolicy.Open, "u", null, Tuesday, 1));

    [Fact]
    public void Yalniz_sekreter_odasini_diger_kimse_acamaz()
    {
        Assert.Equal(["SECRETARY_ONLY"], Codes(Check(Large, "u", Grades.Gm, Tuesday)));
        Assert.Empty(Check(Large, "u", Grades.Specialist, Tuesday, 1, secretary: true));
    }

    [Fact]
    public void Kademesi_listede_olmayan_ve_atanmamis_kisi_acamaz()
    {
        Assert.Equal(["GRADE_NOT_ALLOWED"], Codes(Check(Team, "u", Grades.Manager, Friday, 3)));
        Assert.Equal(["GRADE_NOT_ALLOWED"], Codes(Check(Team, "u", null, Friday, 3)));
    }

    [Theory]
    [InlineData(Grades.SeniorManager, 2, false)]
    [InlineData(Grades.SeniorManager, 3, true)]
    [InlineData(Grades.Gm, 1, true)]
    public void En_az_katilimci_kademeye_gore(string grade, int attendees, bool allowed) =>
        Assert.Equal(allowed, Check(Team, "u", grade, Friday, attendees).Count == 0);

    [Fact]
    public void Oda_listesinde_katilimci_kontrol_edilmez() =>
        Assert.Empty(Check(Team, "u", Grades.SeniorManager, Friday, attendees: null));

    [Fact]
    public void Oncelikli_oda_digerlerine_son_is_gununde_acilir()
    {
        var monday = "2026-08-24";
        Assert.Equal(["PRIORITY_WINDOW"], Codes(Check(Entrance, "u", Grades.Manager, Tuesday, 5)));
        Assert.Empty(Check(Entrance, "u", Grades.Manager, monday, 5));
    }

    [Fact]
    public void Acilis_tarihi_mesajda_is_gunune_gore_yazar()
    {
        var violation = Assert.Single(Check(Entrance, "u", Grades.Manager, Tuesday, 5));
        Assert.Contains("24 Ağustos 2026", violation.Message);
    }

    [Fact]
    public void Oncelikli_kisi_erken_alir_ve_kademesi_listede_olmasa_da_acabilir()
    {
        Assert.Empty(Check(Entrance, "bolge", Grades.Manager, Tuesday, 5));
        Assert.Empty(Check(Entrance, "bolge", Grades.SeniorSpecialist, Tuesday, 1));
    }

    [Fact]
    public void Muaf_kademe_onceligi_beklemez_ama_en_az_kisi_kurali_gecerli()
    {
        var policy = Entrance with { GradeRules = [new(Grades.Manager, 5), new(Grades.Gm, 2, true)] };
        Assert.Empty(Check(policy, "u", Grades.Gm, Tuesday, 2));
        Assert.Equal(["MIN_ATTENDEES"], Codes(Check(policy, "u", Grades.Gm, Tuesday, 1)));
        Assert.Equal(["PRIORITY_WINDOW"], Codes(Check(policy, "u", Grades.Manager, Tuesday, 5)));
    }

    [Fact]
    public void Oncelikli_kisi_listesi_bossa_kisit_yoktur()
    {
        var policy = Entrance with { PriorityUserIds = [] };
        Assert.Empty(Check(policy, "u", Grades.Manager, "2026-09-01", 5));
    }

    [Fact]
    public void Sekreter_tum_oda_kisitlarindan_muaftir()
    {
        Assert.Empty(Check(Entrance, "u", Grades.Specialist, Tuesday, 1, secretary: true));
        Assert.Empty(Check(Team, "u", null, Tuesday, 1, secretary: true));
        Assert.Equal(1, RoomAccess.MinAttendees(Entrance, new RoomRequester("u", Grades.Manager, true)));
    }

    [Theory]
    [InlineData("2026-08-25", 1, "2026-08-24")] // Sali: Pazartesi acilir
    [InlineData("2026-08-24", 1, "2026-08-21")] // Pazartesi: bir onceki is gunu Cuma
    [InlineData("2026-08-28", 2, "2026-08-26")]
    [InlineData("2026-08-25", 0, "2026-08-25")]
    public void Acilis_gunu_is_gunu_geriye_sayar(string day, int days, string expected) =>
        Assert.Equal(expected, Calendar.OpensOn(day, days));

    [Fact]
    public void Acilis_gunu_tatili_atlar() =>
        Assert.Equal("2026-08-21", new WorkCalendar(["2026-08-24"]).OpensOn("2026-08-25", 1));
}
