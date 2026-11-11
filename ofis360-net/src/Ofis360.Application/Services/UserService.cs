using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Services;

public sealed record UserListFilters(
    string? Search = null, string? Role = null, string? DepartmentId = null, string? Status = null, int? Take = null, int? Skip = null, string? Grade = null);

public sealed record UserListItem(AppUser User, int DeskReservationCount, int RoomReservationCount);

public sealed record UserStats(int Total, int Active, int Passive, int Employees, int Managers, int Admins, int Accessibility, int WithoutGrade = 0, int Secretaries = 0);

public sealed record DepartmentListItem(Department Department, int UserCount);

public sealed record PersonSearchResult(string Id, string FullName, string Title, string DepartmentName);

/// <summary>Kullanici ve rol yonetimi (admin) + calisan profil tercihleri.</summary>
public sealed class UserService(IOfis360Db db, AuditService audit, IClock clock)
{
    public const string GradeFilterNone = "NONE";
    public const string GradeFilterSecretary = "SECRETARY";

    private static readonly Dictionary<string, int> DemoOrder = new() { [Roles.Employee] = 0, [Roles.Manager] = 1, [Roles.Admin] = 2 };

    /// <summary>Admin kullanici listesi; 300 kullanici olceginde sayfalama zorunludur.</summary>
    public async Task<PagedResult<UserListItem>> ListAsync(UserListFilters filters, CancellationToken ct = default)
    {
        var q = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(filters.Role)) q = q.Where(u => u.Role == filters.Role);
        if (!string.IsNullOrEmpty(filters.DepartmentId)) q = q.Where(u => u.DepartmentId == filters.DepartmentId);
        if (!string.IsNullOrEmpty(filters.Status)) { var active = filters.Status == "active"; q = q.Where(u => u.Active == active); }
        if (filters.Grade == GradeFilterNone) q = q.Where(u => u.Grade == null);
        else if (filters.Grade == GradeFilterSecretary) q = q.Where(u => u.IsSecretary);
        else if (!string.IsNullOrEmpty(filters.Grade)) q = q.Where(u => u.Grade == filters.Grade);
        if (!string.IsNullOrEmpty(filters.Search))
        {
            var pattern = Search.Pattern(filters.Search);
            q = q.Where(u => EF.Functions.Like(u.FullName.ToUpper().Replace("İ", "I"), pattern, Search.Escape) || EF.Functions.Like(u.Email.ToUpper().Replace("İ", "I"), pattern, Search.Escape) || EF.Functions.Like(u.Title.ToUpper().Replace("İ", "I"), pattern, Search.Escape));
        }

        var take = Paging.Take(filters.Take, 50);
        var skip = Paging.Skip(filters.Skip);
        var total = await q.CountAsync(ct);
        var items = await q.Include(u => u.Department)
            .OrderByDescending(u => u.Active).ThenBy(u => u.FullName)
            .Skip(skip).Take(take)
            .Select(u => new UserListItem(u, u.DeskReservations.Count, u.RoomReservations.Count))
            .ToListAsync(ct);
        return new PagedResult<UserListItem>(items, total, take, skip);
    }

    /// <summary>Sekreter/Admin icin kisi secici: yalniz aktif kullanicilar, sinirli alan.</summary>
    public async Task<List<PersonSearchResult>> SearchPeopleAsync(string? search, CancellationToken ct = default)
    {
        var q = db.Users.AsNoTracking().Where(u => u.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = Search.Pattern(search);
            q = q.Where(u => EF.Functions.Like(u.FullName.ToUpper().Replace("İ", "I"), pattern, Search.Escape) || EF.Functions.Like(u.Email.ToUpper().Replace("İ", "I"), pattern, Search.Escape));
        }
        return await q.OrderBy(u => u.FullName).Take(20)
            .Select(u => new PersonSearchResult(u.Id, u.FullName, u.Title, u.Department.Name))
            .ToListAsync(ct);
    }

    public async Task<UserStats> GetStatsAsync(CancellationToken ct = default)
    {
        var total = await db.Users.CountAsync(ct);
        var active = await db.Users.CountAsync(u => u.Active, ct);
        var byRole = await db.Users.GroupBy(u => u.Role).Select(g => new { Role = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Role, x => x.Count, ct);
        var accessibility = await db.Users.CountAsync(u => u.AccessibilityApproved, ct);
        var withoutGrade = await db.Users.CountAsync(u => u.Grade == null, ct);
        var secretaries = await db.Users.CountAsync(u => u.IsSecretary, ct);
        return new UserStats(total, active, total - active,
            byRole.GetValueOrDefault(Roles.Employee), byRole.GetValueOrDefault(Roles.Manager), byRole.GetValueOrDefault(Roles.Admin), accessibility,
            withoutGrade, secretaries);
    }

    /// <summary>Demo hesaplari: once calisan, sonra yonetici ve admin; erisilebilirlik profili sonda.</summary>
    public async Task<List<AppUser>> ListDemoAccountsAsync(CancellationToken ct = default)
    {
        var accounts = await db.Users.AsNoTracking().Include(u => u.Department).Where(u => u.IsDemoAccount && u.Active).ToListAsync(ct);
        return accounts
            .OrderBy(u => DemoOrder.GetValueOrDefault(u.Role, 9))
            .ThenBy(u => u.AccessibilityApproved ? 1 : 0)
            .ToList();
    }

    public Task<List<DepartmentListItem>> ListDepartmentsAsync(CancellationToken ct = default) =>
        db.Departments.AsNoTracking().Where(d => d.Active).OrderBy(d => d.Name)
            .Select(d => new DepartmentListItem(d, d.Users.Count)).ToListAsync(ct);

    /// <summary>Kurumsal giris e-postayi harf duyarsiz eslestirir; kayit da tek bicimde tutulur.</summary>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<AppUser> CreateAsync(UserInput input, AuditActor actor, CancellationToken ct = default)
    {
        if (!Roles.IsValid(input.Role)) throw AppErrors.Validation("Geçersiz rol.");
        var email = NormalizeEmail(input.Email);
        if (await db.Users.AnyAsync(u => u.Email.ToLower() == email, ct)) throw AppErrors.Conflict("Bu e-posta adresi zaten kayıtlı.");
        var department = await db.Departments.FirstOrDefaultAsync(d => d.Id == input.DepartmentId, ct)
            ?? throw AppErrors.NotFound("Departman bulunamadı.");

        var user = new AppUser
        {
            FullName = input.FullName, Email = email, Title = input.Title, Role = input.Role, DepartmentId = input.DepartmentId,
            AccessibilityApproved = input.AccessibilityApproved, Active = input.Active, Grade = input.Grade, IsSecretary = input.IsSecretary,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        user.Department = department;

        await audit.RecordAsync(actor, AuditActions.UserCreate, "User", user.Id,
            $"{user.FullName} ({user.Email}) kullanıcısı eklendi.", new { role = user.Role, department = department.Name }, ct);
        return user;
    }

    public async Task<AppUser> UpdateAsync(string userId, UserUpdateInput input, AuditActor actor, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw AppErrors.NotFound("Kullanıcı bulunamadı.");

        if (input.Role is not null && !Roles.IsValid(input.Role)) throw AppErrors.Validation("Geçersiz rol.");
        var newEmail = input.Email is null ? null : NormalizeEmail(input.Email);
        if (newEmail is not null && await db.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == newEmail, ct))
        {
            throw AppErrors.Conflict("Bu e-posta adresi zaten kayıtlı.");
        }

        var removesActiveAdmin = user.Role == Roles.Admin && user.Active && ((input.Role is not null && input.Role != Roles.Admin) || input.Active == false);
        var deactivating = user.Active && input.Active == false;

        if (input.FullName is not null) user.FullName = input.FullName;
        if (newEmail is not null) user.Email = newEmail;
        if (input.Title is not null) user.Title = input.Title;
        if (input.Role is not null) user.Role = input.Role;
        if (input.DepartmentId is not null) user.DepartmentId = input.DepartmentId;
        if (input.AccessibilityApproved is bool aa) user.AccessibilityApproved = aa;
        if (input.Active is bool active) user.Active = active;
        if (input.Grade.HasValue) user.Grade = input.Grade.Value;
        if (input.IsSecretary is bool secretary) user.IsSecretary = secretary;

        var cancelled = await db.InTransactionAsync(async token =>
        {
            // Sistemde en az bir aktif admin kalmalidir. Sayim kilitten sonra yapilir: iki admin ayni anda birbirini kapatamaz.
            if (removesActiveAdmin)
            {
                await db.LockActiveAdminsAsync(token);
                if (!await db.Users.AnyAsync(u => u.Id != userId && u.Role == Roles.Admin && u.Active, token))
                {
                    throw AppErrors.Validation("Sistemde en az bir aktif yönetici (admin) bulunmalıdır.");
                }
            }

            await db.SaveChangesAsync(token);
            return deactivating ? await CancelFutureReservationsAsync(userId, actor, token) : 0;
        }, ct);
        if (input.DepartmentId is not null) user.Department = await db.Departments.FirstAsync(d => d.Id == user.DepartmentId, ct);

        await audit.RecordAsync(actor, AuditActions.UserUpdate, "User", userId, $"{user.FullName} kullanıcısı güncellendi.",
            new
            {
                input.FullName, input.Email, input.Title, input.Role, input.DepartmentId, input.AccessibilityApproved, input.Active,
                Grade = input.Grade.HasValue ? input.Grade.Value : null, input.IsSecretary, cancelledReservations = cancelled,
            }, ct);
        return user;
    }

    /// <summary>Pasife alinan kisinin bugun ve sonrasindaki rezervasyonlari kaynagi bosaltmak icin iptal edilir.</summary>
    private async Task<int> CancelFutureReservationsAsync(string userId, AuditActor actor, CancellationToken ct)
    {
        const string Reason = "Kullanıcı hesabı pasife alındı.";
        var today = clock.Today;
        var now = clock.UtcNow;

        var desks = await db.DeskReservations
            .Where(r => r.UserId == userId && r.Date >= today && r.Status == DeskReservationStatus.Booked)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, DeskReservationStatus.Cancelled)
                .SetProperty(r => r.ActiveDeskKey, (string?)null)
                .SetProperty(r => r.ActiveUserDayKey, (string?)null)
                .SetProperty(r => r.CancelledAt, now)
                .SetProperty(r => r.CancelledById, actor.Id)
                .SetProperty(r => r.CancelReason, Reason)
                .SetProperty(r => r.UpdatedAt, now), ct);
        var rooms = await db.RoomReservations
            .Where(r => r.UserId == userId && r.Date >= today && r.Status == RoomReservationStatus.Booked)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, RoomReservationStatus.Cancelled)
                .SetProperty(r => r.CancelledAt, now)
                .SetProperty(r => r.CancelledById, actor.Id)
                .SetProperty(r => r.CancelReason, Reason)
                .SetProperty(r => r.UpdatedAt, now), ct);
        return desks + rooms;
    }

    public async Task<AppUser> UpdateOwnPreferencesAsync(string userId, PreferencesInput input, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw AppErrors.NotFound("Kullanıcı bulunamadı.");
        if (input.PreferredFloorId is { HasValue: true, Value: string floorId }
            && !await db.Floors.AnyAsync(f => f.Id == floorId && f.Active && f.Building.Active, ct))
        {
            throw AppErrors.Validation("Seçilen kat bulunamadı.");
        }
        if (input.PreferredFloorId.HasValue) user.PreferredFloorId = input.PreferredFloorId.Value;
        if (input.PrefersQuietZone is bool quiet) user.PrefersQuietZone = quiet;
        if (input.NotificationsEnabled is bool enabled) user.NotificationsEnabled = enabled;
        await db.SaveChangesAsync(ct);
        return user;
    }
}
