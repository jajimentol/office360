namespace Ofis360.Domain.Common;

/// <summary>SystemSetting.type degerleri (kaynak: TEXT | NUMBER | BOOLEAN | TIME).</summary>
public static class SettingTypes
{
    public const string Text = "TEXT";
    public const string Number = "NUMBER";
    public const string Boolean = "BOOLEAN";
    public const string Time = "TIME";

    public static readonly IReadOnlyList<string> All = [Text, Number, Boolean, Time];
}
