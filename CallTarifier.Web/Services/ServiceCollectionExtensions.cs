using CallTarifier.Web.Configuration;
using CallTarifier.Web.Services.Mock;
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
        services.AddSingleton<MockDataStore>(_ => MockDataStore.Instance);
        services.AddSingleton<ITarificationService, TarificationService>();
        services.AddSingleton<FirebaseAdminBootstrap>();

        var appOptions = configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>()
            ?? new ApplicationOptions();
        var firebaseOptions = configuration.GetSection(FirebaseOptions.SectionName).Get<FirebaseOptions>()
            ?? new FirebaseOptions();

        var useFirebaseAuth = appOptions.UseFirebaseAuth && firebaseOptions.IsWebConfigured;
        if (useFirebaseAuth)
        {
            services.AddScoped<IAuthService, FirebaseAuthService>();
        }
        else
        {
            services.AddScoped<IAuthService, MockAuthService>();
        }

        // Data services remain mock until later phases.
        services.AddScoped<ICallService, MockCallService>();
        services.AddScoped<IInvoiceService, MockInvoiceService>();
        services.AddScoped<IExtensionService, MockExtensionService>();
        services.AddScoped<IStatisticsService, MockStatisticsService>();
        services.AddScoped<IFirebaseService, MockFirebaseService>();

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
