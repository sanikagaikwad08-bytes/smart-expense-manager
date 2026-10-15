
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

// MVC
builder.Services.AddControllersWithViews();

// PostgreSQL database
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is missing. " +
        "Configure it in appsettings.json or the environment variables.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Login and logout paths
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

// Google OAuth configuration
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

        options.CallbackPath = "/signin-google";

        options.CorrelationCookie.SameSite = SameSiteMode.None;
        options.CorrelationCookie.SecurePolicy =
            CookieSecurePolicy.Always;

        options.AuthorizationEndpoint =
            "https://accounts.google.com/o/oauth2/v2/auth";

        options.TokenEndpoint =
            "https://oauth2.googleapis.com/token";

        options.UserInformationEndpoint =
            "https://openidconnect.googleapis.com/v1/userinfo";

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
                    "Bearer",
                    context.AccessToken);

            using var response = await context.Backchannel.SendAsync(
                request,
                context.HttpContext.RequestAborted);

            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(
                context.HttpContext.RequestAborted);

            using var user = JsonDocument.Parse(responseBody);

            var root = user.RootElement;
            var identity = context.Identity
                ?? throw new InvalidOperationException(
                    "Google did not return an authenticated identity.");

            if (root.TryGetProperty("sub", out var sub) &&
                !string.IsNullOrWhiteSpace(sub.GetString()))
            {
                identity.AddClaim(new Claim(
                    ClaimTypes.NameIdentifier,
                    sub.GetString()!));
            }

            if (root.TryGetProperty("name", out var name) &&
                !string.IsNullOrWhiteSpace(name.GetString()))
            {
                identity.AddClaim(new Claim(
                    ClaimTypes.Name,
                    name.GetString()!));
            }

            if (root.TryGetProperty("email", out var email) &&
                !string.IsNullOrWhiteSpace(email.GetString()))
            {
                identity.AddClaim(new Claim(
                    ClaimTypes.Email,
                    email.GetString()!));
            }
        };
    });
}
else
{
    // The app can run without Google login when credentials
    // are not configured, but Google login will not be available.
    Console.WriteLine(
        "Google OAuth is not configured. Check the Google environment variables.");
}

// Render reverse-proxy / HTTPS support
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Must run before HTTPS redirection and authentication.
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
