
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

        private bool ProviderEnabled(string provider)
        {
            return !string.IsNullOrWhiteSpace(
                       _config[$"Authentication:{provider}:ClientId"])
                   && !string.IsNullOrWhiteSpace(
                       _config[$"Authentication:{provider}:ClientSecret"]);
        }

        // ---------------- REGISTER ----------------

        [HttpGet]
        public IActionResult Register()
        {
            SetProviderFlags();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string username,
            string password,
            string confirmPassword)
        {
            SetProviderFlags();

            if (string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(
                    "", "Please enter a username and password.");
                return View();
            }

            if (password != confirmPassword)
            {
                ModelState.AddModelError(
                    "", "Passwords do not match.");
                return View();
            }

            if (await _userManager.FindByNameAsync(username) != null)
            {
                ModelState.AddModelError(
                    "", "This username is already taken.");
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = username,
                FullName = username,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: true);
                return RedirectToAction("Index", "Dashboard");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View();
        }

        // ---------------- LOGIN ----------------

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            SetProviderFlags();
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            string username,
            string password,
            bool rememberMe = false,
            string? returnUrl = null)
        {
            SetProviderFlags();
            ViewBag.ReturnUrl = returnUrl;

            var user = await _userManager.FindByNameAsync(username);

            if (user == null)
            {
                ModelState.AddModelError(
                    "", "Invalid username or password.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user, password, rememberMe, lockoutOnFailure: false);

            if (result.Succeeded)
                return SafeRedirect(returnUrl);

            ModelState.AddModelError(
                "", "Invalid username or password.");

            return View();
        }

        // ---------------- LOGOUT ----------------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // ---------------- GOOGLE ----------------

        [HttpGet]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            return StartExternalLogin("Google", returnUrl);
        }

        // ---------------- GITHUB ----------------

        [HttpGet]
        public IActionResult GitHubLogin(string? returnUrl = null)
        {
            return StartExternalLogin("GitHub", returnUrl);
        }

        // Start the provider's OAuth flow.
        private IActionResult StartExternalLogin(
            string provider,
            string? returnUrl)
        {
            if (!ProviderEnabled(provider))
            {
                return Content(
                    $"{provider} sign-in is not configured. Check your authentication settings.");
            }

            if (!Url.IsLocalUrl(returnUrl))
                returnUrl = null;

            var callbackUrl = Url.Action(
                provider + "Response",
                "Account",
                new { returnUrl });

            var properties =
                _signInManager.ConfigureExternalAuthenticationProperties(
                    provider, callbackUrl);

            return Challenge(properties, provider);
        }

        // Both providers return here after OAuth completes.
        [HttpGet]
        public Task<IActionResult> GoogleResponse(string? returnUrl = null)
        {
            return CompleteExternalLogin(returnUrl);
        }

        [HttpGet]
        public Task<IActionResult> GitHubResponse(string? returnUrl = null)
        {
            return CompleteExternalLogin(returnUrl);
        }

        private async Task<IActionResult> CompleteExternalLogin(
            string? returnUrl)
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();

            if (info == null)
            {
                TempData["Error"] =
                    "External sign-in failed. Please try again.";
                return RedirectToAction("Login");
            }

            // Sign in if this provider is already linked to the user.
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
                    "The provider did not return an email address. Verify your email settings and try again.";
                return RedirectToAction("Login");
            }

            var user = await _userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = string.IsNullOrWhiteSpace(name)
                        ? email
                        : name,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await _userManager.CreateAsync(user);

                if (!createResult.Succeeded)
                {
                    TempData["Error"] =
                        "Could not create your account: " +
                        string.Join(" ", createResult.Errors.Select(
                            e => e.Description));

                    return RedirectToAction("Login");
                }
            }

            // Link the external provider to the account.
            var addLoginResult = await _userManager.AddLoginAsync(user, info);

            if (!addLoginResult.Succeeded)
            {
                TempData["Error"] =
                    "Could not link this external account. Try signing in with the original provider.";

                return RedirectToAction("Login");
            }

            await _signInManager.SignInAsync(user, isPersistent: true);

            return SafeRedirect(returnUrl);
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

        private void SetProviderFlags()
        {
            ViewBag.GoogleEnabled = ProviderEnabled("Google");
            ViewBag.GitHubEnabled = ProviderEnabled("GitHub");
        }
    }
}
