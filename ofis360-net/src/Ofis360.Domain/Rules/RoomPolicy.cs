using Ofis360.Domain.Common;

namespace Ofis360.Domain.Rules;

/// <param name="BypassPriority">Bu kademe oncelikli kisilerin penceresini beklemez (or. GM, GMY).</param>
public sealed record RoomGradeRule(string Grade, int MinAttendees, bool BypassPriority = false);

/// <summary>
/// Odaya ozel erisim kurallari. GradeRules bossa oda herkese aciktir; doluysa yalnizca listedeki kademeler rezerve edebilir.
/// PriorityUserIds doluysa digerleri odayi yalnizca son PriorityOpenDays is gununde alabilir.
/// </summary>
public sealed record RoomPolicy(
    bool SecretaryOnly,
    IReadOnlyList<RoomGradeRule> GradeRules,
    IReadOnlyCollection<string> PriorityUserIds,
    int? PriorityOpenDays)
{
    public static readonly RoomPolicy Open = new(false, [], [], null);
}

/// <param name="UserId">Rezervasyonun sahibi.</param>
/// <param name="Grade">Sahibin kademesi.</param>
/// <param name="ActorIsSecretary">Islemi yapan Sekreter ise oda kisitlari uygulanmaz.</param>
public sealed record RoomRequester(string UserId, string? Grade, bool ActorIsSecretary);

public static class RoomAccess
{
    private static RuleViolation Block(string code, string title, string message, string? hint = null) =>
        new("ROOM_POLICY", code, title, message, ViolationSeverity.Block, hint);

    /// <summary>Kisinin bu odada uymasi gereken en az katilimci sayisi.</summary>
    public static int MinAttendees(RoomPolicy policy, RoomRequester requester)
    {
        if (requester.ActorIsSecretary || policy.SecretaryOnly || policy.GradeRules.Count == 0) return 1;
        var rule = policy.GradeRules.FirstOrDefault(r => r.Grade == requester.Grade);
        return rule?.MinAttendees ?? policy.GradeRules.Min(r => r.MinAttendees);
    }

    /// <summary>attendeeCount null ise (oda listesi) yalnizca erisim ve oncelik kontrol edilir.</summary>
    public static IReadOnlyList<RuleViolation> Evaluate(
        RoomPolicy policy, RoomRequester requester, string dayKey, string todayKey, WorkCalendar calendar, int? attendeeCount)
    {
        if (requester.ActorIsSecretary) return [];

        if (policy.SecretaryOnly)
        {
            return [Block("SECRETARY_ONLY", "Bu oda sekreterlik üzerinden ayrılır", "Bu toplantı odasını yalnızca sekreter rezerve edebilir.", "Sekreterlik ile iletişime geçin.")];
        }

        var violations = new List<RuleViolation>();
        var isPriorityUser = policy.PriorityUserIds.Contains(requester.UserId);

        if (policy.GradeRules.Count > 0)
        {
            var allowed = isPriorityUser || policy.GradeRules.Any(r => r.Grade == requester.Grade);
            if (!allowed)
            {
                return [Block("GRADE_NOT_ALLOWED", "Bu oda kademenize açık değil", "Bu toplantı odası kademeniz için rezervasyona açık değildir.", "Diğer odaları inceleyebilirsiniz.")];
            }

            var minimum = MinAttendees(policy, requester);
            if (attendeeCount is int count && count < minimum)
            {
                violations.Add(Block("MIN_ATTENDEES", "Katılımcı sayısı yetersiz", $"Bu oda için en az {minimum} katılımcı gerekir.", "Katılımcı sayısını artırın veya daha küçük bir oda seçin."));
            }
        }

        var bypassesPriority = policy.GradeRules.Any(r => r.Grade == requester.Grade && r.BypassPriority);
        if (policy.PriorityUserIds.Count > 0 && !isPriorityUser && !bypassesPriority && policy.PriorityOpenDays is int days)
        {
            if (string.CompareOrdinal(dayKey, calendar.LatestBookableDay(todayKey, days)) > 0)
            {
                var opensOn = calendar.OpensOn(dayKey, days);
                violations.Add(Block("PRIORITY_WINDOW", "Oda henüz rezervasyona açılmadı",
                    $"Bu oda öncelikli olarak ayrılmıştır; {DayKeys.FormatLong(dayKey, withWeekday: false)} için {DayKeys.FormatLong(opensOn, withWeekday: false)} tarihinde herkese açılacak.",
                    "Daha yakın bir tarih veya başka bir oda seçebilirsiniz."));
            }
        }

        return violations;
    }
}
