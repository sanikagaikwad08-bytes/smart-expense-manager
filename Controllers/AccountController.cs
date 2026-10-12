
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartExpenseManager.Models;
using System.Security.Claims;

namespace SmartExpenseManager.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration config)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _config = config;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.GoogleEnabled =
                !string.IsNullOrWhiteSpace(
                    _config["Authentication:Google:ClientId"])
                && !string.IsNullOrWhiteSpace(
                    _config["Authentication:Google:ClientSecret"]);

            return View();
        }

        [HttpGet]
        public IActionResult Register(string? returnUrl = null)
        {
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        [HttpGet]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var clientId = _config["Authentication:Google:ClientId"];
            var clientSecret = _config["Authentication:Google:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) ||
                string.IsNullOrWhiteSpace(clientSecret))
            {
                return StatusCode(
                    500,
                    "Google sign-in is not configured. Check the app's environment settings.");
            }

            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = null;

            var callbackUrl = Url.Action(
                nameof(GoogleResponse),
                "Account",
                new { returnUrl });

            var properties =
                _signInManager.ConfigureExternalAuthenticationProperties(
                    "Google", callbackUrl);

            return Challenge(properties, "Google");
        }

        [HttpGet]
        public async Task<IActionResult> GoogleResponse(
            string? returnUrl = null)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null || info.LoginProvider != "Google")
            {
                TempData["Error"] =
                    "Google sign-in did not complete. Please try again.";

                return RedirectToAction(nameof(Login));
            }

            var signInResult =
                await _signInManager.ExternalLoginSignInAsync(
                    info.LoginProvider,
                    info.ProviderKey,
                    isPersistent: true);

            if (signInResult.Succeeded)
                return SafeRedirect(returnUrl);

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            var name = info.Principal.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] =
                    "Google did not provide an email address. Please try again.";

                return RedirectToAction(nameof(Login));
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = string.IsNullOrWhiteSpace(name) ? email : name,
                    EmailConfirmed = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user);

                if (!createResult.Succeeded)
                {
                    TempData["Error"] =
                        "Could not create your account. Please try again.";

                    return RedirectToAction(nameof(Login));
                }
            }

            var addLoginResult = await _userManager.AddLoginAsync(user, info);

            if (!addLoginResult.Succeeded)
            {
                TempData["Error"] =
                    "Could not connect your Google account. Please try again.";

                return RedirectToAction(nameof(Login));
            }

            await _signInManager.SignInAsync(user, isPersistent: true);

            return SafeRedirect(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        private IActionResult SafeRedirect(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard");
        }
    }
}
