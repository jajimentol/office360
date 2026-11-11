using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Persistence;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddOfis360Infrastructure(this IServiceCollection services, string connectionString)
    {
        // CLIENT_IDENTIFIER = istegin kullanicisi (RESERVATION_STATUS_HISTORY aktoru). sp istek scope'udur.
        services.AddDbContext<Ofis360DbContext>((sp, options) => options
            .UseOracle(connectionString, OracleOptions.Configure)
            .AddInterceptors(new ClientIdentifierInterceptor(() => sp.GetService<ICurrentUser>()?.User?.Id)));
        services.AddScoped<IOfis360Db>(sp => sp.GetRequiredService<Ofis360DbContext>());
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
