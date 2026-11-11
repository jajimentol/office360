using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;
using static Ofis360.Seed.SeedSupport;

namespace Ofis360.Seed;

/// <summary>
/// Kurumsal olcek kapasitesi (kaynak: prisma/seed-scale.ts). Hicbir kaydi silmez; eksik departman, kullanici,
/// masa ve odalari ekler. Yeni masa/odalar havuzda (placed=false) olusur.
/// </summary>
public sealed class ScaleSeeder(Ofis360DbContext db, TextWriter log)
{
    private const int TargetHeadcount = 300;
    private const int TargetPassive = 15;
    private const int TargetManagers = 22;
    private const int TargetAdmins = 4;
    private const int TargetAccessibility = 10;
    private readonly SeededRandom _random = new(20260822);

    public async Task RunAsync()
    {
        log.WriteLine("Ofis360 - kurumsal olcek kapasitesi kuruluyor (300 calisan).");
        log.WriteLine("Bu betik hicbir kaydi silmez.\n");

        // 1) Departmanlar
        var existingDepartments = await db.Departments.ToListAsync();
        var departmentIdByCode = new Dictionary<string, string>();
        foreach (var existing in existingDepartments)
        {
            var target = ScaleData.DepartmentMigration.TryGetValue(existing.Code, out var targetCode)
                ? ScaleData.TargetDepartments.FirstOrDefault(t => t.Code == targetCode) : null;
            if (target is null)
            {
                departmentIdByCode[existing.Code] = existing.Id;
                continue;
            }
            existing.Code = target.Code;
            existing.Name = target.Name;
            existing.Color = target.Color;
            departmentIdByCode[target.Code] = existing.Id;
        }
        await db.SaveChangesAsync();

        var createdDepartments = 0;
        foreach (var target in ScaleData.TargetDepartments)
        {
            if (departmentIdByCode.ContainsKey(target.Code)) continue;
            var created = new Department { Code = target.Code, Name = target.Name, Color = target.Color };
            db.Departments.Add(created);
            departmentIdByCode[target.Code] = created.Id;
            createdDepartments++;
        }
        await db.SaveChangesAsync();
        log.WriteLine($"- Departman: {existingDepartments.Count} mevcut kayit hedef yapiya eslendi, {createdDepartments} yeni departman eklendi (toplam {departmentIdByCode.Count}).");

        // 2) Kullanicilar
        var currentUsers = await db.Users.AsNoTracking().Select(u => new { u.Id, u.Email, u.FullName, u.DepartmentId, u.Role }).ToListAsync();
        var usedEmails = currentUsers.Select(u => u.Email).ToHashSet();
        var usedNames = currentUsers.Select(u => u.FullName).ToHashSet();
        var headcountByDepartment = currentUsers.GroupBy(u => u.DepartmentId).ToDictionary(g => g.Key, g => g.Count());

        var missing = TargetHeadcount - currentUsers.Count;
        if (missing <= 0) log.WriteLine($"- Kullanici: sistemde zaten {currentUsers.Count} kullanici var, ekleme yapilmadi.");

        var additions = new List<(string Code, string DepartmentId, int Count)>();
        foreach (var target in ScaleData.TargetDepartments)
        {
            var departmentId = departmentIdByCode[target.Code];
            var need = Math.Max(0, target.Headcount - headcountByDepartment.GetValueOrDefault(departmentId));
            if (need > 0) additions.Add((target.Code, departmentId, need));
        }

        var managersToAdd = Math.Max(0, TargetManagers - currentUsers.Count(u => u.Role == "MANAGER"));
        var adminsToAdd = Math.Max(0, TargetAdmins - currentUsers.Count(u => u.Role == "ADMIN"));
        var passiveToAdd = TargetPassive;
        var accessibilityToAdd = Math.Max(0, TargetAccessibility - await db.Users.CountAsync(u => u.AccessibilityApproved));

        string NextName()
        {
            for (var attempt = 0; attempt < 5000; attempt++)
            {
                var name = $"{_random.Pick(ScaleData.FirstNames)} {_random.Pick(ScaleData.LastNames)}";
                if (usedNames.Add(name)) return name;
            }
            throw new InvalidOperationException("Yeterli benzersiz ad uretilemedi.");
        }

        string NextEmail(string fullName)
        {
            var baseSlug = ScaleEmailSlug(fullName);
            var email = $"{baseSlug}@ofis360.local";
            var suffix = 2;
            while (!usedEmails.Add(email))
            {
                email = $"{baseSlug}{suffix}@ofis360.local";
                suffix++;
            }
            return email;
        }

        var newUserRows = new List<AppUser>();
        foreach (var addition in additions)
        {
            for (var index = 0; index < addition.Count; index++)
            {
                var fullName = NextName();
                var role = "EMPLOYEE";
                var title = _random.Pick(ScaleData.Titles.GetValueOrDefault(addition.Code) ?? ScaleData.Titles["OPR"]);
                if (index == 0 && managersToAdd > 0)
                {
                    role = "MANAGER";
                    title = ScaleData.ManagerTitles.GetValueOrDefault(addition.Code, "Direktör");
                    managersToAdd--;
                }
                else if (addition.Code == "UYD" && adminsToAdd > 0 && index <= 3)
                {
                    role = "ADMIN";
                    title = "İş Yeri Deneyimi Yöneticisi";
                    adminsToAdd--;
                }
                else if (managersToAdd > 0 && index % 12 == 5)
                {
                    role = "MANAGER";
                    title = $"{(addition.Code == "BT" ? "Yazılım" : "Ekip")} Yöneticisi";
                    managersToAdd--;
                }

                var passive = role == "EMPLOYEE" && passiveToAdd > 0 && index % 17 == 9;
                if (passive) passiveToAdd--;
                var accessibility = accessibilityToAdd > 0 && index % 23 == 7;
                if (accessibility) accessibilityToAdd--;

                newUserRows.Add(new AppUser
                {
                    Email = NextEmail(fullName), FullName = fullName, Title = title, Role = role, DepartmentId = addition.DepartmentId,
                    AccessibilityApproved = accessibility, Active = !passive, PrefersQuietZone = _random.Next() < 0.28,
                });
            }
        }

        foreach (var row in newUserRows)
        {
            if (managersToAdd <= 0) break;
            if (row.Role == "EMPLOYEE" && row.Active) { row.Role = "MANAGER"; row.Title = "Ekip Yöneticisi"; managersToAdd--; }
        }
        foreach (var row in newUserRows)
        {
            if (passiveToAdd <= 0) break;
            if (row.Role == "EMPLOYEE" && row.Active) { row.Active = false; passiveToAdd--; }
        }
        foreach (var row in newUserRows)
        {
            if (accessibilityToAdd <= 0) break;
            if (!row.AccessibilityApproved && row.Active) { row.AccessibilityApproved = true; accessibilityToAdd--; }
        }

        if (newUserRows.Count > 0) await BulkInsertAsync(db, newUserRows);
        var totalUsers = await db.Users.CountAsync();
        log.WriteLine($"- Kullanici: {newUserRows.Count} yeni kayit eklendi, {currentUsers.Count} mevcut kayit korundu (toplam {totalUsers}).");

        // 3) Masalar - havuzda
        var floors = await db.Floors.Include(f => f.Zones).ToListAsync();
        var featureIdByKey = await db.DeskFeatures.ToDictionaryAsync(f => f.Key, f => f.Id);
        var existingDeskCodes = (await db.Desks.Select(d => d.Code).ToListAsync()).ToHashSet();
        var createdDesks = 0;
        var skippedDesks = 0;

        foreach (var spec in ScaleData.NewDesks)
        {
            var floor = floors.FirstOrDefault(f => f.Number == spec.FloorNumber);
            if (floor is null) continue;
            var zone = floor.Zones.FirstOrDefault(z => z.Code == spec.ZoneCode);
            if (zone is null)
            {
                log.WriteLine($"  ! {spec.FloorNumber}. kat {spec.ZoneCode} bölgesi bulunamadı, atlandı.");
                continue;
            }

            var accessibleIndexes = new HashSet<int>();
            for (var i = 0; i < spec.AccessibleCount; i++) accessibleIndexes.Add(Math.Min(spec.Count - 1, i * 4 + 2));
            var inactiveIndexes = new HashSet<int>();
            for (var i = 0; i < spec.InactiveCount; i++) inactiveIndexes.Add(Math.Max(0, spec.Count - 1 - i * 3));

            for (var index = 0; index < spec.Count; index++)
            {
                var code = $"{spec.Prefix}{spec.StartIndex + index:00}";
                if (!existingDeskCodes.Add(code)) { skippedDesks++; continue; }

                var accessibleOnly = accessibleIndexes.Contains(index);
                var popular = !accessibleOnly && (double)index / spec.Count < spec.PopularRatio;
                var active = !inactiveIndexes.Contains(index);
                var featureKeys = spec.FeatureSets[index % spec.FeatureSets.Length].ToList();
                if (accessibleOnly && !featureKeys.Contains("ACCESSIBLE")) featureKeys.Add("ACCESSIBLE");
                if (zone.Quiet && !featureKeys.Contains("QUIET")) featureKeys.Add("QUIET");

                var desk = new Desk
                {
                    Code = code, FloorId = floor.Id, ZoneId = zone.Id, X = 0, Y = 0, Placed = false, Active = active, AccessibleOnly = accessibleOnly, Popular = popular,
                    Notes = accessibleOnly ? "Erişilebilirlik gerekçesiyle ayrılmıştır."
                        : !active ? "Donanım yenileme çalışması nedeniyle geçici olarak kullanıma kapalıdır."
                        : popular ? "Pencere kenarı, yoğun tercih edilen masa." : null,
                };
                foreach (var key in featureKeys.Where(featureIdByKey.ContainsKey)) desk.Features.Add(new DeskFeatureOnDesk { DeskId = desk.Id, FeatureId = featureIdByKey[key] });
                db.Desks.Add(desk);
                createdDesks++;
            }
        }
        await db.SaveChangesAsync();
        log.WriteLine($"- Masa: {createdDesks} yeni masa havuzda oluşturuldu{(skippedDesks > 0 ? $" ({skippedDesks} kod zaten mevcut, atlandı)" : "")}.");

        // 4) Toplanti odalari - havuzda
        var roomFeatureIdByKey = await db.RoomFeatures.ToDictionaryAsync(f => f.Key, f => f.Id);
        var existingRoomCodes = (await db.MeetingRooms.Select(r => r.Code).ToListAsync()).ToHashSet();
        var createdRooms = 0;
        foreach (var spec in ScaleData.NewRooms)
        {
            if (existingRoomCodes.Contains(spec.Code)) continue;
            var floor = floors.FirstOrDefault(f => f.Number == spec.FloorNumber);
            if (floor is null) continue;
            var room = new MeetingRoom
            {
                Code = spec.Code, Name = spec.Name, FloorId = floor.Id, Capacity = spec.Capacity, X = 0, Y = 0, Width = spec.Width, Height = spec.Height,
                Placed = false, Notes = $"{spec.Capacity} kişilik toplantı odası.",
            };
            foreach (var key in spec.Features.Where(roomFeatureIdByKey.ContainsKey)) room.Features.Add(new RoomFeatureOnRoom { RoomId = room.Id, FeatureId = roomFeatureIdByKey[key] });
            db.MeetingRooms.Add(room);
            createdRooms++;
        }
        await db.SaveChangesAsync();
        log.WriteLine($"- Toplantı odası: {createdRooms} yeni oda havuzda oluşturuldu.");

        // 5) Isaret kaydi
        var marker = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == ScaleData.ScaleMarkerKey);
        var stamp = DateTimeOffset.UtcNow.ToString("O");
        if (marker is null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = ScaleData.ScaleMarkerKey, Value = stamp, Label = "Kurumsal ölçek kurulumu", Description = "300 çalışanlık demo kapasitesinin kurulduğu tarih.", Type = "TEXT", Group = "GENERAL", Order = 90 });
        }
        else
        {
            marker.Value = stamp;
        }
        await db.SaveChangesAsync();

        var deskTotal = await db.Desks.CountAsync();
        var deskPlaced = await db.Desks.CountAsync(d => d.Placed);
        var roomTotal = await db.MeetingRooms.CountAsync();
        var roomPlaced = await db.MeetingRooms.CountAsync(r => r.Placed);
        var userTotal = await db.Users.CountAsync();
        var activeUsers = await db.Users.CountAsync(u => u.Active);
        log.WriteLine("\nSONUC");
        log.WriteLine($"  Kullanici       : {userTotal} ({activeUsers} aktif, {userTotal - activeUsers} pasif)");
        log.WriteLine($"  Masa            : {deskTotal} ({deskPlaced} yerlesik, {deskTotal - deskPlaced} havuzda)");
        log.WriteLine($"  Toplanti odasi  : {roomTotal} ({roomPlaced} yerlesik, {roomTotal - roomPlaced} havuzda)");
        log.WriteLine("\nSonraki adim: admin ekraninda \"Havuzdaki nesneleri otomatik yerlestir\" ile krokiye yerlestirin, ardindan `activity` ile rezervasyon verisini uretin.");
    }
}
