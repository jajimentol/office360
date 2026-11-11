using Ofis360.Domain.Common;

namespace Ofis360.Domain.Entities;

public class SystemSetting
{
    public string Id { get; set; } = Ids.New();
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = SettingTypes.Text;
    public string Group { get; set; } = "GENERAL";
    public int Order { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
