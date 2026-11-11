namespace Ofis360.Domain.Entities;

public class Department
{
    public string Id { get; set; } = Common.Ids.New();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#0f4c81";
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AppUser> Users { get; set; } = new List<AppUser>();
}
