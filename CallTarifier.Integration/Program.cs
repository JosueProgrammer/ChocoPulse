using CallTarifier.Integration;
using CallTarifier.Integration.Configuration;
using CallTarifier.Integration.Services.Ami;
using CallTarifier.Integration.Services.Calls;
using CallTarifier.Integration.Services.Cdr;
using CallTarifier.Integration.Services.Firebase;
using CallTarifier.Integration.Services.Invoices;
using CallTarifier.Integration.Services.Tarification;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.Configure<ApplicationOptions>(builder.Configuration.GetSection(ApplicationOptions.SectionName));
builder.Services.Configure<AsteriskOptions>(builder.Configuration.GetSection(AsteriskOptions.SectionName));
builder.Services.Configure<TariffOptions>(builder.Configuration.GetSection(TariffOptions.SectionName));
builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection(FirebaseOptions.SectionName));
builder.Services.Configure<CdrDatabaseOptions>(builder.Configuration.GetSection(CdrDatabaseOptions.SectionName));

builder.Services.AddSingleton<ICdrQueue, CdrQueue>();
builder.Services.AddSingleton<ITarificationService, TarificationService>();
builder.Services.AddSingleton<IAmiListenerService, AmiListenerService>();
builder.Services.AddSingleton<ICdrProcessor, CdrProcessor>();
builder.Services.AddSingleton<ICallProcessor, CallProcessor>();
builder.Services.AddSingleton<IInvoiceService, InvoiceService>();
builder.Services.AddSingleton<IFirebaseService, FirebaseService>();
builder.Services.AddSingleton<ICdrBackfillService, CdrBackfillService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
var secretConfigured = !string.IsNullOrWhiteSpace(builder.Configuration["Asterisk:Secret"]);
logger.LogInformation(
    "AMI target {Host}:{Port} user={User} secretConfigured={SecretConfigured}",
    builder.Configuration["Asterisk:Host"],
    builder.Configuration["Asterisk:Port"],
    builder.Configuration["Asterisk:Username"],
    secretConfigured);

host.Run();
