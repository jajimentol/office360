namespace Ofis360.Domain.Rules;

/// <summary>Kural ihlali agirligi. BLOCK rezervasyonu engeller, WARN yalnizca bilgilendirir.</summary>
public static class ViolationSeverity
{
    public const string Block = "BLOCK";
    public const string Warn = "WARN";
}

/// <summary>Kaynak: domain/rules/types.ts RuleViolation.</summary>
public sealed record RuleViolation(
    string RuleKey,
    string Code,
    string Title,
    string Message,
    string Severity,
    string? Hint = null);
