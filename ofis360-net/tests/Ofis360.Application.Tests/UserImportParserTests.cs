using Ofis360.Application.Services;

namespace Ofis360.Application.Tests;

public class UserImportParserTests
{
    [Fact]
    public void Excel_noktali_virgul_BOM_ve_turkce_baslik_okur()
    {
        var result = UserImportParser.Parse("﻿E-posta;Ad Soyad;Unvan;KADEME;Departman;Sekreter\r\nAli@Ray.com;Ali Veli;Uzman;Uzman;Hasar;Hayır\r\n");
        Assert.Null(result.Error);
        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.Line);
        Assert.Equal("ali@ray.com", row.Email);
        Assert.Equal("Ali Veli", row.FullName);
        Assert.Equal("Hasar", row.Department);
        Assert.Equal("Hayır", row.Secretary);
    }

    [Fact]
    public void Virgul_ve_tirnakli_alan_destekler()
    {
        var row = Assert.Single(UserImportParser.Parse("email,departman\na@b.co,\"Satış, Pazarlama \"\"A\"\"\"\n").Rows);
        Assert.Equal("Satış, Pazarlama \"A\"", row.Department);
    }

    [Fact]
    public void Sutun_sirasi_onemsiz_eksik_ve_bos_hucre_null()
    {
        var row = Assert.Single(UserImportParser.Parse("kademe;e-posta\n;a@b.co\n").Rows);
        Assert.Null(row.Grade);
        Assert.Null(row.FullName);
        Assert.Equal("a@b.co", row.Email);
    }

    [Fact]
    public void Bos_satirlari_atlar_ve_satir_numarasini_korur()
    {
        var rows = UserImportParser.Parse("email\n\na@b.co\n;\nc@d.co").Rows;
        Assert.Equal([3, 5], rows.Select(r => r.Line));
    }

    [Fact]
    public void Eposta_basligi_yoksa_hata()
    {
        Assert.NotNull(UserImportParser.Parse("ad soyad;kademe\nAli;Uzman\n").Error);
    }

    [Fact]
    public void Satir_sinirini_asan_dosyayi_reddeder()
    {
        var csv = "email\n" + string.Join("\n", Enumerable.Range(0, UserImportParser.MaxRows + 1).Select(i => $"u{i}@b.co"));
        Assert.NotNull(UserImportParser.Parse(csv).Error);
    }
}
