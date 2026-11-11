using FluentValidation;
using Ofis360.Domain.Plan;

namespace Ofis360.Application.Validation;

// Kaynak: src/lib/schemas.ts (kat krokisi bolumu). Sinirlar ve Turkce mesajlar birebir.

public sealed record PlanStyleInput(string? Fill = null, string? Stroke = null, double? StrokeWidth = null, double? FontSize = null);

public sealed record PlanElementInput(
    string Id,
    string Type,
    string? Label,
    int X,
    int Y,
    int Width,
    int Height,
    int Rotation = 0,
    PlanStyleInput? Style = null,
    int LayerOrder = 0,
    bool Locked = false,
    bool Visible = true,
    string? LinkedResourceId = null,
    string? LinkedResourceType = null);

public sealed record DeskPlacementInput(string Id, int X, int Y, int Width = 46, int Height = 34, int Rotation = 0);

public sealed record RoomPlacementInput(string Id, int X, int Y, int Width, int Height);

public sealed record SavePlanInput(
    string FloorId,
    IReadOnlyList<PlanElementInput> Elements,
    IReadOnlyList<DeskPlacementInput>? DeskPlacements = null,
    IReadOnlyList<RoomPlacementInput>? RoomPlacements = null)
{
    /// <summary>Zod .default([]) karsiligi.</summary>
    public IReadOnlyList<DeskPlacementInput> DeskPlacementsOrEmpty => DeskPlacements ?? [];
    public IReadOnlyList<RoomPlacementInput> RoomPlacementsOrEmpty => RoomPlacements ?? [];
}

public sealed record QuickDeskInput(string FloorId, string ZoneId, string Code, int X, int Y, bool Popular = false, bool AccessibleOnly = false);

public sealed record QuickRoomInput(string FloorId, string Code, string Name, int Capacity, int X, int Y, int Width, int Height);

public sealed class PlanStyleValidator : AbstractValidator<PlanStyleInput>
{
    public PlanStyleValidator()
    {
        RuleFor(x => x.Fill).MaximumLength(32);
        RuleFor(x => x.Stroke).MaximumLength(32);
        RuleFor(x => x.StrokeWidth).InclusiveBetween(0, 20).When(x => x.StrokeWidth.HasValue);
        RuleFor(x => x.FontSize).InclusiveBetween(8, 48).When(x => x.FontSize.HasValue);
    }
}

public sealed class PlanElementValidator : AbstractValidator<PlanElementInput>
{
    public PlanElementValidator()
    {
        RuleFor(x => x.Id).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Type).Must(PlanElementTypes.IsValid).WithMessage("Desteklenmeyen kroki öğesi türü.");
        RuleFor(x => x.Label).MaximumLength(80);
        RuleFor(x => x.X).InclusiveBetween(-10_000, 10_000);
        RuleFor(x => x.Y).InclusiveBetween(-10_000, 10_000);
        RuleFor(x => x.Width).InclusiveBetween(PlanGeometry.MinSize, 10_000);
        RuleFor(x => x.Height).InclusiveBetween(PlanGeometry.MinSize, 10_000);
        RuleFor(x => x.Rotation).InclusiveBetween(-360, 360);
        RuleFor(x => x.Style!).SetValidator(new PlanStyleValidator()).When(x => x.Style is not null);
        RuleFor(x => x.LayerOrder).InclusiveBetween(0, 10_000);
        RuleFor(x => x.LinkedResourceId).MaximumLength(64);
        RuleFor(x => x.LinkedResourceType)
            .Must(t => t is null || LinkedResourceTypes.IsValid(t))
            .WithMessage("Geçersiz bağlı kaynak türü.");
    }
}

public sealed class DeskPlacementValidator : AbstractValidator<DeskPlacementInput>
{
    public DeskPlacementValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.X).InclusiveBetween(0, 10_000);
        RuleFor(x => x.Y).InclusiveBetween(0, 10_000);
        RuleFor(x => x.Width).InclusiveBetween(20, 1_000);
        RuleFor(x => x.Height).InclusiveBetween(16, 1_000);
        RuleFor(x => x.Rotation).InclusiveBetween(-360, 360);
    }
}

public sealed class RoomPlacementValidator : AbstractValidator<RoomPlacementInput>
{
    public RoomPlacementValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.X).InclusiveBetween(0, 10_000);
        RuleFor(x => x.Y).InclusiveBetween(0, 10_000);
        RuleFor(x => x.Width).InclusiveBetween(40, 10_000);
        RuleFor(x => x.Height).InclusiveBetween(40, 10_000);
    }
}

public sealed class SavePlanValidator : AbstractValidator<SavePlanInput>
{
    public SavePlanValidator()
    {
        RuleFor(x => x.FloorId).NotEmpty().WithMessage("Kat seçilmelidir.");
        RuleFor(x => x.Elements).NotNull()
            .Must(e => e.Count <= PlanGeometry.MaxElements)
            .WithMessage($"Bir katta en fazla {PlanGeometry.MaxElements} kroki öğesi bulunabilir.");
        RuleForEach(x => x.Elements).SetValidator(new PlanElementValidator());
        RuleFor(x => x.DeskPlacementsOrEmpty).Must(d => d.Count <= 500).WithMessage("En fazla 500 masa yerleşimi gönderilebilir.");
        RuleForEach(x => x.DeskPlacementsOrEmpty).SetValidator(new DeskPlacementValidator());
        RuleFor(x => x.RoomPlacementsOrEmpty).Must(r => r.Count <= 200).WithMessage("En fazla 200 oda yerleşimi gönderilebilir.");
        RuleForEach(x => x.RoomPlacementsOrEmpty).SetValidator(new RoomPlacementValidator());
    }
}

public sealed class QuickDeskValidator : AbstractValidator<QuickDeskInput>
{
    public QuickDeskValidator()
    {
        RuleFor(x => x.FloorId).NotEmpty();
        RuleFor(x => x.ZoneId).NotEmpty().WithMessage("Bölge seçilmelidir.");
        RuleFor(x => x.Code).MinimumLength(2).WithMessage("Masa kodu en az 2 karakter olmalıdır.").MaximumLength(20)
            .Matches("^[A-Za-z0-9-]+$").WithMessage("Masa kodu yalnızca harf, rakam ve tire içerebilir.");
        RuleFor(x => x.X).InclusiveBetween(0, 10_000);
        RuleFor(x => x.Y).InclusiveBetween(0, 10_000);
    }
}

public sealed class QuickRoomValidator : AbstractValidator<QuickRoomInput>
{
    public QuickRoomValidator()
    {
        RuleFor(x => x.FloorId).NotEmpty();
        RuleFor(x => x.Code).MinimumLength(2).WithMessage("Oda kodu en az 2 karakter olmalıdır.").MaximumLength(20);
        RuleFor(x => x.Name).MinimumLength(2).WithMessage("Oda adı zorunludur.").MaximumLength(60);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(1).WithMessage("Kapasite en az 1 olmalıdır.").LessThanOrEqualTo(200);
        RuleFor(x => x.X).InclusiveBetween(0, 10_000);
        RuleFor(x => x.Y).InclusiveBetween(0, 10_000);
        RuleFor(x => x.Width).InclusiveBetween(40, 10_000);
        RuleFor(x => x.Height).InclusiveBetween(40, 10_000);
    }
}
