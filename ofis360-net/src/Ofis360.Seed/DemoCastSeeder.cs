using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Seed;

/// <summary>
/// Yalnizca GELISTIRME verisi: demoyu kademe basina bir kisiye indirir. Hicbir kayit silinmez; digerleri pasife alinir,
/// gelecek tarihli rezervasyonlari normal pasiflestirmedeki gibi iptal edilir. Idempotenttir.
/// Sistemde ornek olmayan aktif kullanici varsa hicbir sey yapmadan durur.
/// </summary>
public static class DemoCastSeeder
{
    private const string ExampleDomain = "@ofis360.local";
    private const string CancelReason = "Kullanıcı hesabı pasife alındı.";
    private const string RegionalEmail = "bolge1.demo@ofis360.local";
    private const string RegionalRoomCode = "PT36-B02";

    private static readonly (string Email, string Grade, string Title)[] Cast =
    [
        ("ahmet.koc@ofis360.local", Grades.Gm, "Genel Müdür"),
        ("alper.cakir@ofis360.local", Grades.DeputyGm, "Genel Müdür Yardımcısı"),
        ("baris.soydan@ofis360.local", Grades.Director, "Satış Direktörü"),
        ("ceren.arslan@ofis360.local", Grades.SeniorManager, "Operasyon Kıdemli Müdürü"),
        ("mert.demir@ofis360.local", Grades.Manager, "Operasyon Müdürü"),
        ("pinar.uysal@ofis360.local", Grades.AssistantManager, "Finans Müdür Yardımcısı"),
        ("elif.yilmaz@ofis360.local", Grades.SeniorSpecialist, "Kıdemli Yazılım Uzmanı"),
        ("burak.sahin@ofis360.local", Grades.Specialist, "Finans Uzmanı"),
        ("bahar.ozdemir@ofis360.local", Grades.AssistantSpecialist, "Kurumsal Satış Uzman Yardımcısı"),
        (RegionalEmail, Grades.Manager, "Anadolu Bölge 1 Müdürü"),
        ("sekreter.demo@ofis360.local", Grades.Specialist, "Yönetici Asistanı"),
        ("zeynep.kaya@ofis360.local", Grades.Manager, "İş Yeri Deneyimi Yöneticisi"),
    ];

    public static async Task<int> ApplyAsync(Ofis360DbContext db, DateOnly today, TextWriter log)
    {
        var emails = Cast.Select(c => c.Email).ToList();

        var foreign = await db.Users.CountAsync(u => u.Active && !u.Email.EndsWith(ExampleDomain));
        if (foreign > 0) throw new InvalidOperationException($"{foreign} aktif kullanici ornek hesap degil; komut durduruldu.");

        var keep = await db.Users.Where(u => emails.Contains(u.Email)).ToListAsync();
        var missing = emails.Except(keep.Select(u => u.Email)).ToList();
        if (missing.Count > 0) throw new InvalidOperationException($"Hesap bulunamadi: {string.Join(", ", missing)}");

        var now = DateTimeOffset.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync();

        foreach (var (email, grade, title) in Cast)
        {
            var user = keep.Single(u => u.Email == email);
            user.Grade = grade;
            user.Title = title;
            if (grade == Grades.AssistantManager) user.Role = Roles.Employee; // Mudur yardimcisi yonetici raporlarini gormez.
            user.Active = true;
            user.IsDemoAccount = true;
            user.UpdatedAt = now;
        }

        var regional = keep.Single(u => u.Email == RegionalEmail);
        var room = await db.MeetingRooms.Include(r => r.PriorityUsers).SingleOrDefaultAsync(r => r.Code == RegionalRoomCode);
        if (room is not null && room.PriorityUsers.Any(p => p.UserId != regional.Id))
        {
            db.RoomPriorityUsers.RemoveRange(room.PriorityUsers.Where(p => p.UserId != regional.Id));
            if (room.PriorityUsers.All(p => p.UserId != regional.Id))
            {
                db.RoomPriorityUsers.Add(new RoomPriorityUser { RoomId = room.Id, UserId = regional.Id });
            }
        }
        await db.SaveChangesAsync();

        var desks = await db.DeskReservations
            .Where(r => !emails.Contains(r.User.Email) && r.Date >= today && r.Status == DeskReservationStatus.Booked)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, DeskReservationStatus.Cancelled)
                .SetProperty(r => r.ActiveDeskKey, (string?)null)
                .SetProperty(r => r.ActiveUserDayKey, (string?)null)
                .SetProperty(r => r.CancelledAt, now)
                .SetProperty(r => r.CancelReason, CancelReason)
                .SetProperty(r => r.UpdatedAt, now));
        var rooms = await db.RoomReservations
            .Where(r => !emails.Contains(r.User.Email) && r.Date >= today && r.Status == RoomReservationStatus.Booked)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, RoomReservationStatus.Cancelled)
                .SetProperty(r => r.CancelledAt, now)
                .SetProperty(r => r.CancelReason, CancelReason)
                .SetProperty(r => r.UpdatedAt, now));
        var deactivated = await db.Users
            .Where(u => !emails.Contains(u.Email) && (u.Active || u.IsDemoAccount))
            .ExecuteUpdateAsync(u => u
                .SetProperty(x => x.Active, false)
                .SetProperty(x => x.IsDemoAccount, false)
                .SetProperty(x => x.UpdatedAt, now));

        if (deactivated + desks + rooms > 0)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ActorName = "Sistem (demo kadrosu)",
                ActorRole = "SYSTEM",
                Action = AuditActions.UserUpdate,
                EntityType = "User",
                Summary = $"Demo kadrosu {Cast.Length} hesaba indirildi: {deactivated} kullanıcı pasife alındı, {desks} masa ve {rooms} oda rezervasyonu iptal edildi.",
                Metadata = JsonSerializer.Serialize(new { deactivated, desks, rooms }),
                CreatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        await transaction.CommitAsync();
        log.WriteLine($"Kalan hesap              : {Cast.Length}");
        log.WriteLine($"Pasife alinan kullanici  : {deactivated}");
        log.WriteLine($"Iptal edilen masa rez.   : {desks}");
        log.WriteLine($"Iptal edilen oda rez.    : {rooms}");
        return deactivated;
    }
}
