using Ofis360.Domain.Rules;

namespace Ofis360.Application.Common;

/// <summary>
/// Uygulama genelinde kullanilan hata tipi (kaynak: server/errors.ts AppError).
/// API katmani bu hatayi HTTP yanitina cevirir; servis katmani HTTP'den bagimsiz kalir.
/// </summary>
public sealed class AppException : Exception
{
    public int Status { get; }
    public string Code { get; }
    public IReadOnlyList<RuleViolation>? Violations { get; }
    public object? Details { get; }

    public AppException(int status, string code, string message,
        IReadOnlyList<RuleViolation>? violations = null, object? details = null)
        : base(message)
    {
        Status = status;
        Code = code;
        Violations = violations;
        Details = details;
    }
}

/// <summary>Kaynak: server/errors.ts `errors` fabrikasi. Kodlar ve varsayilan mesajlar aynen.</summary>
public static class AppErrors
{
    public static AppException Unauthorized(string message = "Bu işlem için oturum açmanız gerekiyor.")
        => new(401, "UNAUTHORIZED", message);

    public static AppException Forbidden(string message = "Bu işlem için yetkiniz bulunmuyor.")
        => new(403, "FORBIDDEN", message);

    public static AppException NotFound(string message = "Kayıt bulunamadı.")
        => new(404, "NOT_FOUND", message);

    public static AppException Validation(string message, object? details = null)
        => new(400, "VALIDATION_ERROR", message, details: details);

    public static AppException InvalidJson()
        => new(400, "INVALID_JSON", "İstek gövdesi okunamadı.");

    public static AppException Conflict(string message)
        => new(409, "CONFLICT", message);

    public static AppException RuleViolation(IReadOnlyList<RuleViolation> violations)
        => new(422, "RULE_VIOLATION",
            violations.Count > 0 ? violations[0].Message : "Rezervasyon kuralları nedeniyle işlem tamamlanamadı.",
            violations);

    public const string InternalCode = "INTERNAL_ERROR";
    public const string InternalMessage = "Beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.";
}
