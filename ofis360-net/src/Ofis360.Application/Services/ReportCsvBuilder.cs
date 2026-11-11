using System.Globalization;
using Ofis360.Domain.Common;

namespace Ofis360.Application.Services;

public sealed record CsvReport(string Csv, string FileName);

/// <summary>Yonetici raporlarinin CSV disa aktarimi (kaynak: api/raporlar/disa-aktar). Raporlar anonimdir.</summary>
public static class ReportCsvBuilder
{
    private static string Tr(double value, int digits = 1) => value.ToString("F" + digits, CultureInfo.InvariantCulture).Replace('.', ',');
    private static string TrNumber(double value) => value.ToString(CultureInfo.InvariantCulture).Replace('.', ',');

    public static readonly IReadOnlySet<string> KnownTypes = new HashSet<string> { "ozet", "doluluk", "kat", "masa", "oda", "departman", "adil-kullanim" };

    public static CsvReport Build(string type, AnalyticsResult a, string fromDayKey, string toDayKey)
    {
        switch (type)
        {
            case "doluluk":
                return new CsvReport(TextUtils.ToCsv(["Tarih", "Rezervasyon sayısı", "Doluluk oranı (%)"],
                    a.OccupancyTrend.Select(r => new object?[] { r.DayKey, r.Reservations, Tr(r.OccupancyRate) })),
                    $"ofis360-doluluk-{fromDayKey}_{toDayKey}.csv");
            case "kat":
                return new CsvReport(TextUtils.ToCsv(["Kat", "Masa kapasitesi", "Rezervasyon sayısı", "Doluluk oranı (%)"],
                    a.FloorComparison.Select(r => new object?[] { r.Name, r.Capacity, r.Reservations, Tr(r.OccupancyRate) })),
                    $"ofis360-kat-dolulugu-{fromDayKey}_{toDayKey}.csv");
            case "masa":
                return new CsvReport(TextUtils.ToCsv(["Masa kodu", "Kat", "Bölge", "Popüler", "Rezervasyon sayısı", "Kullanım oranı (%)"],
                    a.DeskHeatmap.Select(r => new object?[] { r.Code, $"{r.FloorNumber}. Kat", r.ZoneName, r.Popular ? "Evet" : "Hayır", r.Reservations, Tr(r.UsageRate) })),
                    $"ofis360-masa-kullanimi-{fromDayKey}_{toDayKey}.csv");
            case "oda":
                return new CsvReport(TextUtils.ToCsv(["Oda kodu", "Oda adı", "Kat", "Kapasite", "Rezervasyon", "Dolu dakika", "Kullanım oranı (%)", "Ortalama katılımcı"],
                    a.RoomUsage.Select(r => new object?[] { r.Code, r.Name, $"{r.FloorNumber}. Kat", r.Capacity, r.Reservations, r.BookedMinutes, Tr(r.UsageRate), TrNumber(r.AverageAttendees) })),
                    $"ofis360-oda-kullanimi-{fromDayKey}_{toDayKey}.csv");
            case "departman":
                return new CsvReport(TextUtils.ToCsv(["Departman", "Çalışan sayısı", "Rezervasyon sayısı", "Haftalık ortalama ofis günü"],
                    a.DepartmentDistribution.Select(r => new object?[] { r.Name, r.EmployeeCount, r.Reservations, TrNumber(r.AverageOfficeDays) })),
                    $"ofis360-departman-{fromDayKey}_{toDayKey}.csv");
            case "adil-kullanim":
                return new CsvReport(TextUtils.ToCsv(["Rezervasyon aralığı", "Çalışan sayısı (anonim)"],
                    a.Fairness.Distribution.Select(r => new object?[] { r.Bucket, r.Employees })),
                    $"ofis360-adil-kullanim-{fromDayKey}_{toDayKey}.csv");
            default:
                var rows = new List<object?[]>
                {
                    new object?[] { "Tarih aralığı", $"{fromDayKey} - {toDayKey}" },
                    new object?[] { "Toplam masa rezervasyonu", a.Kpis.TotalDeskReservations },
                    new object?[] { "Toplam toplantı rezervasyonu", a.Kpis.TotalRoomReservations },
                    new object?[] { "Bugünkü rezervasyon", a.Kpis.TodayReservations },
                    new object?[] { "Bugünkü doluluk (%)", Tr(a.Kpis.TodayOccupancyRate) },
                    new object?[] { "Haftalık ortalama doluluk (%)", Tr(a.Kpis.WeeklyAverageOccupancy) },
                    new object?[] { "Kullanılmadı (no-show) oranı (%)", Tr(a.Kpis.NoShowRate) },
                    new object?[] { "İptal oranı (%)", Tr(a.Kpis.CancellationRate) },
                    new object?[] { "Toplantı odası kullanım oranı (%)", Tr(a.Kpis.RoomUsageRate) },
                    new object?[] { "Adil kullanım dengesizlik endeksi (Gini)", TrNumber(a.Fairness.GiniIndex) },
                    new object?[] { "En çok kullanılan 5 masanın payı (%)", Tr(a.Fairness.TopDeskShare) },
                };
                rows.AddRange(a.StatusBreakdown.Select(r => new object?[] { $"Durum: {ReservationStatusLabels.Map.GetValueOrDefault(r.Status, r.Status)}", r.Count }));
                return new CsvReport(TextUtils.ToCsv(["Gösterge", "Değer"], rows), $"ofis360-ozet-{fromDayKey}_{toDayKey}.csv");
        }
    }
}
