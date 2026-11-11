namespace Ofis360.Domain.Common;

/// <summary>Kaynak: src/lib/config.ts. Marka adi ve varsayilan operasyonel degerler tek noktadan.</summary>
public static class AppConfig
{
    public const string Name = "Ofis360";
    public const string ShortName = "O360";
    public const string Tagline = "Hibrit Ofis ve Ortak Alan Rezervasyon Sistemi";
    public const string CompanyName = "Ofis360 Kurumsal";
    public const string SupportEmail = "ofis-destek@ofis360.local";
    public const string Locale = "tr-TR";
    public const string TimeZoneLabel = "Türkiye Saati (GMT+3)";
    public const string Version = "1.0.0";
}

/// <summary>Varsayilan calisma saatleri (dakika cinsinden, gun basindan itibaren).</summary>
public static class DefaultWorkingHours
{
    public const int StartMinutes = 8 * 60;
    public const int EndMinutes = 19 * 60;
    public const int SlotMinutes = 30;
}

/// <summary>Kat plani gorsel olcekleme sinirlari.</summary>
public static class FloorPlanConfig
{
    public const int DeskWidth = 46;
    public const int DeskHeight = 34;
    public const double MinZoom = 0.6;
    public const double MaxZoom = 1.8;
}

/// <summary>Sistem ayarlari icin anahtar sabitleri.</summary>
public static class SettingKeys
{
    public const string AppName = "app.name";
    public const string WorkdayStart = "office.workday_start";
    public const string WorkdayEnd = "office.workday_end";
    public const string OfficeDays = "office.working_days";
    public const string Announcement = "office.announcement";
    public const string LateCancelMinutes = "report.late_cancel_minutes";
    public const string ReportPersonDetail = "report.person_detail";
    public const string ContactEmail = "office.contact_email";
    /// <summary>"Ofiste kim var" sayfasi: true ise yalnizca yonetici ve admin gorur, false ise herkes.</summary>
    public const string PresenceManagersOnly = "presence.managers_only";
}
