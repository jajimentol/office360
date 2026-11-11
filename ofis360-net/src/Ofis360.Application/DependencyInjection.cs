using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Ofis360.Application.Services;

namespace Ofis360.Application;

public static class DependencyInjection
{
    /// <summary>13 servis + bildirim kanallari. Tumu scoped (istek basina DbContext ile ayni yasam dongusu).</summary>
    public static IServiceCollection AddOfis360Application(this IServiceCollection services)
    {
        services.AddScoped<INotificationChannel, InAppNotificationChannel>();
        services.AddScoped<INotificationChannel, EmailNotificationChannel>();
        services.AddScoped<INotificationChannel, TeamsNotificationChannel>();

        services.AddScoped<AuditService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<RuleService>();
        services.AddScoped<SettingService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<DeskReservationService>();
        services.AddScoped<RoomReservationService>();
        services.AddScoped<SpaceService>();
        services.AddScoped<FloorPlanService>();
        services.AddScoped<ClosureService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<PersonalService>();
        services.AddScoped<UserService>();
        services.AddScoped<UserImportService>();
        services.AddScoped<HolidayService>();
        services.AddScoped<RoomPolicyService>();
        services.TryAddSingleton(Identity.SessionPolicy.Default);
        services.TryAddSingleton<Identity.ISsoTicketValidator, Identity.NotConfiguredSsoTicketValidator>();
        services.TryAddSingleton<IAttendanceFeed, NotConfiguredAttendanceFeed>();
        services.AddScoped<NoShowService>();
        services.AddScoped<AttendanceReportService>();
        services.AddScoped<PresenceService>();
        services.AddScoped<Identity.SessionService>();
        services.AddScoped<Identity.SsoSignInService>();
        return services;
    }
}
