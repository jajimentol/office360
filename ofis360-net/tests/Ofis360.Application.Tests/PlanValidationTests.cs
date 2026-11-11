using Ofis360.Application.Validation;

namespace Ofis360.Application.Tests;

/// <summary>Kaynak: tests/unit/plan-geometry.test.ts "Kroki doğrulama şeması" (6 test). Veritabani gerekmez.</summary>
public class PlanValidationTests
{
    private static readonly PlanElementInput ValidElement = new(
        Id: "oge-1", Type: "ROOM", Label: "A Bölgesi", X: 10, Y: 10, Width: 200, Height: 120, Rotation: 0,
        Style: new PlanStyleInput(Fill: "#ffffff", Stroke: "#94a3b8", StrokeWidth: 2), LayerOrder: 10, Locked: false, Visible: true);

    private static readonly PlanElementValidator ElementValidator = new();
    private static readonly SavePlanValidator SaveValidator = new();

    [Fact]
    public void Gecerli_ogeyi_kabul_eder()
    {
        var result = ElementValidator.Validate(ValidElement);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        Assert.Equal("ROOM", ValidElement.Type);
    }

    [Fact]
    public void Desteklenmeyen_oge_turunu_reddeder()
    {
        Assert.False(ElementValidator.Validate(ValidElement with { Type = "HELIKOPTER_PISTI" }).IsValid);
    }

    [Fact]
    public void Asgari_boyutun_altindaki_ogeyi_reddeder()
    {
        Assert.False(ElementValidator.Validate(ValidElement with { Width = 2 }).IsValid);
    }

    [Fact]
    public void Gecersiz_bagli_kaynak_turunu_reddeder()
    {
        Assert.False(ElementValidator.Validate(ValidElement with { LinkedResourceType = "ARABA", LinkedResourceId = "x" }).IsValid);
    }

    [Fact]
    public void Kaydetme_yukunu_dogrular_ve_varsayilanlari_uygular()
    {
        var input = new SavePlanInput("kat-1", [ValidElement]);
        var result = SaveValidator.Validate(input);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        Assert.Empty(input.DeskPlacementsOrEmpty);
        Assert.Empty(input.RoomPlacementsOrEmpty);
    }

    [Fact]
    public void Oge_sayisi_ust_sinirini_uygular()
    {
        var many = Enumerable.Range(0, 401).Select(i => ValidElement with { Id = $"oge-{i}" }).ToList();
        var result = SaveValidator.Validate(new SavePlanInput("kat-1", many));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("en fazla 400"));
    }
}
