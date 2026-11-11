using Ofis360.Domain.Common;

namespace Ofis360.Domain.Tests;

/// <summary>Kaynak: tests/unit/date-and-analytics.test.ts "Analitik yardımcıları", "CSV üretimi", "initialsOf" (8 test).</summary>
public class AnalyticsHelpersTests
{
    [Fact]
    public void Esit_dagilimda_Gini_sifira_yakindir()
    {
        Assert.Equal(0, Statistics.ComputeGini([5, 5, 5, 5]), precision: 5);
    }

    [Fact]
    public void Tek_kiside_yogunlasan_dagilimda_Gini_yukselir()
    {
        Assert.True(Statistics.ComputeGini([0, 0, 0, 20]) > 0.7);
    }

    [Fact]
    public void Bos_dizide_sifir_doner()
    {
        Assert.Equal(0, Statistics.ComputeGini(Array.Empty<int>()));
        Assert.Equal(0, Statistics.ComputeGini([0, 0, 0]));
    }

    [Fact]
    public void Oran_hesabinda_sifira_bolmeyi_engeller()
    {
        Assert.Equal(0, TextUtils.Ratio(5, 0));
        Assert.Equal(50, TextUtils.Ratio(5, 10));
    }

    [Fact]
    public void Csv_baslik_ve_satirlari_noktali_virgulle_ayirir()
    {
        var csv = TextUtils.ToCsv(["Ad", "Değer"], [new object?[] { "Masa", 5 }]);
        Assert.Contains("Ad;Değer", csv);
        Assert.Contains("Masa;5", csv);
    }

    [Fact]
    public void Csv_icinde_ayrac_olan_degerleri_tirnaklar()
    {
        var csv = TextUtils.ToCsv(["Not"], [new object?[] { "Bakım; onarım" }]);
        Assert.Contains("\"Bakım; onarım\"", csv);
    }

    [Fact]
    public void Csv_UTF8_BOM_ile_baslar()
    {
        Assert.Equal(0xFEFF, TextUtils.ToCsv(["A"], [new object?[] { "b" }])[0]);
    }

    [Theory]
    [InlineData("=HYPERLINK(1)", "'=HYPERLINK(1)")]
    [InlineData("+90", "'+90")]
    [InlineData("-1", "'-1")]
    [InlineData("@x", "'@x")]
    [InlineData("Normal", "Normal")]
    public void Csv_formul_hucresi_etkisizlesir(string input, string expected)
    {
        Assert.Contains(expected, TextUtils.ToCsv(["A"], [new object?[] { input }]));
    }

    [Fact]
    public void Csv_sayisal_deger_onek_almaz_ve_cr_tirnaklanir()
    {
        Assert.EndsWith("-5", TextUtils.ToCsv(["A"], [new object?[] { -5 }]).TrimEnd());
        Assert.Contains("\"a\rb\"", TextUtils.ToCsv(["A"], [new object?[] { "a\rb" }]));
    }

    [Fact]
    public void Initials_ad_ve_soyadin_bas_harflerini_uretir()
    {
        Assert.Equal("EY", TextUtils.InitialsOf("Elif Yılmaz"));
        Assert.Equal("ZK", TextUtils.InitialsOf("Zeynep Kaya"));
    }
}
