using Ofis360.Domain.Common;

namespace Ofis360.Domain.Tests;

public class GradesTests
{
    [Theory]
    [InlineData("Uzman Yardımcısı", Grades.AssistantSpecialist)]
    [InlineData("UZMAN YRD.", Grades.AssistantSpecialist)]
    [InlineData("uzman", Grades.Specialist)]
    [InlineData("KIDEMLİ UZMAN", Grades.SeniorSpecialist)]
    [InlineData("Müd. Yrd.", Grades.AssistantManager)]
    [InlineData("MÜDÜR YARDIMCISI", Grades.AssistantManager)]
    [InlineData("Müdür", Grades.Manager)]
    [InlineData("kıdemli müdür", Grades.SeniorManager)]
    [InlineData("Direktör", Grades.Director)]
    [InlineData("GMY", Grades.DeputyGm)]
    [InlineData("Genel Müdür Yardımcısı", Grades.DeputyGm)]
    [InlineData("gm", Grades.Gm)]
    [InlineData("  Genel Müdür ", Grades.Gm)]
    public void Yazim_farklarina_ragmen_kademeyi_tanir(string text, string expected) =>
        Assert.Equal(expected, Grades.Parse(text));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("Stajyer")]
    [InlineData("Yönetici")]
    public void Taninmayan_metin_null_doner(string? text) => Assert.Null(Grades.Parse(text));

    [Fact]
    public void Kademeler_kidem_sirasinda_ve_atanmamis_en_altta()
    {
        Assert.Equal(9, Grades.All.Count);
        Assert.All(Grades.All, g => Assert.True(Grades.Labels.ContainsKey(g)));
        for (var i = 1; i < Grades.All.Count; i++) Assert.True(Grades.Rank(Grades.All[i]) > Grades.Rank(Grades.All[i - 1]));
        Assert.Equal(-1, Grades.Rank(null));
        Assert.True(Grades.Rank(Grades.Gm) > Grades.Rank(Grades.DeputyGm));
    }
}
