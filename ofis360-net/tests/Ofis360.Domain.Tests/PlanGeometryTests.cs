using Ofis360.Domain.Plan;

namespace Ofis360.Domain.Tests;

/// <summary>Kaynak: tests/unit/plan-geometry.test.ts geometri + tanim testleri (16). Sema testleri Application.Tests'te.</summary>
public class PlanGeometryTests
{
    [Fact]
    public void SnapToGrid_kapaliyken_yalnizca_yuvarlar() => Assert.Equal(14, PlanGeometry.SnapToGrid(13.6, false));

    [Fact]
    public void SnapToGrid_acikken_en_yakin_izgara_noktasina_hizalar()
    {
        Assert.Equal(10, PlanGeometry.SnapToGrid(13.6, true));
        Assert.Equal(20, PlanGeometry.SnapToGrid(16, true));
        Assert.Equal(0, PlanGeometry.SnapToGrid(-4, true));
    }

    [Fact]
    public void SnapToGrid_ozel_izgara_adimini_kullanir() => Assert.Equal(25, PlanGeometry.SnapToGrid(23, true, 25));

    [Fact]
    public void ClampRect_ogeyi_plan_sinirlari_icinde_tutar()
    {
        var rect = PlanGeometry.ClampRect(new PlanRect(990, -20, 100, 60), 1000, 600);
        Assert.Equal(900, rect.X);
        Assert.Equal(0, rect.Y);
        Assert.Equal(100, rect.Width);
        Assert.Equal(60, rect.Height);
    }

    [Fact]
    public void ClampRect_plandan_buyuk_ogeyi_kucultur()
    {
        var rect = PlanGeometry.ClampRect(new PlanRect(0, 0, 5000, 4000), 1000, 600);
        Assert.Equal(1000, rect.Width);
        Assert.Equal(600, rect.Height);
    }

    [Fact]
    public void ClampRect_asgari_boyutun_altina_inmez()
    {
        var rect = PlanGeometry.ClampRect(new PlanRect(10, 10, 1, 1), 1000, 600);
        Assert.Equal(PlanGeometry.MinSize, rect.Width);
        Assert.Equal(PlanGeometry.MinSize, rect.Height);
    }

    [Fact]
    public void NormalizeRect_ters_yonde_cizilen_dikdortgeni_duzeltir()
    {
        Assert.Equal(new PlanRect(100, 50, 100, 100), PlanGeometry.NormalizeRect(new PlanPoint(200, 150), new PlanPoint(100, 50)));
    }

    [Fact]
    public void WallRect_yatay_duvar_uretir()
    {
        var rect = PlanGeometry.WallRectFromPoints(new PlanPoint(100, 100), new PlanPoint(300, 108), 10, false);
        Assert.Equal(200, rect.Width);
        Assert.Equal(10, rect.Height);
        Assert.Equal(100, rect.X);
    }

    [Fact]
    public void WallRect_dikey_duvar_uretir()
    {
        var rect = PlanGeometry.WallRectFromPoints(new PlanPoint(100, 100), new PlanPoint(104, 400), 10, false);
        Assert.Equal(300, rect.Height);
        Assert.Equal(10, rect.Width);
    }

    [Fact]
    public void WallRect_Shift_ile_eksene_sabitler_yatay_baskin()
    {
        var rect = PlanGeometry.WallRectFromPoints(new PlanPoint(100, 100), new PlanPoint(400, 260), 12, true);
        Assert.Equal(12, rect.Height);
        Assert.Equal(300, rect.Width);
        Assert.Equal(94, rect.Y);
    }

    [Fact]
    public void WallRect_Shift_ile_eksene_sabitler_dikey_baskin()
    {
        var rect = PlanGeometry.WallRectFromPoints(new PlanPoint(100, 100), new PlanPoint(160, 500), 12, true);
        Assert.Equal(12, rect.Width);
        Assert.Equal(400, rect.Height);
    }

    [Fact]
    public void WallRect_sifir_uzunlukta_asgari_boyutu_uygular()
    {
        var rect = PlanGeometry.WallRectFromPoints(new PlanPoint(100, 100), new PlanPoint(100, 100), 10, false);
        Assert.True(rect.Width >= PlanGeometry.MinSize);
    }

    [Fact]
    public void Clamp_degeri_aralikta_tutar()
    {
        Assert.Equal(10, PlanGeometry.Clamp(15, 0, 10));
        Assert.Equal(0, PlanGeometry.Clamp(-5, 0, 10));
        Assert.Equal(5, PlanGeometry.Clamp(5, 0, 10));
    }

    [Fact]
    public void ParseStyle_gecerli_JSON_cozer()
    {
        var style = PlanGeometry.ParseStyle("{\"fill\":\"#fff\",\"strokeWidth\":2}");
        Assert.Equal(new PlanElementStyle(Fill: "#fff", StrokeWidth: 2), style);
    }

    [Fact]
    public void ParseStyle_bozuk_JSON_veya_bos_degerde_guvenli_sonuc_doner()
    {
        Assert.Equal(PlanElementStyle.Empty, PlanGeometry.ParseStyle("bozuk"));
        Assert.Equal(PlanElementStyle.Empty, PlanGeometry.ParseStyle(null));
        Assert.Equal(PlanElementStyle.Empty, PlanGeometry.ParseStyle("\"metin\""));
    }

    [Fact]
    public void Her_tur_icin_tanim_ve_Turkce_etiket_vardir()
    {
        foreach (var type in PlanElementTypes.All)
        {
            var definition = PlanElementDefinitions.All[type];
            Assert.NotNull(definition);
            Assert.True(definition.Label.Length > 1);
            Assert.Equal(definition.Label, PlanElementDefinitions.Label(type));
            Assert.True(definition.DefaultWidth >= PlanGeometry.MinSize);
            Assert.True(definition.DefaultHeight >= PlanGeometry.MinSize);
        }
        Assert.Equal(12, PlanElementTypes.All.Count);
    }
}
