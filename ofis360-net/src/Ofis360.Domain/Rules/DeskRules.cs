using Ofis360.Domain.Common;

namespace Ofis360.Domain.Rules;

/// <summary>Adil kullanim hakki ozeti; calisan ana sayfasinda ve rezervasyon ekraninda gosterilir.</summary>
public sealed record FairUseSummary(
    int WeeklyLimit,
    int WeeklyUsed,
    int WeeklyRemaining,
    int AdvanceDays,
    int PopularWindowSize,
    int PopularMaxUsage,
    int PopularUsed,
    bool PopularRestricted,
    string WeekStartKey);

/// <summary>
/// Adil kullanim kural motoru - masa rezervasyonlari (kaynak: desk-rules.ts).
/// Saf fonksiyonlar: veritabanina veya yan etkiye bagimli degildir; tum girdi DeskRuleContext ile verilir.
/// </summary>
public static class DeskRules
{
    private static RuleViolation Block(string ruleKey, string code, string title, string message, string? hint = null)
        => new(ruleKey, code, title, message, ViolationSeverity.Block, hint);

    private static RuleViolation Warn(string ruleKey, string code, string title, string message, string? hint = null)
        => new(ruleKey, code, title, message, ViolationSeverity.Warn, hint);

    /// <summary>
    /// Kullanicinin ayni populer masayi ust uste kac ofis gunudur kullandigini hesaplar.
    /// Hedef gun listeye eklenmis gibi dusunulur; hedefin hem oncesindeki hem sonrasindaki ardisik
    /// ayni-masa kayitlari sayilir. Donen deger hedef gun dahil seri uzunlugudur.
    /// </summary>
    public static int ComputeConsecutiveDeskStreak(IReadOnlyList<UserDeskHistoryItem> history, string targetDayKey, string deskId)
    {
        var others = history
            .Where(item => item.DayKey != targetDayKey)
            .OrderBy(item => item.DayKey, StringComparer.Ordinal)
            .ToList();

        var insertIndex = others.FindIndex(item => string.CompareOrdinal(item.DayKey, targetDayKey) > 0);
        var position = insertIndex == -1 ? others.Count : insertIndex;

        var streak = 1;
        for (var i = position - 1; i >= 0; i--)
        {
            if (others[i].DeskId == deskId) streak++;
            else break;
        }
        for (var i = position; i < others.Count; i++)
        {
            if (others[i].DeskId == deskId) streak++;
            else break;
        }
        return streak;
    }

    /// <summary>Son N rezervasyon icinde populer masa kullanim sayisi; hedef gun disindaki en guncel kayitlar.</summary>
    public static (int PopularCount, int SampleSize) ComputePopularUsageInWindow(
        IReadOnlyList<UserDeskHistoryItem> history, string targetDayKey, int windowSize)
    {
        var recent = history
            .Where(item => item.DayKey != targetDayKey)
            .OrderByDescending(item => item.DayKey, StringComparer.Ordinal)
            .Take(Math.Max(0, windowSize))
            .ToList();

        return (recent.Count(item => item.Popular), recent.Count);
    }

    /// <summary>
    /// Bir masa rezervasyon talebini tum adil kullanim kurallarina gore degerlendirir.
    /// Kurallar sirayla degerlendirilir ve gecerli tum ihlaller birlikte dondurulur.
    /// </summary>
    public static RuleEvaluation Evaluate(DeskRuleContext context, RuleSettings settings)
    {
        var violations = new List<RuleViolation>();
        var warnings = new List<RuleViolation>();
        var desk = context.Desk;
        var user = context.User;
        var dayKey = context.DayKey;
        var todayKey = context.TodayKey;
        var calendar = context.Calendar ?? WorkCalendar.Default;

        // 1) Gecmis tarih kontrolu
        if (settings.NoPastDate.Enabled && string.CompareOrdinal(dayKey, todayKey) < 0)
        {
            violations.Add(Block(RuleKeys.NoPastDate, "PAST_DATE", "Geçmiş tarih seçilemez",
                $"{DayKeys.FormatLong(dayKey)} tarihi geçmişte kaldığı için rezervasyon oluşturulamaz.",
                "Bugün veya sonrası için bir tarih seçin."));
        }

        // 2) Onceden rezervasyon penceresi
        if (settings.AdvanceBookingDays.Enabled)
        {
            var latest = calendar.LatestBookableDay(todayKey, settings.AdvanceBookingDays.Days);
            if (string.CompareOrdinal(dayKey, latest) > 0)
            {
                violations.Add(Block(RuleKeys.AdvanceBookingDays, "ADVANCE_WINDOW_EXCEEDED", "Rezervasyon penceresi dışında",
                    $"Rezervasyonlar en fazla {settings.AdvanceBookingDays.Days} iş günü öncesinden yapılabilir. Son uygun tarih: {DayKeys.FormatLong(latest)}.",
                    "Daha yakın bir tarih seçin; pencere her iş günü bir gün ileri kayar."));
            }
        }

        if (settings.WorkingDaysOnly.Enabled && !calendar.IsWorkingDay(dayKey))
        {
            violations.Add(Block(RuleKeys.WorkingDaysOnly, "NON_WORKING_DAY", "Çalışma günü değil",
                $"{DayKeys.FormatLong(dayKey)} hafta sonu veya resmi tatil olduğu için rezervasyon yapılamaz.",
                "Bir çalışma günü seçin."));
        }

        // 3) Bina / kat / bolge rezervasyona acik mi
        if (desk.BuildingActive == false)
        {
            violations.Add(Block("BUILDING_INACTIVE", "BUILDING_INACTIVE", "Bu bina rezervasyona kapalı",
                $"{desk.BuildingName ?? "Bina"} çalışan rezervasyonuna açık değildir.",
                "Aktif binadaki katlardan birini seçin."));
        }
        else if (!desk.FloorReservable)
        {
            violations.Add(Block("FLOOR_NOT_RESERVABLE", "FLOOR_NOT_RESERVABLE", "Bu kat rezervasyona kapalı",
                $"{desk.FloorName} çalışan rezervasyonuna açık değildir.",
                "Rezervasyona açık katlardan birini seçin."));
        }
        else if (!desk.ZoneReservable)
        {
            violations.Add(Block("ZONE_NOT_RESERVABLE", "ZONE_NOT_RESERVABLE", "Bu bölge rezervasyona kapalı",
                $"{desk.ZoneName} bölgesi rezervasyona açık değildir.",
                "Aynı kattaki diğer çalışma bölgelerini deneyin."));
        }

        // 4) Masa krokiye yerlestirilmis mi
        if (!desk.Placed)
        {
            violations.Add(Block("DESK_NOT_PLACED", "DESK_NOT_PLACED", "Masa henüz kat planına yerleştirilmedi",
                $"{desk.Code} kodlu masa envantere eklenmiş ancak {desk.FloorName} krokisinde henüz konumlandırılmamıştır.",
                "Yönetim masayı kat planına yerleştirdiğinde rezervasyona açılacaktır."));
        }

        // 5) Masa aktif mi / kapatma kaydi var mi
        if (!desk.Active)
        {
            violations.Add(Block("DESK_INACTIVE", "DESK_INACTIVE", "Masa kullanıma kapalı",
                $"{desk.Code} kodlu masa yönetim tarafından kullanıma kapatılmıştır.",
                "Yakınındaki uygun masalardan birini seçebilirsiniz."));
        }

        if (context.Closure.Active)
        {
            var note = context.Closure.Note;
            violations.Add(Block("CLOSURE", "CLOSURE_ACTIVE", "Seçilen tarihte kapalı",
                !string.IsNullOrEmpty(note)
                    ? $"{DayKeys.FormatLong(dayKey)} tarihinde geçici kapatma uygulanıyor: {note}"
                    : $"{DayKeys.FormatLong(dayKey)} tarihinde bu alan geçici olarak kapalıdır.",
                "Başka bir tarih veya başka bir masa seçebilirsiniz."));
        }

        // 6) Erisilebilirlik icin ayrilmis masa
        if (settings.AccessibleDeskRestriction.Enabled && desk.AccessibleOnly && !user.AccessibilityApproved)
        {
            violations.Add(Block(RuleKeys.AccessibleDeskRestriction, "ACCESSIBLE_ONLY", "Erişilebilirlik için ayrılmış masa",
                $"{desk.Code} kodlu masa sağlık ve erişilebilirlik gerekçesiyle ayrılmıştır; yalnızca yetkili profiller tarafından seçilebilir.",
                "İhtiyacınız varsa İnsan Kaynakları üzerinden erişilebilirlik yetkisi tanımlatabilirsiniz."));
        }

        // 7) Cakisan rezervasyon
        if (context.OccupiedBy is not null)
        {
            var isOwn = context.OccupiedBy.UserId == user.Id;
            violations.Add(Block("NO_CONFLICT",
                isOwn ? "ALREADY_OWNED" : "DESK_OCCUPIED",
                isOwn ? "Bu masa zaten sizin" : "Masa dolu",
                isOwn
                    ? $"{desk.Code} kodlu masa {DayKeys.FormatLong(dayKey)} tarihi için zaten sizin adınıza rezerve edilmiş."
                    : $"{desk.Code} kodlu masa {DayKeys.FormatLong(dayKey)} tarihinde başka bir çalışan tarafından rezerve edilmiş.",
                isOwn
                    ? "Rezervasyonlarım ekranından mevcut kaydınızı görüntüleyebilirsiniz."
                    : "Aynı bölgedeki uygun masalardan birini seçebilirsiniz."));
        }

        // 8) Ayni gun tek masa
        if (settings.OneDeskPerDay.Enabled && context.SameDayReservation is not null)
        {
            violations.Add(Block(RuleKeys.OneDeskPerDay, "ONE_DESK_PER_DAY", "Aynı gün için bir masa hakkınız var",
                $"{DayKeys.FormatLong(dayKey)} tarihi için zaten {context.SameDayReservation.DeskCode} kodlu masayı rezerve etmişsiniz.",
                "Farklı bir masaya geçmek için önce mevcut rezervasyonunuzu iptal edin."));
        }

        // 9) Haftalik limit
        if (settings.WeeklyDeskLimit.Enabled)
        {
            var limit = settings.WeeklyDeskLimit.Limit;
            if (context.WeeklyReservationCount >= limit)
            {
                violations.Add(Block(RuleKeys.WeeklyDeskLimit, "WEEKLY_LIMIT_REACHED", "Haftalık rezervasyon hakkınız doldu",
                    $"{DayKeys.FormatLong(DayKeys.StartOfWeek(dayKey), withWeekday: false)} haftası için tanımlı {limit} masa rezervasyonu hakkınızın tamamını kullandınız.",
                    "Bu hafta içindeki bir rezervasyonunuzu iptal ederek yeni bir gün seçebilir veya gelecek haftaya rezervasyon oluşturabilirsiniz."));
            }
        }

        // 10) Populer masa ardisik kullanim limiti
        if (settings.PopularDeskStreakLimit.Enabled && desk.Popular)
        {
            var limit = settings.PopularDeskStreakLimit.Limit;
            var streak = ComputeConsecutiveDeskStreak(context.History, dayKey, desk.Id);
            if (streak > limit)
            {
                violations.Add(Block(RuleKeys.PopularDeskStreakLimit, "POPULAR_STREAK_LIMIT", "Aynı popüler masa arka arkaya kullanılamaz",
                    $"{desk.Code} popüler masalardan biri ve arka arkaya en fazla {limit} ofis günü kullanılabilir. Bu rezervasyon seriyi {streak} güne çıkarıyor.",
                    "Ortak alanların adil dağılımı için bu kez farklı bir masa seçin; bir gün ara verdikten sonra bu masayı yeniden seçebilirsiniz."));
            }
        }

        // 11) Populer masa kullanim payi
        if (settings.PopularShareLimit.Enabled && desk.Popular)
        {
            var windowSize = settings.PopularShareLimit.WindowSize;
            var maxPopular = settings.PopularShareLimit.MaxPopular;
            var (popularCount, sampleSize) = ComputePopularUsageInWindow(context.History, dayKey, windowSize);
            if (sampleSize >= Math.Min(windowSize, maxPopular + 1) && popularCount > maxPopular)
            {
                violations.Add(Block(RuleKeys.PopularShareLimit, "POPULAR_SHARE_LIMIT", "Popüler masa kullanımınız geçici olarak sınırlandı",
                    $"Son {sampleSize} rezervasyonunuzun {popularCount} tanesi pencere yanı / popüler masalarda. Tanımlı üst sınır {windowSize} rezervasyonda {maxPopular} kullanımdır.",
                    "Standart masalardan birini seçtiğinizde popüler masa hakkınız otomatik olarak yeniden açılır."));
            }
        }

        // Bilgilendirme: hafta sonu rezervasyonu
        if (DayKeys.IsWeekend(dayKey) && !settings.WorkingDaysOnly.Enabled)
        {
            warnings.Add(Warn("WEEKEND_NOTICE", "WEEKEND_NOTICE", "Hafta sonu rezervasyonu",
                $"{DayKeys.FormatLong(dayKey)} bir hafta sonu günü. Ofis hizmetleri sınırlı olabilir."));
        }

        return new RuleEvaluation(violations.Count == 0, violations, warnings);
    }

    public static FairUseSummary BuildFairUseSummary(
        RuleSettings settings,
        IReadOnlyList<UserDeskHistoryItem> history,
        int weeklyReservationCount,
        string referenceDayKey)
    {
        var (popularCount, sampleSize) = ComputePopularUsageInWindow(history, string.Empty, settings.PopularShareLimit.WindowSize);

        // Kaynakta kural kapaliyken limit Infinity → ozet 0 olarak raporlanir.
        var weeklyEnabled = settings.WeeklyDeskLimit.Enabled;
        var weeklyLimit = weeklyEnabled ? settings.WeeklyDeskLimit.Limit : 0;
        var weeklyRemaining = weeklyEnabled ? Math.Max(0, settings.WeeklyDeskLimit.Limit - weeklyReservationCount) : 0;

        var share = settings.PopularShareLimit;
        return new FairUseSummary(
            WeeklyLimit: weeklyLimit,
            WeeklyUsed: weeklyReservationCount,
            WeeklyRemaining: weeklyRemaining,
            AdvanceDays: settings.AdvanceBookingDays.Days,
            PopularWindowSize: share.WindowSize,
            PopularMaxUsage: share.MaxPopular,
            PopularUsed: popularCount,
            PopularRestricted: share.Enabled
                && sampleSize >= Math.Min(share.WindowSize, share.MaxPopular + 1)
                && popularCount > share.MaxPopular,
            WeekStartKey: DayKeys.StartOfWeek(referenceDayKey));
    }

    /// <summary>Saat araligini okunabilir metne cevirir (oneri mesajlarinda kullanilir).</summary>
    public static string FormatMinuteRange(int startMinutes, int endMinutes)
        => $"{DayKeys.MinutesToTime(startMinutes)} - {DayKeys.MinutesToTime(endMinutes)}";
}
