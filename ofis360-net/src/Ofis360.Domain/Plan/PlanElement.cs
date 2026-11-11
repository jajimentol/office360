namespace Ofis360.Domain.Plan;

/// <summary>Kaynak: src/lib/plan.ts PlanElement — kroki ogesinin API/UI gorunumu.</summary>
public sealed record PlanElement(
    string Id,
    string Type,
    string? Label,
    int X,
    int Y,
    int Width,
    int Height,
    int Rotation,
    PlanElementStyle Style,
    int LayerOrder,
    bool Locked,
    bool Visible,
    string? LinkedResourceId,
    string? LinkedResourceType);
