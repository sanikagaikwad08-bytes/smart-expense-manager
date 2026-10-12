
using Microsoft.AspNetCore.Authentication;
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
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
});

// OAuth credentials
var googleClientId =
    builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret =
    builder.Configuration["Authentication:Google:ClientSecret"];

var githubClientId =
    builder.Configuration["Authentication:GitHub:ClientId"];
var githubClientSecret =
    builder.Configuration["Authentication:GitHub:ClientSecret"];

// Register external authentication providers
var authentication = builder.Services.AddAuthentication();

if (!string.IsNullOrWhiteSpace(googleClientId) &&
    !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddOAuth("Google", options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;

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

        options.ClaimActions.MapJsonKey(
            ClaimTypes.NameIdentifier, "sub");
        options.ClaimActions.MapJsonKey(
            ClaimTypes.Name, "name");
        options.ClaimActions.MapJsonKey(
            ClaimTypes.Email, "email");

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

            context.RunClaimActions(user.RootElement);
        };
    });
}

if (!string.IsNullOrWhiteSpace(githubClientId) &&
    !string.IsNullOrWhiteSpace(githubClientSecret))
{
    authentication.AddOAuth("GitHub", options =>
    {
        options.ClientId = githubClientId;
        options.ClientSecret = githubClientSecret;
        options.SignInScheme = IdentityConstants.ExternalScheme;

        options.AuthorizationEndpoint =
            "https://github.com/login/oauth/authorize";
        options.TokenEndpoint =
            "https://github.com/login/oauth/access_token";
        options.UserInformationEndpoint =
            "https://api.github.com/user";

        options.CallbackPath = "/signin-github";

        options.Scope.Add("read:user");
        options.Scope.Add("user:email");

        options.ClaimActions.MapJsonKey(
            ClaimTypes.NameIdentifier, "id");
        options.ClaimActions.MapJsonKey(
            ClaimTypes.Name, "login");
        options.ClaimActions.MapJsonKey(
            ClaimTypes.Email, "email");

        options.Events.OnCreatingTicket = async context =>
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                context.Options.UserInformationEndpoint);

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer", context.AccessToken);
            request.Headers.UserAgent.ParseAdd(
                "SmartExpenseManager");

            using var response = await context.Backchannel.SendAsync(
                request,
                context.HttpContext.RequestAborted);

            response.EnsureSuccessStatusCode();

            using var user = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(
                    context.HttpContext.RequestAborted));

            context.RunClaimActions(user.RootElement);

            // GitHub may keep the user's email private.
            var hasEmail = context.Identity?.HasClaim(
                c => c.Type == ClaimTypes.Email) == true;

            if (!hasEmail)
            {
                using var emailRequest = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://api.github.com/user/emails");

                emailRequest.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer", context.AccessToken);
                emailRequest.Headers.UserAgent.ParseAdd(
                    "SmartExpenseManager");

                using var emailResponse =
                    await context.Backchannel.SendAsync(
                        emailRequest,
                        context.HttpContext.RequestAborted);

                emailResponse.EnsureSuccessStatusCode();

                using var emails = JsonDocument.Parse(
                    await emailResponse.Content.ReadAsStringAsync(
                        context.HttpContext.RequestAborted));

                foreach (var item in emails.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("primary", out var primary) &&
                        primary.GetBoolean() &&
                        item.TryGetProperty("verified", out var verified) &&
                        verified.GetBoolean() &&
                        item.TryGetProperty("email", out var email))
                    {
                        var emailValue = email.GetString();

                        if (!string.IsNullOrWhiteSpace(emailValue))
                        {
                            context.Identity?.AddClaim(
                                new Claim(
                                    ClaimTypes.Email,
                                    emailValue));
                        }

                        break;
                    }
                }
            }
        };
    });
}

// Render / reverse-proxy headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

// Must run before HTTPS redirection
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
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
