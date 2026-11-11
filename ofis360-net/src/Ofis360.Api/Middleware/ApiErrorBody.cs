namespace Ofis360.Api.Middleware;

/// <summary>Kaynak: server/api.ts ApiErrorBody — `{ error: { code, message, violations?, details? } }`.</summary>
public sealed record ApiErrorBody(ApiError Error);

public sealed record ApiError(string Code, string Message, object? Violations = null, object? Details = null);
