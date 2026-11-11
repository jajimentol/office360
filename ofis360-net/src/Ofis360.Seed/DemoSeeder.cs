using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Services;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Plan;
using Ofis360.Domain.Rules;
using Ofis360.Infrastructure.Persistence;
using static Ofis360.Seed.SeedSupport;

namespace Ofis360.Seed;

/// <summary>
/// Demo veritabani doldurma (kaynak: prisma/seed.ts). DESTRUCTIVE: once tum tablolar temizlenir.
/// Uretilen veri deterministiktir (tohum 20260821) ve adil kullanim kurallariyla tutarlidir.
/// </summary>
public sealed class DemoSeeder(Ofis360DbContext db, string today, TextWriter log)
{
    private const int HistoryDays = 92;
    private const int FutureDays = 14;
    private readonly SeededRandom _random = new(20260821);

    private sealed record SketchInput(string Type, int X, int Y, int Width, int Height, string? Label, bool LabelGiven = true);

    private sealed class SeedDesk
    {
        public required string Id;
        public required string Code;
        public required int FloorNumber;
        public required bool FloorReservable;
        public required bool ZoneReservable;
        public required bool Popular;
        public required bool AccessibleOnly;
        public required bool Quiet;
        public required double Weight;
    }

    private sealed record SeedRoom(string Id, string Code, int Capacity, int FloorNumber, bool FloorReservable);

    private sealed class SeedUser
    {
        public required string Id;
        public required string FullName;
        public required string Role;
        public required double Attendance;
        public required bool AccessibilityApproved;
        public required string DepartmentId;
        public required bool IsDemoAccount;
    }

    /// <summary>Kat spesifikasyonundan fiziksel kroki uretir; adminin editorde cizecegi yapinin aynisi.</summary>
    private static List<SketchInput> BuildSketch(FloorSpec floor)
    {
        var w = floor.PlanWidth;
        var h = floor.PlanHeight;
        const int wall = 12;
        var elements = new List<SketchInput>
        {
            new("WALL", 0, 0, w, wall, null),
            new("WALL", 0, h - wall, w, wall, null),
            new("WALL", 0, 0, wall, h, null),
            new("WALL", w - wall, 0, wall, h, null),
            new("TEXT", 24, 22, 220, 26, $"{floor.Name} · {floor.Number}. kat krokisi"),
        };

        foreach (var zone in floor.Zones)
        {
            switch (zone.Kind)
            {
                case "WORK":
                    elements.Add(new("ROOM", zone.X, zone.Y, zone.Width, zone.Height, zone.Name));
                    if (zone.WindowSide)
                    {
                        elements.Add(new("WINDOW", zone.X + 10, Math.Max(2, zone.Y - 10), Math.Max(60, zone.Width - 20), 10, null));
                    }
                    break;
                case "CORRIDOR": elements.Add(new("CORRIDOR", zone.X, zone.Y, zone.Width, zone.Height, zone.Name)); break;
                case "SOCIAL": elements.Add(new("KITCHEN", zone.X, zone.Y, zone.Width, zone.Height, zone.Name)); break;
                case "RESTRICTED": elements.Add(new("RESTRICTED", zone.X, zone.Y, zone.Width, zone.Height, zone.Name)); break;
                case "CORE":
                    var half = (int)Math.Floor(zone.Height / 2.0) - 6;
                    elements.Add(new("ELEVATOR", zone.X, zone.Y, zone.Width, half, "Asansör"));
                    elements.Add(new("STAIRS", zone.X, zone.Y + half + 12, zone.Width, half, "Merdiven"));
                    break;
                case "MEETING": elements.Add(new("ROOM", zone.X, zone.Y, zone.Width, zone.Height, zone.Name)); break;
            }
        }

        foreach (var room in floor.Rooms)
        {
            elements.Add(new("DOOR", room.X + 12, room.Y + room.Height - 6, 44, 12, null));
        }

        elements.Add(new("DOOR", JsRound(w / 2.0) - 22, h - wall - 4, 44, 14, null));
        elements.Add(new("WC", w - 140, h - 140, 110, 100, "WC"));
        return elements;
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private async Task ClearDatabaseAsync()
    {
        await db.CapacityShortfalls.ExecuteDeleteAsync(); // kaynakta yoktu; tutarlilik icin (FK yok)
        await db.AuditLogs.ExecuteDeleteAsync();
        await db.Notifications.ExecuteDeleteAsync();
        await db.DeskReservations.ExecuteDeleteAsync();
        await db.RoomReservations.ExecuteDeleteAsync();
        await db.Closures.ExecuteDeleteAsync();
        await db.DeskFeatureLinks.ExecuteDeleteAsync();
        await db.RoomFeatureLinks.ExecuteDeleteAsync();
        await db.Desks.ExecuteDeleteAsync();
        await db.MeetingRooms.ExecuteDeleteAsync();
        await db.FloorPlanElements.ExecuteDeleteAsync();
        await db.Zones.ExecuteDeleteAsync();
        await db.Floors.ExecuteDeleteAsync();
        await db.Buildings.ExecuteDeleteAsync();
        await db.DeskFeatures.ExecuteDeleteAsync();
        await db.RoomFeatures.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Departments.ExecuteDeleteAsync();
        await db.ReservationRuleOverrides.ExecuteDeleteAsync();
        await db.Holidays.ExecuteDeleteAsync();
        await db.ReservationRules.ExecuteDeleteAsync();
        await db.SystemSettings.ExecuteDeleteAsync();
    }

    public async Task RunAsync()
    {
        log.WriteLine($"Ofis360 seed baslatiliyor. Referans tarih: {today}");
        await ClearDatabaseAsync();

        // --- Departmanlar ve kullanicilar ---
        var departmentIdByCode = new Dictionary<string, string>();
        foreach (var d in SeedData.Departments)
        {
            var entity = new Department { Code = d.Code, Name = d.Name, Color = d.Color };
            db.Departments.Add(entity);
            departmentIdByCode[d.Code] = entity.Id;
        }

        var users = new List<SeedUser>();
        var usedEmails = new HashSet<string>();
        foreach (var e in SeedData.Employees)
        {
            var email = $"{EmailSlug(e.FullName)}@ofis360.local";
            var suffix = 2;
            while (!usedEmails.Add(email))
            {
                email = $"{EmailSlug(e.FullName)}{suffix}@ofis360.local";
                suffix++;
            }
            var departmentId = departmentIdByCode[e.DepartmentCode];
            var user = new AppUser
            {
                Email = email, FullName = e.FullName, Title = e.Title, Role = e.Role, DepartmentId = departmentId,
                AccessibilityApproved = e.AccessibilityApproved, IsDemoAccount = e.IsDemoAccount, PrefersQuietZone = e.Attendance < 0.45,
            };
            db.Users.Add(user);
            users.Add(new SeedUser
            {
                Id = user.Id, FullName = user.FullName, Role = user.Role, Attendance = e.Attendance,
                AccessibilityApproved = user.AccessibilityApproved, DepartmentId = departmentId, IsDemoAccount = user.IsDemoAccount,
            });
        }
        await db.SaveChangesAsync();

        var demoEmployee = users.Single(u => u.FullName == "Elif Yılmaz");
        var demoManager = users.Single(u => u.FullName == "Mert Demir");
        var demoAdmin = users.Single(u => u.FullName == "Zeynep Kaya");
        var demoAccessible = users.Single(u => u.FullName == "Burak Şahin");
        log.WriteLine($"- {users.Count} kullanici, {SeedData.Departments.Length} departman olusturuldu.");

        // --- Ozellikler ---
        var deskFeatureIdByKey = new Dictionary<string, string>();
        foreach (var f in SeedData.DeskFeatures)
        {
            var entity = new DeskFeature { Key = f.Key, Name = f.Name, Description = f.Description, Icon = f.Icon };
            db.DeskFeatures.Add(entity);
            deskFeatureIdByKey[f.Key] = entity.Id;
        }
        var roomFeatureIdByKey = new Dictionary<string, string>();
        foreach (var f in SeedData.RoomFeatures)
        {
            var entity = new RoomFeature { Key = f.Key, Name = f.Name, Icon = f.Icon };
            db.RoomFeatures.Add(entity);
            roomFeatureIdByKey[f.Key] = entity.Id;
        }
        await db.SaveChangesAsync();

        // --- Katlar, bolgeler, masalar, toplanti odalari ---
        var desks = new List<SeedDesk>();
        var rooms = new List<SeedRoom>();

        // Demo kurulumunda tek bina vardir; kurumsal kurulumda gercek bina `palladium` ile eklenir.
        var demoBuilding = new Building { Code = "DEMO", Name = "Demo Bina", Address = "Kurgusal gösterim binası", Order = 90, Active = true, IsDemo = true };
        db.Buildings.Add(demoBuilding);
        await db.SaveChangesAsync();

        foreach (var floorSpec in SeedData.Floors)
        {
            var floor = new Floor
            {
                BuildingId = demoBuilding.Id, Number = floorSpec.Number, Name = floorSpec.Name, Description = floorSpec.Description,
                Reservable = floorSpec.Reservable, PlanWidth = floorSpec.PlanWidth, PlanHeight = floorSpec.PlanHeight,
            };
            db.Floors.Add(floor);

            foreach (var zoneSpec in floorSpec.Zones)
            {
                var zone = new Zone
                {
                    FloorId = floor.Id, Code = zoneSpec.Code, Name = zoneSpec.Name, Kind = zoneSpec.Kind, X = zoneSpec.X, Y = zoneSpec.Y,
                    Width = zoneSpec.Width, Height = zoneSpec.Height, WindowSide = zoneSpec.WindowSide, Quiet = zoneSpec.Quiet, Reservable = zoneSpec.Reservable,
                };
                db.Zones.Add(zone);

                if (zoneSpec.Desks is null) continue;
                var grid = zoneSpec.Desks;
                var cellWidth = (double)zoneSpec.Width / grid.Cols;
                var cellHeight = (double)zoneSpec.Height / grid.Rows;
                var index = 0;
                for (var row = 0; row < grid.Rows; row++)
                {
                    for (var col = 0; col < grid.Cols; col++)
                    {
                        var code = $"{grid.Prefix}{index + 1:00}";
                        var popular = (grid.PopularRows ?? []).Contains(row);
                        var accessibleOnly = (grid.AccessibleIndexes ?? []).Contains(index);
                        var featureKeys = grid.FeatureSets[index % grid.FeatureSets.Length].ToList();
                        if (accessibleOnly && !featureKeys.Contains("ACCESSIBLE")) featureKeys.Add("ACCESSIBLE");
                        if (zoneSpec.Quiet && !featureKeys.Contains("QUIET")) featureKeys.Add("QUIET");

                        var desk = new Desk
                        {
                            Code = code, FloorId = floor.Id, ZoneId = zone.Id,
                            X = JsRound(zoneSpec.X + cellWidth * (col + 0.5)), Y = JsRound(zoneSpec.Y + cellHeight * (row + 0.5)),
                            Active = true, AccessibleOnly = accessibleOnly, Popular = popular,
                            Notes = accessibleOnly ? "Erişilebilirlik gerekçesiyle ayrılmıştır." : popular ? "Pencere kenarı, yoğun tercih edilen masa." : null,
                        };
                        foreach (var key in featureKeys.Where(deskFeatureIdByKey.ContainsKey))
                        {
                            desk.Features.Add(new DeskFeatureOnDesk { DeskId = desk.Id, FeatureId = deskFeatureIdByKey[key] });
                        }
                        db.Desks.Add(desk);
                        desks.Add(new SeedDesk
                        {
                            Id = desk.Id, Code = desk.Code, FloorNumber = floorSpec.Number, FloorReservable = floorSpec.Reservable,
                            ZoneReservable = zoneSpec.Reservable, Popular = popular, AccessibleOnly = accessibleOnly, Quiet = zoneSpec.Quiet,
                            Weight = popular ? 2.6 : zoneSpec.Quiet ? 1.25 : 1.6,
                        });
                        index++;
                    }
                }
            }

            // --- Kat krokisi ---
            var sketch = BuildSketch(floorSpec);
            for (var i = 0; i < sketch.Count; i++)
            {
                var element = sketch[i];
                var definition = PlanElementDefinitions.All[element.Type];
                db.FloorPlanElements.Add(new FloorPlanElement
                {
                    FloorId = floor.Id, Type = element.Type, Label = element.Label,
                    X = Math.Max(0, Math.Min(element.X, floorSpec.PlanWidth - 8)),
                    Y = Math.Max(0, Math.Min(element.Y, floorSpec.PlanHeight - 8)),
                    Width = Math.Max(8, Math.Min(element.Width, floorSpec.PlanWidth - element.X)),
                    Height = Math.Max(8, Math.Min(element.Height, floorSpec.PlanHeight - element.Y)),
                    Rotation = 0,
                    Style = JsonSerializer.Serialize(new { fill = definition.DefaultStyle.Fill, stroke = definition.DefaultStyle.Stroke, strokeWidth = definition.DefaultStyle.StrokeWidth, fontSize = definition.DefaultStyle.FontSize }, Json),
                    LayerOrder = definition.BaseLayer + i, Locked = false, Visible = true,
                });
            }

            foreach (var roomSpec in floorSpec.Rooms)
            {
                var room = new MeetingRoom
                {
                    Code = roomSpec.Code, Name = roomSpec.Name, FloorId = floor.Id, Capacity = roomSpec.Capacity,
                    X = roomSpec.X, Y = roomSpec.Y, Width = roomSpec.Width, Height = roomSpec.Height, Notes = roomSpec.Notes,
                };
                foreach (var key in roomSpec.Features.Where(roomFeatureIdByKey.ContainsKey))
                {
                    room.Features.Add(new RoomFeatureOnRoom { RoomId = room.Id, FeatureId = roomFeatureIdByKey[key] });
                }
                db.MeetingRooms.Add(room);
                rooms.Add(new SeedRoom(room.Id, room.Code, room.Capacity, floorSpec.Number, floorSpec.Reservable));
            }
            await db.SaveChangesAsync();
        }

        // Az kullanilan masalari belirginlestirmek icin birkac masanin agirligini dusur.
        foreach (var code in new[] { "F1-B04", "F3-J04", "F2-E06", "F3-I06" })
        {
            var desk = desks.FirstOrDefault(d => d.Code == code);
            if (desk is not null) desk.Weight = 1;
        }

        var sketchCount = await db.FloorPlanElements.CountAsync();
        log.WriteLine($"- {SeedData.Floors.Length} kat, {desks.Count} masa, {rooms.Count} toplantı odası, {sketchCount} kroki ögesi olusturuldu.");

        // --- Kurallar ve sistem ayarlari ---
        foreach (var rule in RuleDefinitions.All) db.ReservationRules.Add(RuleService.ToEntity(rule));

        var settings = new[]
        {
            new SystemSetting { Key = SettingKeys.AppName, Value = AppConfig.Name, Label = "Uygulama adı", Description = "Arayüzde ve bildirimlerde görünen marka adı.", Type = "TEXT", Group = "GENERAL", Order = 1 },
            new SystemSetting { Key = SettingKeys.WorkdayStart, Value = DayKeys.MinutesToTime(DefaultWorkingHours.StartMinutes), Label = "Mesai başlangıcı", Description = "Toplantı odası rezervasyonlarının başlayabileceği en erken saat.", Type = "TIME", Group = "OFFICE", Order = 2 },
            new SystemSetting { Key = SettingKeys.WorkdayEnd, Value = DayKeys.MinutesToTime(DefaultWorkingHours.EndMinutes), Label = "Mesai bitişi", Description = "Toplantı odası rezervasyonlarının bitmesi gereken en geç saat.", Type = "TIME", Group = "OFFICE", Order = 3 },
            new SystemSetting { Key = SettingKeys.OfficeDays, Value = "Pazartesi, Salı, Çarşamba, Perşembe, Cuma", Label = "Ofis günleri", Description = "Ofisin tam kapasite hizmet verdiği günler.", Type = "TEXT", Group = "OFFICE", Order = 4 },
            new SystemSetting { Key = SettingKeys.LateCancelMinutes, Value = "120", Label = "Geç iptal süresi (dakika)", Description = "Toplantıya bundan az kala yapılan iptaller raporda geç iptal sayılır.", Type = "NUMBER", Group = "OFFICE", Order = 8 },
            new SystemSetting { Key = SettingKeys.ReportPersonDetail, Value = "false", Label = "Katılım raporunda kişi adları", Description = "İK ve KVKK onayı alınmadan açılmamalıdır.", Type = "BOOLEAN", Group = "OFFICE", Order = 9 },
            new SystemSetting { Key = SettingKeys.PresenceManagersOnly, Value = "false", Label = "“Ofiste kim var” yalnızca yöneticilere açık", Description = "Kapalıyken tüm çalışanlar görür; açıkken yalnızca yönetici ve admin görür.", Type = "BOOLEAN", Group = "OFFICE", Order = 10 },
            new SystemSetting { Key = SettingKeys.Announcement, Value = "Eylül ayı itibarıyla ortak ofis günü Çarşamba olarak belirlenmiştir.", Label = "Ana sayfa duyurusu", Description = "Çalışan ana sayfasında gösterilen sistem duyurusu.", Type = "TEXT", Group = "GENERAL", Order = 6 },
            new SystemSetting { Key = SettingKeys.ContactEmail, Value = AppConfig.SupportEmail, Label = "Destek e-postası", Description = "Çalışanların ofis hizmetleri için ulaşabileceği e-posta adresi.", Type = "TEXT", Group = "GENERAL", Order = 7 },
        };
        db.SystemSettings.AddRange(settings);
        await db.SaveChangesAsync();

        // --- Kapatma kayitlari ---
        var closedDesk = desks.Single(d => d.Code == "F2-F05");
        var closedRoom = rooms.Single(r => r.Code == "T103");
        db.Closures.AddRange(
            new Closure { Scope = "DESK", DeskId = closedDesk.Id, StartDate = DayKeys.ToDateOnly(DayKeys.AddDays(today, -1)), EndDate = DayKeys.ToDateOnly(DayKeys.AddDays(today, 5)), Reason = "MAINTENANCE", Note = "Monitör ve dock istasyonu değişimi yapılacaktır.", CreatedById = demoAdmin.Id },
            new Closure { Scope = "ROOM", RoomId = closedRoom.Id, StartDate = DayKeys.ToDateOnly(DayKeys.AddDays(today, 3)), EndDate = DayKeys.ToDateOnly(DayKeys.AddDays(today, 4)), Reason = "EVENT", Note = "Kurum içi eğitim programı için ayrılmıştır.", CreatedById = demoAdmin.Id },
            new Closure { Scope = "DESK", DeskId = desks.Single(d => d.Code == "F1-B03").Id, StartDate = DayKeys.ToDateOnly(DayKeys.AddDays(today, -30)), EndDate = DayKeys.ToDateOnly(DayKeys.AddDays(today, -24)), Reason = "MAINTENANCE", Note = "Elektrik tesisatı yenilendi.", Active = false, CreatedById = demoAdmin.Id });
        await db.SaveChangesAsync();

        // --- Masa rezervasyonlari ---
        var allDays = DayKeys.Range(DayKeys.AddDays(today, -HistoryDays), DayKeys.AddDays(today, FutureDays)).Where(d => !DayKeys.IsWeekend(d)).ToList();
        var bookableDesks = desks.Where(d => d.FloorReservable && d.ZoneReservable && !d.AccessibleOnly).ToList();
        var accessibleDesks = desks.Where(d => d.FloorReservable && d.ZoneReservable && d.AccessibleOnly).ToList();

        // Elif Yilmaz icin adil kullanim senaryosunu gosterecek belirlenmis gecmis.
        var pastOfficeDays = allDays.Where(d => string.CompareOrdinal(d, today) < 0).Reverse().ToList();
        var currentWeekStart = DayKeys.StartOfWeek(today);
        var demoPlanDays = new List<string>();
        var weekUsage = new Dictionary<string, int>();
        foreach (var day in pastOfficeDays)
        {
            var weekKey = DayKeys.StartOfWeek(day);
            var cap = weekKey == currentWeekStart ? 1 : 2;
            var used = weekUsage.GetValueOrDefault(weekKey);
            if (used >= cap) continue;
            weekUsage[weekKey] = used + 1;
            demoPlanDays.Add(day);
            if (demoPlanDays.Count >= 12) break;
        }

        string[] demoDeskCodes = ["F2-D01", "F2-D01", "F1-B01", "F1-A02", "F3-I01", "F1-A03", "F2-E02", "F3-J02", "F2-E04", "F1-B02", "F3-I02", "F2-E03"];
        var fixedAssignments = new Dictionary<string, List<(string UserId, string DeskId)>>();
        for (var i = 0; i < demoPlanDays.Count; i++)
        {
            var desk = desks.FirstOrDefault(d => d.Code == demoDeskCodes[i % demoDeskCodes.Length]);
            if (desk is null) continue;
            if (!fixedAssignments.TryGetValue(demoPlanDays[i], out var list)) fixedAssignments[demoPlanDays[i]] = list = [];
            list.Add((demoEmployee.Id, desk.Id));
        }

        var weekdayWeight = new Dictionary<int, double> { [1] = 0.6, [2] = 0.82, [3] = 0.9, [4] = 0.75, [5] = 0.45 };
        var userStreak = new Dictionary<string, (string DeskId, int Streak)>();
        var deskReservationRows = new List<DeskReservation>();
        string[] cancelReasons = ["Planlarım değişti, uzaktan çalışacağım.", "Toplantılarım iptal oldu.", "Sağlık nedeniyle ofise gelemiyorum.", "Müşteri ziyaretine gideceğim."];

        (string Status, bool Cancelled) StatusForDay(string dayKey)
        {
            var value = _random.Next();
            if (string.CompareOrdinal(dayKey, today) < 0)
            {
                if (value < 0.07) return ("CANCELLED", true);
                if (value < 0.18) return ("NO_SHOW", false);
                return ("COMPLETED", false);
            }
            if (dayKey == today)
            {
                if (value < 0.05) return ("CANCELLED", true);
                if (value < 0.62) return ("CHECKED_IN", false);
                return ("BOOKED", false);
            }
            if (value < 0.06) return ("CANCELLED", true);
            return ("BOOKED", false);
        }

        void PushReservation(string dayKey, string userId, SeedDesk desk, string? forcedStatus = null, string? cancelledById = null, string? cancelReason = null, string? note = null)
        {
            var outcome = forcedStatus is not null ? (Status: forcedStatus, Cancelled: forcedStatus == "CANCELLED") : StatusForDay(dayKey);
            var createdAt = At(dayKey, -_random.RandomInt(1, 6) * 24 + _random.RandomInt(8, 17));
            deskReservationRows.Add(new DeskReservation
            {
                DeskId = desk.Id, UserId = userId, Date = DayKeys.ToDateOnly(dayKey), Status = outcome.Status,
                ActiveDeskKey = outcome.Cancelled ? null : $"{desk.Id}#{dayKey}",
                ActiveUserDayKey = outcome.Cancelled ? null : $"{userId}#{dayKey}",
                Source = "SEED",
                CheckedInAt = outcome.Status is "CHECKED_IN" or "COMPLETED" ? At(dayKey, _random.RandomInt(8, 10)).AddHours(-3) : null,
                CancelledAt = outcome.Cancelled ? createdAt.AddDays(1) : null,
                CancelledById = outcome.Cancelled ? cancelledById ?? userId : null,
                CancelReason = outcome.Cancelled ? cancelReason ?? _random.Pick(cancelReasons) : null,
                CreatedAt = createdAt, UpdatedAt = createdAt, Note = note,
            });
        }

        // Haftalik ofis plani: haftalik limit (3) asilmaz; Carsamba en yogun, Cuma en sakin.
        var attendanceByDay = new Dictionary<string, List<string>>();
        var weekStarts = allDays.Select(DayKeys.StartOfWeek).Distinct().ToList();
        foreach (var weekStart in weekStarts)
        {
            var weekDays = allDays.Where(d => DayKeys.StartOfWeek(d) == weekStart).ToList();
            foreach (var user in users)
            {
                if (user.Id == demoEmployee.Id) continue; // Elif'in gecmisi elle kurgulanir.
                var desired = Math.Max(0, Math.Min(3, JsRound(user.Attendance * 4 + (_random.Next() - 0.5) * 1.1)));
                if (desired == 0) continue;

                // Agirlikli ornekleme (Efraimidis-Spirakis).
                var ranked = weekDays
                    .Select(day => (Day: day, Score: Math.Pow(_random.Next(), 1 / weekdayWeight.GetValueOrDefault(DayKeys.WeekdayIndex(day), 0.5))))
                    .OrderByDescending(x => x.Score)
                    .Take(desired);
                foreach (var item in ranked)
                {
                    if (!attendanceByDay.TryGetValue(item.Day, out var list)) attendanceByDay[item.Day] = list = [];
                    list.Add(user.Id);
                }
            }
        }

        var userById = users.ToDictionary(u => u.Id);
        var deskById = desks.ToDictionary(d => d.Id);
        foreach (var dayKey in allDays)
        {
            var usedDesks = new HashSet<string>();
            var usedUsers = new HashSet<string>();

            foreach (var assignment in fixedAssignments.GetValueOrDefault(dayKey) ?? [])
            {
                var desk = deskById[assignment.DeskId];
                usedDesks.Add(desk.Id);
                usedUsers.Add(assignment.UserId);
                PushReservation(dayKey, assignment.UserId, desk, string.CompareOrdinal(dayKey, today) < 0 ? "COMPLETED" : null);
            }

            var attendees = (attendanceByDay.GetValueOrDefault(dayKey) ?? [])
                .Where(id => !usedUsers.Contains(id))
                .Select(id => userById[id])
                .Select(u => (User: u, Score: _random.Next()))
                .OrderByDescending(x => x.Score)
                .Select(x => x.User)
                .ToList();

            var candidateDesks = bookableDesks
                .Select(d => (Desk: d, Score: d.Weight * (0.35 + _random.Next() * 1.3)))
                .OrderByDescending(x => x.Score)
                .Select(x => x.Desk)
                .ToList();

            var deskCursor = 0;
            foreach (var user in attendees)
            {
                SeedDesk? assigned = null;
                if (user.AccessibilityApproved && _random.Next() < 0.6)
                {
                    var free = accessibleDesks.Where(d => !usedDesks.Contains(d.Id)).ToList();
                    if (free.Count > 0) assigned = free[_random.RandomInt(0, free.Count - 1)];
                }

                var attempts = 0;
                while (assigned is null && deskCursor < candidateDesks.Count && attempts < 8)
                {
                    var desk = candidateDesks[deskCursor];
                    deskCursor++;
                    attempts++;
                    if (usedDesks.Contains(desk.Id)) continue;
                    if (desk.Popular && userStreak.TryGetValue(user.Id, out var s) && s.DeskId == desk.Id && s.Streak >= 2) continue;
                    assigned = desk;
                }
                if (assigned is null) continue;

                usedDesks.Add(assigned.Id);
                usedUsers.Add(user.Id);
                userStreak[user.Id] = userStreak.TryGetValue(user.Id, out var streak) && streak.DeskId == assigned.Id
                    ? (assigned.Id, streak.Streak + 1)
                    : (assigned.Id, 1);
                PushReservation(dayKey, user.Id, assigned);
            }
        }

        // Yonetici tarafindan iptal edilmis ornek bir rezervasyon (bildirim senaryosu icin)
        var adminCancelDay = DayKeys.AddDays(today, 6);
        var adminCancelDesk = desks.Single(d => d.Code == "F3-H02");
        deskReservationRows.RemoveAll(r => r.DeskId == adminCancelDesk.Id && r.ActiveDeskKey == $"{adminCancelDesk.Id}#{adminCancelDay}");
        RemoveFirst(deskReservationRows, r => r.ActiveUserDayKey == $"{demoEmployee.Id}#{adminCancelDay}");
        PushReservation(adminCancelDay, demoEmployee.Id, adminCancelDesk, "CANCELLED", cancelledById: demoAdmin.Id, cancelReason: "Bu bölgede planlı bakım çalışması yapılacaktır.");

        // Elif icin yaklasan aktif rezervasyon
        var upcomingDay = allDays.First(d => string.CompareOrdinal(d, today) > 0);
        var upcomingDesk = desks.Single(d => d.Code == "F1-A05");
        RemoveFirst(deskReservationRows, r => r.ActiveDeskKey == $"{upcomingDesk.Id}#{upcomingDay}");
        RemoveFirst(deskReservationRows, r => r.ActiveUserDayKey == $"{demoEmployee.Id}#{upcomingDay}");
        PushReservation(upcomingDay, demoEmployee.Id, upcomingDesk, "BOOKED", note: "Ekip planlama toplantısı nedeniyle ofisteyim.");

        await BulkInsertAsync(db, deskReservationRows);
        log.WriteLine($"- {deskReservationRows.Count} masa rezervasyonu olusturuldu.");

        // --- Toplanti odasi rezervasyonlari ---
        var bookableRooms = rooms.Where(r => r.FloorReservable).ToList();
        var roomReservationRows = new List<RoomReservation>();
        var organizerPool = users.Where(u => u.Attendance > 0.4).ToList();
        int[] durations = [30, 60, 60, 90, 120];

        foreach (var dayKey in allDays)
        {
            var dayWeight = weekdayWeight.GetValueOrDefault(DayKeys.WeekdayIndex(dayKey), 0.5);
            foreach (var room in bookableRooms)
            {
                var meetingCount = JsRound(dayWeight * _random.RandomInt(1, 4));
                var cursor = DefaultWorkingHours.StartMinutes + _random.RandomInt(0, 2) * 30;
                for (var i = 0; i < meetingCount; i++)
                {
                    var duration = _random.Pick(durations);
                    if (cursor + duration > DefaultWorkingHours.EndMinutes) break;
                    var startMinutes = cursor;
                    var endMinutes = cursor + duration;
                    cursor = endMinutes + _random.RandomInt(0, 3) * 30;

                    var organizer = organizerPool[_random.RandomInt(0, organizerPool.Count - 1)];
                    var value = _random.Next();
                    var status = "BOOKED";
                    if (string.CompareOrdinal(dayKey, today) < 0) status = value < 0.06 ? "CANCELLED" : value < 0.14 ? "NO_SHOW" : "COMPLETED";
                    else if (value < 0.05) status = "CANCELLED";

                    var createdAt = At(dayKey, -_random.RandomInt(1, 5) * 24);
                    roomReservationRows.Add(new RoomReservation
                    {
                        RoomId = room.Id, UserId = organizer.Id, Date = DayKeys.ToDateOnly(dayKey),
                        StartAt = AtMinutes(dayKey, startMinutes), EndAt = AtMinutes(dayKey, endMinutes), StartMinutes = startMinutes, EndMinutes = endMinutes,
                        Title = _random.Pick(SeedData.MeetingTitles), Purpose = _random.Pick(SeedData.MeetingPurposes),
                        AttendeeCount = Math.Max(2, Math.Min(room.Capacity, _random.RandomInt(2, room.Capacity))),
                        Status = status, Source = "SEED",
                        CancelledAt = status == "CANCELLED" ? createdAt.AddHours(12) : null,
                        CancelledById = status == "CANCELLED" ? organizer.Id : null,
                        CancelReason = status == "CANCELLED" ? "Toplantı çevrimiçi yapılacak şekilde güncellendi." : null,
                        CreatedAt = createdAt, UpdatedAt = createdAt,
                    });
                }
            }
        }

        // Elif icin yaklasan bir toplanti
        var upcomingMeetingRoom = bookableRooms.Single(r => r.Code == "T102");
        var upcomingMeetingDay = upcomingDay;
        var hasConflict = roomReservationRows.Any(r => r.RoomId == upcomingMeetingRoom.Id && r.DayKey == upcomingMeetingDay
            && r.StartMinutes < 16 * 60 && 15 * 60 < r.EndMinutes && r.Status != "CANCELLED");
        if (!hasConflict)
        {
            roomReservationRows.Add(new RoomReservation
            {
                RoomId = upcomingMeetingRoom.Id, UserId = demoEmployee.Id, Date = DayKeys.ToDateOnly(upcomingMeetingDay),
                StartAt = AtMinutes(upcomingMeetingDay, 15 * 60), EndAt = AtMinutes(upcomingMeetingDay, 16 * 60), StartMinutes = 15 * 60, EndMinutes = 16 * 60,
                Title = "Ürün yol haritası değerlendirme", Purpose = "Ekip içi koordinasyon", AttendeeCount = 6, Status = "BOOKED", Source = "SEED",
            });
        }
        await BulkInsertAsync(db, roomReservationRows);
        log.WriteLine($"- {roomReservationRows.Count} toplantı odası rezervasyonu olusturuldu.");

        // --- Bildirimler ---
        var now = DateTimeOffset.UtcNow;
        DateTimeOffset HoursAgo(double hours) => now.AddHours(-hours);
        var notifications = new List<Notification>
        {
            new() { UserId = demoEmployee.Id, Type = NotificationTypes.UpcomingReservationReminder, Title = "Yaklaşan masa rezervasyonunuz", Body = $"{DayKeys.FormatLong(upcomingDay)} tarihinde {upcomingDesk.Code} kodlu masa sizin adınıza ayrılmış durumda.", Link = "/panel/rezervasyonlarim", Severity = "INFO", CreatedAt = HoursAgo(3) },
            new() { UserId = demoEmployee.Id, Type = NotificationTypes.DeskReservationCancelledByAdmin, Title = "Rezervasyonunuz yönetim tarafından iptal edildi", Body = $"{DayKeys.FormatLong(adminCancelDay)} tarihli {adminCancelDesk.Code} masa rezervasyonunuz planlı bakım çalışması nedeniyle iptal edildi.", Link = "/panel/masa", Severity = "WARNING", CreatedAt = HoursAgo(20) },
            new() { UserId = demoEmployee.Id, Type = NotificationTypes.AlternativeDeskSuggestion, Title = "Alternatif masa önerisi", Body = $"{DayKeys.FormatLong(adminCancelDay)} için {adminCancelDesk.Code} yerine F3-H03 ve F3-I01 masaları uygun görünüyor.", Link = "/panel/masa", Severity = "INFO", CreatedAt = HoursAgo(20) },
            new() { UserId = demoEmployee.Id, Type = NotificationTypes.RoomReservationCreated, Title = "Toplantı odası rezervasyonunuz oluşturuldu", Body = $"{upcomingMeetingRoom.Code} - {DayKeys.FormatLong(upcomingMeetingDay)} 15:00 - 16:00 aralığı için rezervasyonunuz onaylandı.", Link = "/panel/rezervasyonlarim", Severity = "SUCCESS", ReadAt = HoursAgo(40), CreatedAt = HoursAgo(46) },
            new() { UserId = demoEmployee.Id, Type = NotificationTypes.ResourceClosed, Title = "Masa geçici olarak kullanıma kapatıldı", Body = $"{closedDesk.Code} kodlu masa {DayKeys.FormatLong(DayKeys.AddDays(today, -1), withWeekday: false)} - {DayKeys.FormatLong(DayKeys.AddDays(today, 5), withWeekday: false)} tarihleri arasında bakım nedeniyle kapalıdır.", Link = "/panel/masa", Severity = "WARNING", ReadAt = HoursAgo(60), CreatedAt = HoursAgo(72) },
        };
        foreach (var demoUser in new[] { demoEmployee, demoManager, demoAdmin, demoAccessible })
        {
            for (var i = 0; i < SeedData.Announcements.Length; i++)
            {
                var a = SeedData.Announcements[i];
                notifications.Add(new Notification { UserId = demoUser.Id, Type = NotificationTypes.Announcement, Title = a.Title, Body = a.Body, Severity = "INFO", ReadAt = i == 1 ? HoursAgo(90) : null, CreatedAt = HoursAgo(24 * (i + 2)) });
            }
        }
        notifications.Add(new Notification { UserId = demoAccessible.Id, Type = NotificationTypes.DeskReservationCreated, Title = "Masa rezervasyonunuz oluşturuldu", Body = "Erişilebilirlik için ayrılmış masa talebiniz onaylandı ve rezervasyonunuz oluşturuldu.", Link = "/panel/rezervasyonlarim", Severity = "SUCCESS", CreatedAt = HoursAgo(8) });
        await BulkInsertAsync(db, notifications);
        log.WriteLine($"- {notifications.Count} bildirim olusturuldu.");

        // --- Denetim kayitlari ---
        AuditLog Audit(SeedUser actor, string role, string action, string entityType, string? entityId, string summary, object? metadata, double hoursAgo) => new()
        {
            ActorId = actor.Id, ActorName = actor.FullName, ActorRole = role, Action = action, EntityType = entityType, EntityId = entityId,
            Summary = summary, Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata, Json), CreatedAt = HoursAgo(hoursAgo),
        };
        var auditRows = new List<AuditLog>
        {
            Audit(demoAdmin, "ADMIN", AuditActions.SettingUpdate, "SystemSetting", SettingKeys.Announcement, "Ana sayfa duyurusu güncellendi.", new { key = SettingKeys.Announcement }, 120),
            Audit(demoAdmin, "ADMIN", AuditActions.ClosureCreate, "Closure", closedDesk.Id, $"{closedDesk.Code} kodlu masa bakım nedeniyle kapatıldı.", new { scope = "DESK", reason = "MAINTENANCE" }, 72),
            Audit(demoAdmin, "ADMIN", AuditActions.ClosureCreate, "Closure", closedRoom.Id, $"{closedRoom.Code} toplantı odası etkinlik nedeniyle kapatıldı.", new { scope = "ROOM", reason = "EVENT" }, 70),
            Audit(demoAdmin, "ADMIN", AuditActions.DeskReservationCancel, "DeskReservation", null, $"{demoEmployee.FullName} adlı çalışanın {adminCancelDesk.Code} masa rezervasyonu yönetim tarafından iptal edildi.", new { deskCode = adminCancelDesk.Code, date = adminCancelDay }, 20),
            Audit(demoAdmin, "ADMIN", AuditActions.RuleUpdate, "ReservationRule", "WEEKLY_DESK_LIMIT", "Haftalık masa rezervasyon limiti 3 olarak güncellendi.", new { key = "WEEKLY_DESK_LIMIT", intValue = 3 }, 200),
            Audit(demoManager, "MANAGER", AuditActions.ReportExport, "Report", "occupancy", "Doluluk raporu CSV olarak dışa aktarıldı.", new { report = "occupancy" }, 30),
        };
        foreach (var demoUser in new[] { demoEmployee, demoManager, demoAdmin, demoAccessible })
        {
            auditRows.Add(Audit(demoUser, demoUser.Role, AuditActions.Login, "User", demoUser.Id, $"{demoUser.FullName} demo hesabıyla oturum açtı.", null, _random.RandomInt(2, 48)));
        }
        await BulkInsertAsync(db, auditRows);
        log.WriteLine($"- {auditRows.Count} denetim kaydi olusturuldu.");
        log.WriteLine("Seed tamamlandi.");
    }

    private static void RemoveFirst<T>(List<T> list, Predicate<T> match)
    {
        var index = list.FindIndex(match);
        if (index >= 0) list.RemoveAt(index);
    }
}
