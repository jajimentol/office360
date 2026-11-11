using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Services;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Seed;

public sealed class EkBinaRuleReport
{
    public int RulesAdded { get; set; }
    public int DefaultsUpdated { get; set; }
    public int OverridesAdded { get; set; }
    public int HolidaysAdded { get; set; }
}

/// <summary>
/// Ek Bina Faz 1 formunun kural degerlerini yukler. Admin panelindeki degisiklikleri ezmez: varsayilanlar yalnizca hala
/// fabrika degerindeyse, kademe degerleri yalnizca o hucre bossa yazilir. Bu yuzden tekrar calistirmak guvenlidir.
/// </summary>
public static class EkBinaRuleSeeder
{
    private static readonly (string Key, int Factory, int Form)[] DefaultValues =
    [
        (RuleKeys.AdvanceBookingDays, 14, 2),
        (RuleKeys.WeeklyDeskLimit, 3, 2),
        (RuleKeys.RoomMaxDurationMinutes, 240, 540),
        (RuleKeys.RoomSlotMinutes, 15, 30),
    ];

    private static readonly string[] ManagerAndAbove = [Grades.Manager, Grades.SeniorManager, Grades.Director, Grades.DeputyGm, Grades.Gm];

    private static readonly (string Date, string Name)[] FixedHolidays =
    [
        ("2027-01-01", "Yılbaşı"),
        ("2027-04-23", "Ulusal Egemenlik ve Çocuk Bayramı"),
        ("2027-05-01", "Emek ve Dayanışma Günü"),
        ("2027-05-19", "Atatürk'ü Anma, Gençlik ve Spor Bayramı"),
        ("2027-07-15", "Demokrasi ve Milli Birlik Günü"),
        ("2027-08-30", "Zafer Bayramı"),
        ("2027-10-29", "Cumhuriyet Bayramı"),
    ];

    public static async Task<EkBinaRuleReport> ApplyAsync(Ofis360DbContext db)
    {
        var report = new EkBinaRuleReport();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var rules = await db.ReservationRules.ToListAsync();
        foreach (var definition in RuleDefinitions.All.Where(d => rules.All(r => r.Key != d.Key)))
        {
            var entity = RuleService.ToEntity(definition);
            db.ReservationRules.Add(entity);
            rules.Add(entity);
            report.RulesAdded++;
        }

        var advance = rules.Single(r => r.Key == RuleKeys.AdvanceBookingDays);
        advance.Description = RuleDefinitions.AdvanceBookingDescription;
        advance.Unit = RuleDefinitions.AdvanceBookingUnit;

        foreach (var (key, factory, form) in DefaultValues)
        {
            var rule = rules.Single(r => r.Key == key);
            if (rule.IntValue != factory) continue;
            rule.IntValue = form;
            report.DefaultsUpdated++;
        }

        var existing = await db.ReservationRuleOverrides.Where(o => o.BuildingId == null).ToListAsync();

        // Ilk uygulamada acilir; sonradan admin kapattiysa tekrar acilmaz.
        var workingDays = rules.Single(r => r.Key == RuleKeys.WorkingDaysOnly);
        if (!workingDays.Enabled && existing.All(o => o.RuleKey != RuleKeys.WorkingDaysOnly))
        {
            workingDays.Enabled = true;
            report.DefaultsUpdated++;
        }
        void Set(string key, string grade, bool? enabled, int? value)
        {
            if (existing.Any(o => o.RuleKey == key && o.Grade == grade)) return;
            db.ReservationRuleOverrides.Add(new ReservationRuleOverride { RuleKey = key, Grade = grade, Enabled = enabled, IntValue = value });
            report.OverridesAdded++;
        }

        foreach (var grade in new[] { Grades.Manager, Grades.SeniorManager, Grades.Director }) Set(RuleKeys.AdvanceBookingDays, grade, null, 5);
        foreach (var grade in new[] { Grades.DeputyGm, Grades.Gm }) Set(RuleKeys.AdvanceBookingDays, grade, null, 15);
        foreach (var grade in ManagerAndAbove) Set(RuleKeys.WorkingDaysOnly, grade, false, null);

        var holidayDates = await db.Holidays.Select(h => h.Date).ToListAsync();
        foreach (var (date, name) in FixedHolidays)
        {
            var day = DayKeys.ToDateOnly(date);
            if (holidayDates.Contains(day)) continue;
            db.Holidays.Add(new Holiday { Date = day, Name = name });
            report.HolidaysAdded++;
        }

        // Kullanilmayan eski ayar: toleransin tek kaynagi NO_SHOW_RELEASE kuralidir.
        await db.SystemSettings.Where(s => s.Key == "office.no_show_grace_minutes").ExecuteDeleteAsync();

        var settingKeys = await db.SystemSettings.Select(s => s.Key).ToListAsync();
        if (!settingKeys.Contains(SettingKeys.LateCancelMinutes))
        {
            db.SystemSettings.Add(new SystemSetting { Key = SettingKeys.LateCancelMinutes, Value = "120", Label = "Geç iptal süresi (dakika)", Description = "Toplantıya bundan az kala yapılan iptaller raporda geç iptal sayılır.", Type = SettingTypes.Number, Group = "OFFICE", Order = 8 });
        }
        if (!settingKeys.Contains(SettingKeys.ReportPersonDetail))
        {
            db.SystemSettings.Add(new SystemSetting { Key = SettingKeys.ReportPersonDetail, Value = "false", Label = "Katılım raporunda kişi adları", Description = "İK ve KVKK onayı alınmadan açılmamalıdır.", Type = SettingTypes.Boolean, Group = "OFFICE", Order = 9 });
        }

        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return report;
    }

    public static void PrintReport(TextWriter log, EkBinaRuleReport report)
    {
        log.WriteLine($"Eklenen yeni kural       : {report.RulesAdded}");
        log.WriteLine($"Guncellenen varsayilan   : {report.DefaultsUpdated}");
        log.WriteLine($"Eklenen kademe degeri    : {report.OverridesAdded}");
        log.WriteLine($"Eklenen sabit resmi tatil: {report.HolidaysAdded}");
        log.WriteLine("Dini bayramlar her yil degistigi icin admin panelinden girilmelidir.");
    }
}
