using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Seed;

/// <summary>
/// Ek Bina Faz 1 oda kurallarini yukler. Admin'in duzenledigi odaya dokunmaz: yalnizca hic kural tanimlanmamis odalar yazilir,
/// bu yuzden tekrar calistirmak guvenlidir.
/// </summary>
public static class EkBinaRoomSeeder
{
    public static readonly string[] SecretaryOnlyRooms = ["PT36-A09", "PT36-B10"];
    public static readonly string[] EntranceRooms = ["PT36-A02", "PT36-B02"];
    public static readonly string[] TeamRooms = ["PT36-A06", "PT36-A07", "PT36-A08", "PT36-B06", "PT36-B09"];

    private static readonly (string Grade, int Min)[] EntranceRules =
        [(Grades.Manager, 5), (Grades.SeniorManager, 5), (Grades.Director, 5), (Grades.DeputyGm, 1), (Grades.Gm, 1)];

    private static readonly (string Grade, int Min)[] TeamRules =
        [(Grades.SeniorManager, 3), (Grades.Director, 3), (Grades.DeputyGm, 1), (Grades.Gm, 1)];

    public static async Task<int> ApplyAsync(Ofis360DbContext db)
    {
        var codes = SecretaryOnlyRooms.Concat(EntranceRooms).Concat(TeamRooms).ToArray();
        var rooms = await db.MeetingRooms.Include(r => r.AccessRules).Where(r => codes.Contains(r.Code)).ToListAsync();
        var updated = 0;

        foreach (var room in rooms)
        {
            if (room.SecretaryOnly || room.PriorityOpenDays is not null || room.AccessRules.Count > 0) continue;

            if (SecretaryOnlyRooms.Contains(room.Code))
            {
                room.SecretaryOnly = true;
            }
            else
            {
                var isEntrance = EntranceRooms.Contains(room.Code);
                foreach (var (grade, min) in isEntrance ? EntranceRules : TeamRules)
                {
                    db.RoomAccessRules.Add(new RoomAccessRule { RoomId = room.Id, Grade = grade, MinAttendees = min, BypassPriority = grade is Grades.DeputyGm or Grades.Gm });
                }
                if (isEntrance) room.PriorityOpenDays = 1;
            }
            updated++;
        }

        await db.SaveChangesAsync();
        return updated;
    }

    /// <summary>Yalnizca GELISTIRME verisi: Sekreter ve iki bolge muduru icin giris yapilabilir ornek hesaplar.</summary>
    public static async Task AssignDemoRolesAsync(Ofis360DbContext db, TextWriter log)
    {
        var department = await db.Departments.Where(d => d.Active).OrderBy(d => d.Name).FirstAsync();

        async Task<AppUser> Ensure(string email, string name, string title, string? grade, bool secretary)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is not null) return user;
            user = new AppUser { Email = email, FullName = name, Title = title, DepartmentId = department.Id, Grade = grade, IsSecretary = secretary, IsDemoAccount = true };
            db.Users.Add(user);
            log.WriteLine($"Ornek hesap eklendi: {name}");
            return user;
        }

        await Ensure("sekreter.demo@ofis360.local", "Selin Sekreter", "Yönetici Asistanı", Grades.Specialist, secretary: true);
        var first = await Ensure("bolge1.demo@ofis360.local", "Bora Bölge Bir", "Anadolu Bölge 1 Müdürü", Grades.Manager, secretary: false);
        var second = await Ensure("bolge2.demo@ofis360.local", "Burcu Bölge İki", "Anadolu Bölge 2 Müdürü", Grades.Manager, secretary: false);
        await db.SaveChangesAsync();

        async Task Priority(string code, AppUser user)
        {
            var room = await db.MeetingRooms.Include(r => r.PriorityUsers).SingleAsync(r => r.Code == code);
            if (room.PriorityUsers.Count > 0) return;
            db.RoomPriorityUsers.Add(new RoomPriorityUser { RoomId = room.Id, UserId = user.Id });
            log.WriteLine($"{room.Name} ({code}) onceligi: {user.FullName}");
        }

        await Priority("PT36-A02", first);
        await Priority("PT36-B02", second);
        await db.SaveChangesAsync();
    }
}
