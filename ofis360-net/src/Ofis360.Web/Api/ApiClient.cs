using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Ofis360.Web.Api;

/// <summary>API'den gelen cevap: durum kodu + ham JSON govdesi. MVC bunu oldugu gibi ya da donusturerek AngularJS'e verir.</summary>
public sealed record ApiResult(HttpStatusCode Status, string Body)
{
    public IReadOnlyList<string> SetCookies { get; init; } = [];

    public bool Ok => (int)Status is >= 200 and < 300;

    public JsonElement Json => string.IsNullOrWhiteSpace(Body) ? default : JsonDocument.Parse(Body).RootElement.Clone();

    public T? As<T>() => string.IsNullOrWhiteSpace(Body) ? default : JsonSerializer.Deserialize<T>(Body, ApiClient.JsonOptions);
}

/// <summary>
/// Ofis360.Api icin tipli HttpClient. Tarayici API'yi hic gormez: kullanicinin API oturum bileti MVC'nin sifreli
/// kimlik cerezinde durur ve her istekte API'ye <c>ofis360_session</c> cerezi olarak iletilir.
/// </summary>
public sealed class ApiClient(HttpClient http, IHttpContextAccessor accessor)
{
    public const string SessionCookieName = "ofis360_session";
    public const string TokenClaim = "ofis360:api_token";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string? Token => accessor.HttpContext?.User.FindFirstValue(TokenClaim);

    public Task<ApiResult> GetAsync(string path, CancellationToken ct) => SendAsync(HttpMethod.Get, path, null, ct);

    public Task<ApiResult> PostAsync(string path, object? body, CancellationToken ct) => SendAsync(HttpMethod.Post, path, body, ct);

    public Task<ApiResult> PutAsync(string path, object? body, CancellationToken ct) => SendAsync(HttpMethod.Put, path, body, ct);

    public async Task<ApiResult> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct, string? tokenOverride = null)
    {
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        var token = tokenOverride ?? Token;
        if (!string.IsNullOrEmpty(token)) request.Headers.Add("Cookie", $"{SessionCookieName}={token}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json");
        }
        else if (method != HttpMethod.Get)
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        return new ApiResult(response.StatusCode, text) { SetCookies = response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies.ToList() : [] };
    }

    /// <summary>API'nin Set-Cookie basliklarindan oturum biletini cikarir (demo girisi sonrasi).</summary>
    public static string? ExtractSessionToken(IEnumerable<string> setCookies)
    {
        foreach (var header in setCookies)
        {
            var first = header.Split(';', 2)[0];
            var parts = first.Split('=', 2);
            if (parts.Length == 2 && parts[0].Trim() == SessionCookieName && parts[1].Length > 0) return Uri.UnescapeDataString(parts[1]);
        }
        return null;
    }
}
