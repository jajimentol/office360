using Ofis360.Domain.Common;

namespace Ofis360.Domain.Entities;

/// <summary>Sunucu tarafli oturum. Bilet yalnizca cerezde, burada ozeti durur.</summary>
public class AppSession
{
    public string TokenHash { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public AppUser User { get; set; } = null!;
}

/// <summary>Kaynak: Prisma User. Oracle'da USER ayrilmis sozcuk oldugu icin tablo adi APP_USER.</summary>
public class AppUser
{
    public string Id { get; set; } = Ids.New();
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Employee;
    public string DepartmentId { get; set; } = string.Empty;
    /// <summary>Kurumsal kademe (<see cref="Grades"/>); personel listesi gelene kadar bos olabilir.</summary>
    public string? Grade { get; set; }
    /// <summary>Sekreter yetkisi: kademeden bagimsizdir.</summary>
    public bool IsSecretary { get; set; }
    public bool AccessibilityApproved { get; set; }
    public bool IsDemoAccount { get; set; }
    public bool Active { get; set; } = true;
    public string? PreferredFloorId { get; set; }
    public bool PrefersQuietZone { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Department Department { get; set; } = null!;
    public ICollection<DeskReservation> DeskReservations { get; set; } = new List<DeskReservation>();
    public ICollection<RoomReservation> RoomReservations { get; set; } = new List<RoomReservation>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
