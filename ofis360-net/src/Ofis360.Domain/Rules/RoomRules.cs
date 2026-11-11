using System.Globalization;
using Ofis360.Domain.Common;

namespace Ofis360.Domain.Rules;

/// <summary>Adil kullanim kural motoru - toplanti odasi rezervasyonlari (kaynak: room-rules.ts).</summary>
public static class RoomRules
{
    public const int MinDurationMinutes = 15;

    private static RuleViolation Block(string ruleKey, string code, string title, string message, string? hint = null)
        => new(ruleKey, code, title, message, ViolationSeverity.Block, hint);

    /// <summary>Iki zaman araliginin kesisip kesismedigini dondurur (bitis dahil degil).</summary>
    public static bool Overlaps(int aStart, int aEnd, int bStart, int bEnd) => aStart < bEnd && bStart < aEnd;

    public static RuleEvaluation Evaluate(RoomRuleContext context, RuleSettings settings)
    {
        var violations = new List<RuleViolation>();
        var warnings = new List<RuleViolation>();
        var room = context.Room;
        var dayKey = context.DayKey;
        var todayKey = context.TodayKey;
        var startMinutes = context.StartMinutes;
        var endMinutes = context.EndMinutes;
        var attendeeCount = context.AttendeeCount;
        var calendar = context.Calendar ?? WorkCalendar.Default;

        // 1) Saat araligi tutarliligi
        if (endMinutes <= startMinutes)
        {
            violations.Add(Block("TIME_RANGE", "INVALID_RANGE", "Saat aralığı geçersiz",
                "Bitiş saati başlangıç saatinden sonra olmalıdır.",
                "Örneğin 10:00 - 11:00 gibi bir aralık seçin."));
        }
        else if (endMinutes - startMinutes < MinDurationMinutes)
        {
            violations.Add(Block("TIME_RANGE", "TOO_SHORT", "Toplantı süresi çok kısa",
                $"Toplantılar en az {MinDurationMinutes} dakika olmalıdır."));
        }

        // 2) Gecmis tarih / gecmis saat
        if (settings.NoPastDate.Enabled)
        {
            if (string.CompareOrdinal(dayKey, todayKey) < 0)
            {
                violations.Add(Block(RuleKeys.NoPastDate, "PAST_DATE", "Geçmiş tarih seçilemez",
                    $"{DayKeys.FormatLong(dayKey)} tarihi geçmişte kaldığı için rezervasyon oluşturulamaz.",
                    "Bugün veya sonrası için bir tarih seçin."));
            }
            else if (dayKey == todayKey && context.NowMinutes >= 0 && startMinutes < context.NowMinutes)
            {
                violations.Add(Block(RuleKeys.NoPastDate, "PAST_TIME", "Geçmiş saat seçilemez",
                    $"Bugün için saat {DayKeys.MinutesToTime(context.NowMinutes)} itibarıyla geçmiş bir saat aralığı seçtiniz.",
                    "Şu andan sonraki bir saat aralığı seçin."));
            }
        }

        // 3) Onceden rezervasyon penceresi
        if (settings.AdvanceBookingDays.Enabled)
        {
            var latest = calendar.LatestBookableDay(todayKey, settings.AdvanceBookingDays.Days);
            if (string.CompareOrdinal(dayKey, latest) > 0)
            {
                violations.Add(Block(RuleKeys.AdvanceBookingDays, "ADVANCE_WINDOW_EXCEEDED", "Rezervasyon penceresi dışında",
                    $"Toplantı odaları en fazla {settings.AdvanceBookingDays.Days} iş günü öncesinden rezerve edilebilir. Son uygun tarih: {DayKeys.FormatLong(latest)}.",
                    "Daha yakın bir tarih seçin."));
            }
        }

        if (settings.WorkingDaysOnly.Enabled && !calendar.IsWorkingDay(dayKey))
        {
            violations.Add(Block(RuleKeys.WorkingDaysOnly, "NON_WORKING_DAY", "Çalışma günü değil",
                $"{DayKeys.FormatLong(dayKey)} hafta sonu veya resmi tatil olduğu için rezervasyon yapılamaz.",
                "Bir çalışma günü seçin."));
        }

        if (settings.RoomSlotMinutes.Enabled && endMinutes > startMinutes)
        {
            var slot = settings.RoomSlotMinutes.Minutes;
            if (startMinutes % slot != 0 || endMinutes % slot != 0)
            {
                violations.Add(Block(RuleKeys.RoomSlotMinutes, "INVALID_SLOT", "Saat aralığı uygun değil",
                    $"Toplantı saatleri {slot} dakikalık aralıklarla seçilebilir.",
                    "Başlangıç ve bitiş saatini bu aralığa göre seçin."));
            }
        }

        // 4) Mesai saatleri
        if (settings.RoomWorkingHours.Enabled)
        {
            var workStart = settings.RoomWorkingHours.StartMinutes;
            var workEnd = settings.RoomWorkingHours.EndMinutes;
            if (startMinutes < workStart || endMinutes > workEnd)
            {
                violations.Add(Block(RuleKeys.RoomWorkingHours, "OUTSIDE_WORKING_HOURS", "Mesai saatleri dışında",
                    $"Toplantı odaları yalnızca {DayKeys.MinutesToTime(workStart)} - {DayKeys.MinutesToTime(workEnd)} saatleri arasında rezerve edilebilir.",
                    $"Seçtiğiniz aralık {DayKeys.MinutesToTime(startMinutes)} - {DayKeys.MinutesToTime(endMinutes)}. Mesai aralığına giren bir saat seçin."));
            }
        }

        // 5) Azami sure
        if (settings.RoomMaxDurationMinutes.Enabled && endMinutes > startMinutes)
        {
            var duration = endMinutes - startMinutes;
            if (duration > settings.RoomMaxDurationMinutes.Minutes)
            {
                var maxHours = settings.RoomMaxDurationMinutes.Minutes / 60.0;
                violations.Add(Block(RuleKeys.RoomMaxDurationMinutes, "DURATION_EXCEEDED", "Toplantı süresi üst sınırı aşıldı",
                    $"Tek bir toplantı en fazla {JsNumber(maxHours)} saat sürebilir. Seçtiğiniz süre {(duration / 60.0).ToString("F1", CultureInfo.InvariantCulture)} saat.",
                    "Uzun toplantılar için ardışık iki rezervasyon oluşturabilirsiniz."));
            }
        }

        // 6) Kapasite
        if (settings.RoomCapacityCheck.Enabled)
        {
            if (attendeeCount < 1)
            {
                violations.Add(Block(RuleKeys.RoomCapacityCheck, "INVALID_ATTENDEES", "Katılımcı sayısı geçersiz",
                    "Katılımcı sayısı en az 1 olmalıdır."));
            }
            else if (attendeeCount > room.Capacity)
            {
                violations.Add(Block(RuleKeys.RoomCapacityCheck, "CAPACITY_EXCEEDED", "Oda kapasitesi yetersiz",
                    $"{room.Name} odasının kapasitesi {room.Capacity} kişidir; {attendeeCount} katılımcı için uygun değildir.",
                    "Daha büyük kapasiteli bir oda seçin veya katılımcı sayısını güncelleyin."));
            }
        }

        if (context.Policy is { } policy && context.Requester is { } requester)
        {
            violations.AddRange(RoomAccess.Evaluate(policy, requester, dayKey, todayKey, calendar, attendeeCount));
        }

        // 7) Oda / bina / kat durumu
        if (!room.Active)
        {
            violations.Add(Block("ROOM_INACTIVE", "ROOM_INACTIVE", "Oda kullanıma kapalı",
                $"{room.Name} yönetim tarafından kullanıma kapatılmıştır.",
                "Diğer toplantı odalarını inceleyebilirsiniz."));
        }

        if (room.BuildingActive == false)
        {
            violations.Add(Block("BUILDING_INACTIVE", "BUILDING_INACTIVE", "Bu bina rezervasyona kapalı",
                $"{room.BuildingName ?? "Bina"} çalışan rezervasyonuna açık değildir."));
        }
        else if (!room.FloorReservable)
        {
            violations.Add(Block("FLOOR_NOT_RESERVABLE", "FLOOR_NOT_RESERVABLE", "Bu kat rezervasyona kapalı",
                $"{room.FloorName} çalışan rezervasyonuna açık değildir."));
        }

        if (context.Closure.Active)
        {
            var note = context.Closure.Note;
            violations.Add(Block("CLOSURE", "CLOSURE_ACTIVE", "Seçilen tarihte kapalı",
                !string.IsNullOrEmpty(note)
                    ? $"{DayKeys.FormatLong(dayKey)} tarihinde geçici kapatma uygulanıyor: {note}"
                    : $"{DayKeys.FormatLong(dayKey)} tarihinde bu oda geçici olarak kapalıdır.",
                "Başka bir tarih veya başka bir oda seçebilirsiniz."));
        }

        // 8) Cakisma
        var first = context.Conflicts.FirstOrDefault(item => Overlaps(startMinutes, endMinutes, item.StartMinutes, item.EndMinutes));
        if (first is not null)
        {
            violations.Add(Block("NO_CONFLICT", "ROOM_CONFLICT", "Seçilen saatte oda dolu",
                $"{room.Name} odası {DayKeys.MinutesToTime(first.StartMinutes)} - {DayKeys.MinutesToTime(first.EndMinutes)} aralığında zaten rezerve edilmiş.",
                "Uygun saat aralıklarından birini seçin veya alternatif odaları inceleyin."));
        }

        return new RuleEvaluation(violations.Count == 0, violations, warnings);
    }

    /// <summary>JS sayi gosterimi: 4 → "4", 2.5 → "2.5".</summary>
    private static string JsNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Bir odanin belirli gun icin bos saat araliklarini hesaplar (oneri motoru ve takvim gorunumu).</summary>
    public static IReadOnlyList<TimeRange> ComputeFreeSlots(
        int workStartMinutes,
        int workEndMinutes,
        IReadOnlyList<TimeRange> busy,
        int minDurationMinutes,
        int? notBeforeMinutes = null,
        int slotMinutes = 1)
    {
        // Bos araliklar slot sinirina hizalanir; yoksa onerilen saat kuraldan gecemez (or. su an 10:07 iken).
        int Align(int minutes) => (minutes + slotMinutes - 1) / slotMinutes * slotMinutes;

        var notBefore = notBeforeMinutes ?? -1;
        var sorted = busy.OrderBy(b => b.StartMinutes).ToList();

        var free = new List<TimeRange>();
        var cursor = Math.Max(workStartMinutes, notBefore);

        foreach (var slot in sorted)
        {
            if (slot.EndMinutes <= cursor) continue;
            if (slot.StartMinutes > cursor)
            {
                var start = Align(cursor);
                var end = Math.Min(slot.StartMinutes, workEndMinutes);
                if (end - start >= minDurationMinutes)
                {
                    free.Add(new TimeRange(start, end));
                }
            }
            cursor = Math.Max(cursor, slot.EndMinutes);
        }

        var lastStart = Align(cursor);
        if (workEndMinutes - lastStart >= minDurationMinutes)
        {
            free.Add(new TimeRange(lastStart, workEndMinutes));
        }

        return free;
    }
}
