using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Ofis360.Web.Api;

var builder = WebApplication.CreateBuilder(args);

// Portlar: API 5210, eski Angular 4210, bu MVC uygulamasi 5220.
builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.ListenLocalhost(builder.Configuration.GetValue("Ofis360Web:Port", 5220));
});

var apiBaseUrl = builder.Configuration["Ofis360Web:ApiBaseUrl"] ?? "http://localhost:5210/";
if (!apiBaseUrl.EndsWith('/')) apiBaseUrl += "/";

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ApiClient>(c =>
    {
        c.BaseAddress = new Uri(apiBaseUrl);
        c.Timeout = TimeSpan.FromSeconds(30);
    })
    // Oturum cerezi her istekte elle eklenir; paylasilan cerez kabi kullanicilar arasinda karisma riski tasir.
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false });

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "ofis360_web";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/Login";
        o.ExpireTimeSpan = TimeSpan.FromHours(12);
        o.SlidingExpiration = false;
        // AngularJS istekleri yonlendirme yerine 401/403 alir; sayfa giris ekranina kendisi gider.
        o.Events.OnRedirectToLogin = context => RejectAjax(context, StatusCodes.Status401Unauthorized);
        o.Events.OnRedirectToAccessDenied = context => RejectAjax(context, StatusCodes.Status403Forbidden);
    });
builder.Services.AddAuthorization();

// AngularJS $http, XSRF-TOKEN cerezini okuyup X-XSRF-TOKEN basligiyla geri gonderir.
builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-XSRF-TOKEN";
    o.Cookie.Name = "ofis360_af";
    o.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    o.Filters.Add(new AuthorizeFilterAttribute());
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Account/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Sayfa (HTML) isteklerinde AngularJS'in okuyacagi antiforgery cerezini yaz.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) && !IsAjax(context.Request))
    {
        var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions { HttpOnly = false, SameSite = SameSiteMode.Strict, Path = "/" });
    }
    await next();
});

app.MapControllerRoute("default", "{controller=Plan}/{action=Index}/{id?}");

app.Run();

static bool IsAjax(HttpRequest request) =>
    request.Headers.XRequestedWith == "XMLHttpRequest" || request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);

static Task RejectAjax(Microsoft.AspNetCore.Authentication.RedirectContext<CookieAuthenticationOptions> context, int status)
{
    if (IsAjax(context.Request))
    {
        context.Response.StatusCode = status;
        return Task.CompletedTask;
    }
    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

public partial class Program;

/// <summary>Varsayilan olarak tum sayfalar oturum ister; giris ekrani [AllowAnonymous] ile acilir.</summary>
internal sealed class AuthorizeFilterAttribute : Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter
{
    public AuthorizeFilterAttribute() : base(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()) { }
}
