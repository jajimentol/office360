using FluentValidation;
using Ofis360.Application.Common;
using Ofis360.Domain.Common;

namespace Ofis360.Application.Validation;

// Kaynak: src/lib/schemas.ts (kroki disindaki semalar). Zod sinirlari ve Turkce mesajlar birebir.
// Zod .partial() karsiligi: guncelleme girdilerinde tum alanlar nullable; null = "gonderilmedi".

public sealed record DeskReservationInput(string DeskId, string Date, string? Note = null, string? TargetUserId = null);

public sealed record CancelInput(string Action = "cancel", string? Reason = null);

public sealed record RoomReservationInput(
    string RoomId, string Date, int StartMinutes, int EndMinutes, string Title, string Purpose, int AttendeeCount, string? TargetUserId = null);

public sealed record PreferencesInput(Optional<string?> PreferredFloorId = default, bool? PrefersQuietZone = null, bool? NotificationsEnabled = null);

public sealed record NotificationActionInput(string Action, string? NotificationId = null);

public sealed record BuildingInput(string Code, string Name, string Address = "", int Order = 0, bool Active = true);

public sealed record BuildingUpdateInput(string? Code = null, string? Name = null, string? Address = null, int? Order = null, bool? Active = null);

public sealed record FloorInput(
    string BuildingId, int Number, string Name, string Description = "", bool Reservable = true, bool Active = true,
    int? PlanWidth = null, int? PlanHeight = null);

public sealed record FloorUpdateInput(
    string? BuildingId = null, int? Number = null, string? Name = null, string? Description = null, bool? Reservable = null, bool? Active = null,
    int? PlanWidth = null, int? PlanHeight = null);

public sealed record ZoneUpdateInput(
    string? Name = null, string? Kind = null, int? X = null, int? Y = null, int? Width = null, int? Height = null,
    bool? WindowSide = null, bool? Quiet = null, bool? Reservable = null);

public sealed record DeskInput(
    string Code, string FloorId, string ZoneId, int X, int Y, bool Active = true, bool AccessibleOnly = false,
    bool Popular = false, string? Notes = null, IReadOnlyList<string>? FeatureKeys = null);

public sealed record DeskUpdateInput(
    string? Code = null, string? FloorId = null, string? ZoneId = null, int? X = null, int? Y = null, bool? Active = null,
    bool? AccessibleOnly = null, bool? Popular = null, Optional<string?> Notes = default, IReadOnlyList<string>? FeatureKeys = null);

public sealed record RoomInput(
    string Code, string Name, string FloorId, int Capacity, int X, int Y, int Width, int Height, bool Active = true,
    string? Notes = null, IReadOnlyList<string>? FeatureKeys = null);

public sealed record RoomUpdateInput(
    string? Code = null, string? Name = null, string? FloorId = null, int? Capacity = null, int? X = null, int? Y = null,
    int? Width = null, int? Height = null, bool? Active = null, Optional<string?> Notes = default, IReadOnlyList<string>? FeatureKeys = null);

public sealed record UserInput(
    string FullName, string Email, string Title, string Role, string DepartmentId, bool AccessibilityApproved = false, bool Active = true,
    string? Grade = null, bool IsSecretary = false);

public sealed record UserUpdateInput(
    string? FullName = null, string? Email = null, string? Title = null, string? Role = null, string? DepartmentId = null,
    bool? AccessibilityApproved = null, bool? Active = null, Optional<string?> Grade = default, bool? IsSecretary = null);

public sealed record UserImportInput(string Csv, bool Apply = false);

public sealed record RuleUpdateInput(bool? Enabled = null, Optional<int?> IntValue = default, Optional<int?> SecondValue = default);

public sealed record RuleMatrixCell(string RuleKey, string Grade, bool? Enabled = null, int? IntValue = null);

public sealed record RuleMatrixInput(string? BuildingId, IReadOnlyList<RuleMatrixCell> Cells);

public sealed record HolidayInput(string Date, string Name);

public sealed record RoomGradeRuleInput(string Grade, int MinAttendees, bool BypassPriority = false);

public sealed record RoomPolicyInput(bool SecretaryOnly, int? PriorityOpenDays, IReadOnlyList<RoomGradeRuleInput> GradeRules, IReadOnlyList<string> PriorityUserIds);

public sealed record ClosureInput(string Scope, string TargetId, string StartDayKey, string EndDayKey, string Reason, string Note);

public sealed record ClosureUpdateInput(bool? Active = null, string? Note = null, string? EndDayKey = null);

public sealed record SettingUpdateInput(string Value);

public sealed record LoginInput(string UserId);

internal static class Rules
{
    public static IRuleBuilderOptions<T, string> DayKey<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(v => DayKeys.IsValid(v)).WithMessage("Geçerli bir tarih seçin.");
}

public sealed class DeskReservationValidator : AbstractValidator<DeskReservationInput>
{
    public DeskReservationValidator()
    {
        RuleFor(x => x.DeskId).NotEmpty().WithMessage("Masa seçilmedi.");
        RuleFor(x => x.Date).DayKey();
        RuleFor(x => x.Note).MaximumLength(240).WithMessage("Not en fazla 240 karakter olabilir.");
    }
}

public sealed class CancelValidator : AbstractValidator<CancelInput>
{
    public CancelValidator()
    {
        RuleFor(x => x.Action).Must(a => a is "cancel" or "check-in").WithMessage("Geçersiz işlem.");
        RuleFor(x => x.Reason).MaximumLength(240).WithMessage("Gerekçe en fazla 240 karakter olabilir.");
    }
}

public sealed class RoomReservationValidator : AbstractValidator<RoomReservationInput>
{
    public RoomReservationValidator()
    {
        RuleFor(x => x.RoomId).NotEmpty().WithMessage("Toplantı odası seçilmedi.");
        RuleFor(x => x.Date).DayKey();
        RuleFor(x => x.StartMinutes).InclusiveBetween(0, 24 * 60);
        RuleFor(x => x.EndMinutes).InclusiveBetween(0, 24 * 60);
        RuleFor(x => x.Title).MinimumLength(3).WithMessage("Toplantı başlığı en az 3 karakter olmalıdır.")
            .MaximumLength(120).WithMessage("Toplantı başlığı en fazla 120 karakter olabilir.");
        RuleFor(x => x.Purpose).NotEmpty().WithMessage("Rezervasyon amacı seçilmelidir.").MaximumLength(120);
        RuleFor(x => x.AttendeeCount).GreaterThanOrEqualTo(1).WithMessage("Katılımcı sayısı en az 1 olmalıdır.").LessThanOrEqualTo(200);
        RuleFor(x => x.EndMinutes).GreaterThan(x => x.StartMinutes).WithMessage("Bitiş saati başlangıç saatinden sonra olmalıdır.");
    }
}

public sealed class NotificationActionValidator : AbstractValidator<NotificationActionInput>
{
    public NotificationActionValidator()
    {
        RuleFor(x => x.Action).Must(a => a is "read" or "read-all").WithMessage("Geçersiz işlem.");
    }
}

public sealed class BuildingValidator : AbstractValidator<BuildingInput>
{
    public BuildingValidator()
    {
        RuleFor(x => x.Code).MinimumLength(2).WithMessage("Bina kodu en az 2 karakter olmalıdır.").MaximumLength(12)
            .Matches("^[A-Z0-9-]+$").WithMessage("Bina kodu yalnızca büyük harf, rakam ve tire içerebilir.");
        RuleFor(x => x.Name).MinimumLength(2).WithMessage("Bina adı zorunludur.").MaximumLength(80);
        RuleFor(x => x.Address).MaximumLength(240);
        RuleFor(x => x.Order).InclusiveBetween(0, 999);
    }
}

public sealed class FloorValidator : AbstractValidator<FloorInput>
{
    public FloorValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("Bina seçilmelidir.");
        RuleFor(x => x.Number).InclusiveBetween(-5, 200);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kat adı zorunludur.").MaximumLength(60);
        RuleFor(x => x.Description).MaximumLength(240);
        RuleFor(x => x.PlanWidth).InclusiveBetween(400, 4000).When(x => x.PlanWidth.HasValue);
        RuleFor(x => x.PlanHeight).InclusiveBetween(300, 4000).When(x => x.PlanHeight.HasValue);
    }
}

public sealed class FloorUpdateValidator : AbstractValidator<FloorUpdateInput>
{
    public FloorUpdateValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("Bina seçilmelidir.").When(x => x.BuildingId is not null);
        RuleFor(x => x.Number).InclusiveBetween(-5, 200).When(x => x.Number.HasValue);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Kat adı zorunludur.").MaximumLength(60).When(x => x.Name is not null);
        RuleFor(x => x.Description).MaximumLength(240).When(x => x.Description is not null);
        RuleFor(x => x.PlanWidth).InclusiveBetween(400, 4000).When(x => x.PlanWidth.HasValue);
        RuleFor(x => x.PlanHeight).InclusiveBetween(300, 4000).When(x => x.PlanHeight.HasValue);
    }
}

public sealed class ZoneUpdateValidator : AbstractValidator<ZoneUpdateInput>
{
    public ZoneUpdateValidator()
    {
        RuleFor(x => x.Kind!).Must(k => ZoneKinds.All.Contains(k)).WithMessage("Geçersiz bölge türü.").When(x => x.Kind is not null);
        RuleFor(x => x.Name).MinimumLength(1).MaximumLength(60).When(x => x.Name is not null);
        RuleFor(x => x.X).GreaterThanOrEqualTo(0).When(x => x.X.HasValue);
        RuleFor(x => x.Y).GreaterThanOrEqualTo(0).When(x => x.Y.HasValue);
        RuleFor(x => x.Width).GreaterThanOrEqualTo(20).When(x => x.Width.HasValue);
        RuleFor(x => x.Height).GreaterThanOrEqualTo(20).When(x => x.Height.HasValue);
    }
}

public sealed class DeskValidator : AbstractValidator<DeskInput>
{
    public DeskValidator()
    {
        RuleFor(x => x.Code).MinimumLength(2).WithMessage("Masa kodu en az 2 karakter olmalıdır.").MaximumLength(20)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Masa kodu yalnızca harf, rakam ve tire içerebilir.");
        RuleFor(x => x.FloorId).NotEmpty().WithMessage("Kat seçilmelidir.");
        RuleFor(x => x.ZoneId).NotEmpty().WithMessage("Bölge seçilmelidir.");
        RuleFor(x => x.X).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Y).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(240);
    }
}

public sealed class DeskUpdateValidator : AbstractValidator<DeskUpdateInput>
{
    public DeskUpdateValidator()
    {
        RuleFor(x => x.Code).MinimumLength(2).WithMessage("Masa kodu en az 2 karakter olmalıdır.").MaximumLength(20)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Masa kodu yalnızca harf, rakam ve tire içerebilir.").When(x => x.Code is not null);
        RuleFor(x => x.FloorId).NotEmpty().WithMessage("Kat seçilmelidir.").When(x => x.FloorId is not null);
        RuleFor(x => x.ZoneId).NotEmpty().WithMessage("Bölge seçilmelidir.").When(x => x.ZoneId is not null);
        RuleFor(x => x.X).GreaterThanOrEqualTo(0).When(x => x.X.HasValue);
        RuleFor(x => x.Y).GreaterThanOrEqualTo(0).When(x => x.Y.HasValue);
        RuleFor(x => x.Notes.Value).MaximumLength(240).When(x => x.Notes.HasValue);
    }
}

public sealed class RoomValidator : AbstractValidator<RoomInput>
{
    public RoomValidator()
    {
        RuleFor(x => x.Code).MinimumLength(2).MaximumLength(20);
        RuleFor(x => x.Name).MinimumLength(2).WithMessage("Oda adı zorunludur.").MaximumLength(60);
        RuleFor(x => x.FloorId).NotEmpty().WithMessage("Kat seçilmelidir.");
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(1).WithMessage("Kapasite en az 1 olmalıdır.").LessThanOrEqualTo(200);
        RuleFor(x => x.X).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Y).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Width).InclusiveBetween(40, 2000);
        RuleFor(x => x.Height).InclusiveBetween(40, 2000);
        RuleFor(x => x.Notes).MaximumLength(240);
    }
}

public sealed class RoomUpdateValidator : AbstractValidator<RoomUpdateInput>
{
    public RoomUpdateValidator()
    {
        RuleFor(x => x.Code).MinimumLength(2).MaximumLength(20).When(x => x.Code is not null);
        RuleFor(x => x.Name).MinimumLength(2).WithMessage("Oda adı zorunludur.").MaximumLength(60).When(x => x.Name is not null);
        RuleFor(x => x.FloorId).NotEmpty().WithMessage("Kat seçilmelidir.").When(x => x.FloorId is not null);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 200).WithMessage("Kapasite en az 1 olmalıdır.").When(x => x.Capacity.HasValue);
        RuleFor(x => x.X).GreaterThanOrEqualTo(0).When(x => x.X.HasValue);
        RuleFor(x => x.Y).GreaterThanOrEqualTo(0).When(x => x.Y.HasValue);
        RuleFor(x => x.Width).InclusiveBetween(40, 2000).When(x => x.Width.HasValue);
        RuleFor(x => x.Height).InclusiveBetween(40, 2000).When(x => x.Height.HasValue);
        RuleFor(x => x.Notes.Value).MaximumLength(240).When(x => x.Notes.HasValue);
    }
}

public sealed class UserValidator : AbstractValidator<UserInput>
{
    public UserValidator()
    {
        RuleFor(x => x.FullName).MinimumLength(3).WithMessage("Ad soyad en az 3 karakter olmalıdır.").MaximumLength(80);
        RuleFor(x => x.Email).EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");
        RuleFor(x => x.Title).MinimumLength(2).WithMessage("Unvan zorunludur.").MaximumLength(80);
        RuleFor(x => x.Role).Must(Roles.IsValid).WithMessage("Geçersiz rol.");
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("Departman seçilmelidir.");
        RuleFor(x => x.Grade).Must(Grades.IsValid).WithMessage("Geçersiz kademe.").When(x => x.Grade is not null);
    }
}

public sealed class HolidayValidator : AbstractValidator<HolidayInput>
{
    public HolidayValidator()
    {
        RuleFor(x => x.Date).DayKey();
        RuleFor(x => x.Name).MinimumLength(2).WithMessage("Tatil adı zorunludur.").MaximumLength(80);
    }
}

public sealed class UserImportValidator : AbstractValidator<UserImportInput>
{
    public const int MaxLength = 2_000_000;

    public UserImportValidator()
    {
        RuleFor(x => x.Csv).NotEmpty().WithMessage("Dosya boş.").MaximumLength(MaxLength).WithMessage("Dosya çok büyük.");
    }
}

public sealed class UserUpdateValidator : AbstractValidator<UserUpdateInput>
{
    public UserUpdateValidator()
    {
        RuleFor(x => x.FullName).MinimumLength(3).WithMessage("Ad soyad en az 3 karakter olmalıdır.").MaximumLength(80).When(x => x.FullName is not null);
        RuleFor(x => x.Email).EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.").When(x => x.Email is not null);
        RuleFor(x => x.Title).MinimumLength(2).WithMessage("Unvan zorunludur.").MaximumLength(80).When(x => x.Title is not null);
        RuleFor(x => x.Role!).Must(Roles.IsValid).WithMessage("Geçersiz rol.").When(x => x.Role is not null);
        RuleFor(x => x.DepartmentId).NotEmpty().WithMessage("Departman seçilmelidir.").When(x => x.DepartmentId is not null);
        RuleFor(x => x.Grade).Must(g => !g.HasValue || g.Value is null || Grades.IsValid(g.Value)).WithMessage("Geçersiz kademe.");
    }
}

public sealed class ClosureValidator : AbstractValidator<ClosureInput>
{
    public ClosureValidator()
    {
        RuleFor(x => x.Scope).Must(s => ClosureScopes.All.Contains(s)).WithMessage("Geçersiz kapatma kapsamı.");
        RuleFor(x => x.TargetId).NotEmpty().WithMessage("Kapatılacak alan seçilmelidir.");
        RuleFor(x => x.StartDayKey).DayKey();
        RuleFor(x => x.EndDayKey).DayKey();
        RuleFor(x => x.Reason).Must(r => ClosureReasons.All.Contains(r)).WithMessage("Geçersiz kapatma nedeni.");
        RuleFor(x => x.Note).MinimumLength(3).WithMessage("Açıklama en az 3 karakter olmalıdır.").MaximumLength(240);
        RuleFor(x => x.EndDayKey)
            .Must((input, end) => string.CompareOrdinal(end, input.StartDayKey) >= 0)
            .WithMessage("Bitiş tarihi başlangıç tarihinden önce olamaz.")
            .When(x => DayKeys.IsValid(x.StartDayKey) && DayKeys.IsValid(x.EndDayKey));
    }
}

public sealed class ClosureUpdateValidator : AbstractValidator<ClosureUpdateInput>
{
    public ClosureUpdateValidator()
    {
        RuleFor(x => x.Note).MinimumLength(3).MaximumLength(240).When(x => x.Note is not null);
        RuleFor(x => x.EndDayKey!).DayKey().When(x => x.EndDayKey is not null);
    }
}

public sealed class SettingUpdateValidator : AbstractValidator<SettingUpdateInput>
{
    public SettingUpdateValidator()
    {
        RuleFor(x => x.Value).NotEmpty().WithMessage("Değer boş bırakılamaz.").MaximumLength(500);
    }
}

public sealed class LoginValidator : AbstractValidator<LoginInput>
{
    public LoginValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Kullanıcı seçilmelidir.");
    }
}
