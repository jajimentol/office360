using Ofis360.Domain.Common;

namespace Ofis360.Domain.Tests;

/// <summary>Kaynak: tests/unit/date-and-analytics.test.ts "Tarih yardımcıları" (10 test).</summary>
public class DayKeyTests
{
    [Fact]
    public void Gun_anahtarini_UTC_gece_yarisina_cevirir()
    {
        var date = DayKeys.ToUtcDate("2026-08-21");
        Assert.Equal(DateTimeKind.Utc, date.Kind);
        Assert.Equal("2026-08-21T00:00:00.000Z", date.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
        Assert.Equal("2026-08-21", DayKeys.FromUtcDate(date));
    }

    [Fact]
    public void Gun_ekler_ve_ay_gecislerini_dogru_yapar()
    {
        Assert.Equal("2026-09-01", DayKeys.AddDays("2026-08-31", 1));
        Assert.Equal("2025-12-31", DayKeys.AddDays("2026-01-01", -1));
        Assert.Equal("2028-02-29", DayKeys.AddDays("2028-02-28", 1));
    }

    [Fact]
    public void Iki_tarih_arasindaki_gun_farkini_hesaplar()
    {
        Assert.Equal(14, DayKeys.DiffInDays("2026-08-21", "2026-09-04"));
        Assert.Equal(-14, DayKeys.DiffInDays("2026-09-04", "2026-08-21"));
    }

    [Fact]
    public void Gecersiz_gun_anahtarlarini_reddeder()
    {
        Assert.True(DayKeys.IsValid("2026-08-21"));
        Assert.False(DayKeys.IsValid("2026-13-01"));
        Assert.False(DayKeys.IsValid("2026-02-30"));
        Assert.False(DayKeys.IsValid("21-08-2026"));
        Assert.False(DayKeys.IsValid(42));
    }

    [Fact]
    public void Hafta_baslangicini_Pazartesi_olarak_hesaplar()
    {
        Assert.Equal("2026-08-17", DayKeys.StartOfWeek("2026-08-21"));
        Assert.Equal("2026-08-17", DayKeys.StartOfWeek("2026-08-17"));
        Assert.Equal("2026-08-17", DayKeys.StartOfWeek("2026-08-23"));
        Assert.Equal("2026-08-23", DayKeys.EndOfWeek("2026-08-21"));
    }

    [Fact]
    public void Hafta_sonlarini_tespit_eder()
    {
        Assert.True(DayKeys.IsWeekend("2026-08-22"));
        Assert.True(DayKeys.IsWeekend("2026-08-23"));
        Assert.False(DayKeys.IsWeekend("2026-08-21"));
    }

    [Fact]
    public void Turkce_tarih_ve_gun_adlarini_uretir()
    {
        Assert.Equal("21 Ağustos 2026 Cuma", DayKeys.FormatLong("2026-08-21"));
        Assert.Equal("21 Ağustos 2026", DayKeys.FormatLong("2026-08-21", withWeekday: false));
        Assert.Equal("Çarşamba", DayKeys.WeekdayLabel("2026-08-19"));
        Assert.Equal("Çar", DayKeys.WeekdayLabel("2026-08-19", shortForm: true));
    }

    [Fact]
    public void Saat_donusumlerini_yapar()
    {
        Assert.Equal("09:30", DayKeys.MinutesToTime(570));
        Assert.Equal("00:00", DayKeys.MinutesToTime(0));
        Assert.Equal(570, DayKeys.TimeToMinutes("09:30"));
        Assert.False(DayKeys.IsValidTime("24:00"));
        Assert.False(DayKeys.IsValidTime("09:60"));
        Assert.True(DayKeys.IsValidTime("09:05"));
    }

    [Fact]
    public void Gun_ve_dakikadan_tam_zaman_uretir()
    {
        var at = DayKeys.WithMinutes("2026-08-21", 570);
        Assert.Equal("2026-08-21T09:30:00.000Z", at.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
    }

    [Fact]
    public void Tarih_araligini_listeler()
    {
        Assert.Equal(["2026-08-19", "2026-08-20", "2026-08-21", "2026-08-22"], DayKeys.Range("2026-08-19", "2026-08-22"));
    }
}
