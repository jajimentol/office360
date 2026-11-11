using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Common;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Seed;

/// <summary>
/// Yalnizca GELISTIRME verisi: gercek personel listesi gelene kadar kademe kurallari denenebilsin diye ornek kullanicilara
/// kademe atar. Kademesi dolu kayda dokunmaz, idempotenttir. Gercek listeyle calisan ortamda kullanilmaz.
/// </summary>
public static class DemoGradeSeeder
{
    /// <summary>Yonetici rolundeki ornek kullanicilarin ust kademelere dagilimi (e-posta sirasiyla).</summary>
    private static readonly (string Grade, int Count)[] ManagerSpread =
        [(Grades.Gm, 1), (Grades.DeputyGm, 2), (Grades.Director, 4), (Grades.SeniorManager, 5)];

    public static async Task<int> AssignAsync(Ofis360DbContext db, TextWriter log)
    {
        var users = await db.Users.Where(u => u.Grade == null).OrderBy(u => u.Email).ToListAsync();
        var spread = ManagerSpread.SelectMany(s => Enumerable.Repeat(s.Grade, s.Count)).ToList();
        var nextManager = 0;
        var employeeIndex = 0;

        foreach (var user in users)
        {
            if (user.Role == Roles.Employee)
            {
                user.Grade = user.Title.Contains("Kıdemli") ? Grades.SeniorSpecialist
                    : employeeIndex % 7 == 0 ? Grades.AssistantSpecialist
                    : Grades.Specialist;
                employeeIndex++;
            }
            else if (user.IsDemoAccount || user.Role == Roles.Admin || nextManager >= spread.Count)
            {
                user.Grade = Grades.Manager;
            }
            else
            {
                user.Grade = spread[nextManager++];
            }
        }

        await db.SaveChangesAsync();
        log.WriteLine($"Kademe atanan ornek kullanici: {users.Count}");
        foreach (var group in users.GroupBy(u => u.Grade!).OrderBy(g => Grades.Rank(g.Key)))
        {
            log.WriteLine($"  {Grades.Label(group.Key),-24} {group.Count()}");
        }
        return users.Count;
    }
}
