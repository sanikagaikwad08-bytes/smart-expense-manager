using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartExpenseManager.Models;

namespace SmartExpenseManager.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private const long MaxPhotoBytes = 2 * 1024 * 1024;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ApplicationUser input)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        var username = (input.UserName ?? "").Trim();
        var email = (input.Email ?? "").Trim();
        var fullName = (input.FullName ?? "").Trim();

        if (username.Length < 3 || username.Length > 50)
            ModelState.AddModelError(nameof(input.UserName),
                "Username must be between 3 and 50 characters.");

        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                .IsValid(email))
            ModelState.AddModelError(nameof(input.Email),
                "Enter a valid email address.");

        if (fullName.Length < 2 || fullName.Length > 100)
            ModelState.AddModelError(nameof(input.FullName),
                "Full name must be between 2 and 100 characters.");

        if (input.Age is < 1 or > 120)
            ModelState.AddModelError(nameof(input.Age),
                "Age must be between 1 and 120.");

        if (!ModelState.IsValid)
        {
            input.ProfileImage = user.ProfileImage;
            input.ProfileImageContentType = user.ProfileImageContentType;
            input.ProfilePictureUrl = user.ProfilePictureUrl;
            input.CreatedAt = user.CreatedAt;
            return View(input);
        }

        var existingUsername = await _userManager.FindByNameAsync(username);

        if (existingUsername is not null && existingUsername.Id != user.Id)
        {
            ModelState.AddModelError(nameof(input.UserName),
                "That username is already taken.");
            return View(input);
        }

        var existingEmail = await _userManager.FindByEmailAsync(email);

        if (existingEmail is not null && existingEmail.Id != user.Id)
        {
            ModelState.AddModelError(nameof(input.Email),
                "That email address is already in use.");
            return View(input);
        }

        var usernameResult = await _userManager.SetUserNameAsync(user, username);

        if (!usernameResult.Succeeded)
        {
            AddIdentityErrors(usernameResult);
            return View(input);
        }

        var emailResult = await _userManager.SetEmailAsync(user, email);

        if (!emailResult.Succeeded)
        {
            AddIdentityErrors(emailResult);
            return View(input);
        }

        user.FullName = fullName;
        user.Age = input.Age;

        var updateResult = await _userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            AddIdentityErrors(updateResult);
            return View(input);
        }

        await _signInManager.RefreshSignInAsync(user);

        TempData["Success"] = "Profile updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(IFormFile? photo)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        if (photo is null || photo.Length == 0)
        {
            TempData["Error"] = "Please choose a photo first.";
            return RedirectToAction(nameof(Index));
        }

        if (photo.Length > MaxPhotoBytes)
        {
            TempData["Error"] = "Photo must be smaller than 2 MB.";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = new MemoryStream();
        await photo.CopyToAsync(stream, HttpContext.RequestAborted);

        var bytes = stream.ToArray();
        var contentType = DetectImageType(bytes);

        if (contentType is null)
        {
            TempData["Error"] = "Upload a valid JPG, PNG, or WebP image.";
            return RedirectToAction(nameof(Index));
        }

        user.ProfileImage = bytes;
        user.ProfileImageContentType = contentType;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            TempData["Error"] = "Could not save your photo.";
            return RedirectToAction(nameof(Index));
        }

        await _signInManager.RefreshSignInAsync(user);

        TempData["Success"] = "Profile photo updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Photo()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user?.ProfileImage is not { Length: > 0 } image ||
            string.IsNullOrWhiteSpace(user.ProfileImageContentType))
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        return File(image, user.ProfileImageContentType);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError("", error.Description);
    }

    private static string? DetectImageType(byte[] bytes)
    {
        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "image/jpeg";

        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 &&
            bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A &&
            bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "image/png";

        if (bytes.Length >= 12 &&
            bytes[0] == 'R' && bytes[1] == 'I' &&
            bytes[2] == 'F' && bytes[3] == 'F' &&
            bytes[8] == 'W' && bytes[9] == 'E' &&
            bytes[10] == 'B' && bytes[11] == 'P')
            return "image/webp";

        return null;
    }
}