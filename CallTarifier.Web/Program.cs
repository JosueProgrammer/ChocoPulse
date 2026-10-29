using System.Security.Claims;
using CallTarifier.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services.AddCallTarifierServices(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var choosing = path.StartsWith("/account/choose-extension", StringComparison.OrdinalIgnoreCase);
        var bacAccess = path.StartsWith("/account/acceso", StringComparison.OrdinalIgnoreCase);
        var signingOut = path.Equals("/logout", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/logout/", StringComparison.OrdinalIgnoreCase);
        var api = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
        var email = user.FindFirst(ClaimTypes.Email)?.Value;
        var extensionClaim = user.FindFirst("extension")?.Value;
        if (!signingOut
            && E1Owner.IsOwner(email)
            && !string.Equals(extensionClaim, E1Owner.Extension, StringComparison.OrdinalIgnoreCase))
        {
            var auth = context.RequestServices.GetRequiredService<IAuthService>();
            var updated = await auth.ChooseExtensionAsync(E1Owner.Extension, E1Owner.Branch, context.RequestAborted);
            if (updated is not null && !api)
            {
                context.Response.Redirect(context.Request.Path + context.Request.QueryString);
                return;
            }
        }

        var passedGate = context.Request.Cookies.ContainsKey("CallTarifier.Bac");
        if (!passedGate && !bacAccess && !signingOut && !api)
        {
            context.Response.Redirect("/account/acceso");
            return;
        }

        var chosen = string.Equals(user.FindFirst("extensionChosen")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        if (!chosen && !choosing && !signingOut && !api && !bacAccess)
        {
            context.Response.Redirect("/account/choose-extension");
            return;
        }
    }

    await next();
});
app.MapRazorPages();

app.Logger.LogInformation(
    "CallTarifier.Web started. FirebaseAuth=true ProjectId={ProjectId}",
    builder.Configuration["Firebase:ProjectId"]);

app.Run();
