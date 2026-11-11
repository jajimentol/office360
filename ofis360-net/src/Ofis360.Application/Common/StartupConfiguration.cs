namespace Ofis360.Application.Common;

public static class AuthModes
{
    public const string Demo = "Demo";
    public const string Handoff = "Handoff";

    public static string Resolve(string? configured, bool isDevelopment) =>
        string.IsNullOrWhiteSpace(configured) ? (isDevelopment ? Demo : Handoff) : configured.Trim();
}

/// <summary>
/// Gelistirme disi ortamlarda eksik veya tehlikeli ayar sessizce gecistirilmez: uygulama hatayi yazip baslamaz.
/// Yanlis yapilandirilmis kurulumun gelistirme veritabanina ya da demo girisine dusmesi bu sekilde engellenir.
/// </summary>
public static class StartupConfiguration
{
    private static readonly string[] DevelopmentSecrets = ["Ofis360Dev_2026", "Ofis360Test_2026", "Ofis360Sys_2026"];

    public static IReadOnlyList<string> Validate(bool isDevelopment, string? connectionString, IReadOnlyList<string> allowedOrigins, string authMode)
    {
        var errors = new List<string>();

        if (authMode is not (AuthModes.Demo or AuthModes.Handoff))
        {
            errors.Add($"Ofis360:Auth:Mode geçersiz: {authMode}. Demo veya Handoff olmalı.");
        }
        if (isDevelopment) return errors;

        if (authMode == AuthModes.Demo) errors.Add("Demo giriş yalnızca Development ortamında açılabilir.");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add("ConnectionStrings:Oracle tanımlı değil.");
        }
        else if (DevelopmentSecrets.Any(secret => connectionString.Contains(secret, StringComparison.Ordinal)))
        {
            errors.Add("ConnectionStrings:Oracle geliştirme parolası içeriyor.");
        }

        foreach (var origin in allowedOrigins)
        {
            var valid = Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !uri.IsLoopback;
            if (!valid) errors.Add($"Ofis360:AllowedOrigins geçersiz (https ve localhost dışı olmalı): {origin}");
        }

        return errors;
    }
}
