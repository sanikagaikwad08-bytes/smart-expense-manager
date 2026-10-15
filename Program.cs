
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartExpenseManager.Data;
using SmartExpenseManager.Models;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

var googleClientId =
    builder.Configuration["Authentication:Google:ClientId"];

var googleClientSecret =
    builder.Configuration["Authentication:Google:ClientSecret"];

var authentication = builder.Services.AddAuthentication();

if (!string.IsNullOrWhiteSpace(googleClientId) &&
    !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddOAuth("Google", options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;

        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy =
            CookieSecurePolicy.Always;

        options.AuthorizationEndpoint =
            "https://accounts.google.com/o/oauth2/v2/auth";

        options.TokenEndpoint =
            "https://oauth2.googleapis.com/token";

        options.UserInformationEndpoint =
            "https://openidconnect.googleapis.com/v1/userinfo";

        options.CallbackPath = "/signin-google";

        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");

        options.Events.OnCreatingTicket = async context =>
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                context.Options.UserInformationEndpoint);

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", context.AccessToken);

            using var response = await context.Backchannel.SendAsync(
                request,
                context.HttpContext.RequestAborted);

            response.EnsureSuccessStatusCode();

            using var user = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(
                    context.HttpContext.RequestAborted));

            var root = user.RootElement;

            if (root.TryGetProperty("sub", out var sub))
                context.Identity!.AddClaim(
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        sub.GetString()!));

            if (root.TryGetProperty("name", out var name))
                context.Identity!.AddClaim(
                    new Claim(
                        ClaimTypes.Name,
                        name.GetString()!));

            if (root.TryGetProperty("email", out var email))
                context.Identity!.AddClaim(
                    new Claim(
                        ClaimTypes.Email,
                        email.GetString()!));
        };
    });
}

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(
                "Something went wrong. Please try again.");
        });
    });

    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
