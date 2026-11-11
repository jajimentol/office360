using System.Text.Json;
using System.Text.Json.Serialization;
using Ofis360.Api.Controllers;
using Ofis360.Api.Identity;
using Ofis360.Api.Json;
using Ofis360.Api.Middleware;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Ofis360.Application;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Infrastructure;
using Ofis360.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Portlar: API 5210, Angular 4210 (3000/3210/3300/5555 baska uygulamalara ait, dokunulmaz).
builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.ListenLocalhost(builder.Configuration.GetValue("Ofis360:Port", 5210));
});

// Gelistirme disinda eksik ya da tehlikeli ayarla uygulama baslamaz; varsayilana dusulmez.
var isDevelopment = builder.Environment.IsDevelopment();
var connectionString = builder.Configuration.GetConnectionString("Oracle");
var allowedOrigins = builder.Configuration.GetSection("Ofis360:AllowedOrigins").Get<string[]>() ?? [];
var authMode = AuthModes.Resolve(builder.Configuration["Ofis360:Auth:Mode"], isDevelopment);
var configurationErrors = StartupConfiguration.Validate(isDevelopment, connectionString, allowedOrigins, authMode);
if (configurationErrors.Count > 0)
{
    throw new InvalidOperationException("Uygulama yapılandırması geçersiz: " + string.Join(" | ", configurationErrors));
}

builder.Services.AddOfis360Infrastructure(connectionString ?? ConnectionStrings.LocalDev);
builder.Services.AddSingleton(new SessionPolicy(
    TimeSpan.FromMinutes(builder.Configuration.GetValue("Ofis360:Session:IdleMinutes", 120)),
    TimeSpan.FromHours(builder.Configuration.GetValue("Ofis360:Session:AbsoluteHours", 12))));
builder.Services.AddOfis360Application();

// Kimlik: cerezdeki bilet sunucuda dogrulanir; demo ve kurumsal giris ayni oturumu kullanir.
builder.Services.AddSingleton(new AuthSettings(authMode));
if (builder.Configuration.GetValue("Ofis360:Jobs:Enabled", true)) builder.Services.AddHostedService<NoShowWorker>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IIdentityProvider, CookieSessionIdentityProvider>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services
    .AddControllers(o => o.Filters.Add<AppExceptionFilter>())
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(new OptionalJsonConverterFactory());
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // Model baglama hatalari da kaynakla ayni zarfi uretir (INVALID_JSON / VALIDATION_ERROR).
        o.InvalidModelStateResponseFactory = context =>
        {
            var first = context.ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault();
            // Govde cozumleme hatalari "$..." anahtariyla gelir; nitelik dogrulamalari alan adiyla.
            var isJson = context.ModelState.Keys.Any(k => k.Length == 0 || k[0] == '$') || first?.Exception is JsonException;
            var code = isJson ? "INVALID_JSON" : "VALIDATION_ERROR";
            var message = isJson ? "İstek gövdesi okunamadı." : first?.ErrorMessage is { Length: > 0 } m ? m : "Gönderilen veri geçersiz.";
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new ApiErrorBody(new ApiError(code, message)));
        };
    });

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<KnownSessions>();
builder.Services.AddRateLimiter(o =>
{
    static string Partition(HttpContext context) =>
        context.Request.Cookies.TryGetValue(SessionConstants.CookieName, out var token) && !string.IsNullOrEmpty(token)
            && context.RequestServices.GetRequiredService<KnownSessions>().Contains(token)
            ? "s:" + token
            : "ip:" + context.Connection.RemoteIpAddress;

    // Ofis tek IP'den ciktigi icin genel sinir dogrulanmis oturum basinadir. Oturumu dogrulanmamis istekler ayni IP kovasini
    // paylasir; sabah girisinde tum ofis bu kovadan gectigi icin siniri daha genistir.
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = Partition(context);
        var limit = key.StartsWith("ip:", StringComparison.Ordinal) ? 600 : 300;
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions { PermitLimit = limit, Window = TimeSpan.FromMinutes(1) });
    });
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        "ip:" + context.Connection.RemoteIpAddress, _ => new FixedWindowRateLimiterOptions { PermitLimit = 120, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("export", context => RateLimitPartition.GetConcurrencyLimiter(
        Partition(context), _ => new ConcurrencyLimiterOptions { PermitLimit = 2, QueueLimit = 0 }));
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        await context.HttpContext.Response.WriteAsJsonAsync(new ApiErrorBody(new ApiError("RATE_LIMITED", "Çok fazla istek gönderildi. Lütfen biraz sonra yeniden deneyin.")), token);
    };
});

if (!isDevelopment)
{
    builder.Services.AddHttpsRedirection(o => o.HttpsPort = builder.Configuration.GetValue("Ofis360:HttpsPort", 443));
    builder.Services.AddHsts(o => { o.MaxAge = TimeSpan.FromDays(365); o.IncludeSubDomains = false; });
}

var app = builder.Build();

app.UseMiddleware<UnhandledExceptionMiddleware>();
app.UseMiddleware<RequestDeadlineMiddleware>();

if (!isDevelopment)
{
    // TLS ters vekilde sonlanir; ForwardedHeaders varsayilan olarak yalnizca yerel vekile guvenir.
    var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
    foreach (var proxy in builder.Configuration.GetSection("Ofis360:KnownProxies").Get<string[]>() ?? [])
    {
        forwarded.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
    }
    app.UseForwardedHeaders(forwarded);
    app.UseHsts();
    // Saglik yoklamasi ic agdan HTTP ile gelebilir.
    app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api/health"), branch => branch.UseHttpsRedirection());
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.MapControllers();

app.Run();

public partial class Program;
