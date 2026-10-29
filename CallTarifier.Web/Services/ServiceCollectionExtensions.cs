using CallTarifier.Web.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CallTarifier.Web.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCallTarifierServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ApplicationOptions>(configuration.GetSection(ApplicationOptions.SectionName));
        services.Configure<TariffOptions>(configuration.GetSection(TariffOptions.SectionName));
        services.Configure<WebRtcOptions>(configuration.GetSection(WebRtcOptions.SectionName));
        services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.SectionName));
        services.Configure<AsteriskOptions>(configuration.GetSection(AsteriskOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<ITarificationService, TarificationService>();
        services.AddSingleton<FirebaseAdminBootstrap>();
        services.AddSingleton<IFirestoreDbFactory, FirestoreDbFactory>();

        services.AddScoped<IAuthService, FirebaseAuthService>();
        services.AddScoped<ICallService, FirestoreCallService>();
        services.AddScoped<IInvoiceService, FirestoreInvoiceService>();
        services.AddSingleton<IssabelExtensionRefresher>();
        services.AddScoped<IExtensionService, FirestoreExtensionService>();
        services.AddHostedService<ExtensionCatalogSync>();
        services.AddScoped<IStatisticsService, FirestoreStatisticsService>();
        services.AddScoped<IFirebaseService, FirestoreFirebaseService>();
        services.AddSingleton<AmiSipSecretReader>();
        services.AddSingleton<AmiCallService>();
        services.AddSingleton<PlacedCallRecorder>();
        services.AddScoped<IWebRtcSessionService, WebRtcSessionService>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.AccessDeniedPath = "/login";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.Cookie.Name = "CallTarifier.Auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

        services.AddAuthorization();

        return services;
    }
}
