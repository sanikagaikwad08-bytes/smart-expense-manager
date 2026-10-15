
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartExpenseManager.Models;

namespace SmartExpenseManager.Controllers;

public class ProfileViewModel
{
    [Required(ErrorMessage = "Please enter your name.")]
    [StringLength(100, MinimumLength = 2,
        ErrorMessage = "Name must be between 2 and 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";

    public string? AvatarUrl { get; set; }

    public string Initial { get; set; } = "?";
}

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

    // Display the profile page
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        return View(BuildModel(user));
    }

    // Save the edited name
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateName(ProfileViewModel input)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        var name = string.Join(
            " ",
            (input.FullName ?? "").Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));

        if (name.Any(char.IsControl))
        {
            ModelState.AddModelError(
                nameof(input.FullName),
                "Name contains invalid characters.");
        }

        if (name.Length < 2 || name.Length > 100)
        {
            ModelState.AddModelError(
                nameof(input.FullName),
                "Name must be between 2 and 100 characters.");
        }

        if (!ModelState.IsValid)
        {
            var model = BuildModel(user);
            model.FullName = input.FullName ?? "";

            return View("Index", model);
        }

        user.FullName = name;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            TempData["Error"] =
                "Could not save your name. Please try again.";

            return RedirectToAction(nameof(Index));
        }

        await _signInManager.RefreshSignInAsync(user);

        TempData["Success"] = "Your name has been updated.";

        return RedirectToAction(nameof(Index));
    }

    // Upload and save the profile photo
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
            TempData["Error"] =
                "This photo is too large. Please choose a smaller image.";

            return RedirectToAction(nameof(Index));
        }

        byte[] bytes;

        await using (var stream = new MemoryStream())
        {
            await photo.CopyToAsync(
                stream,
                HttpContext.RequestAborted);

            bytes = stream.ToArray();
        }

        var contentType = DetectImageType(bytes);

        if (contentType is null)
        {
            TempData["Error"] =
                "Please upload a valid JPG, PNG or WebP image.";

            return RedirectToAction(nameof(Index));
        }

        user.ProfileImage = bytes;
        user.ProfileImageContentType = contentType;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            TempData["Error"] =
                "Could not save your photo. Please try again.";

            return RedirectToAction(nameof(Index));
        }

        await _signInManager.RefreshSignInAsync(user);

        TempData["Success"] = "Your profile photo has been updated.";

        return RedirectToAction(nameof(Index));
    }

    // Return the saved photo to the profile page
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

    // Prepare the information shown on the profile page
    private ProfileViewModel BuildModel(ApplicationUser user)
    {
        string? avatarUrl = null;

        if (user.ProfileImage is { Length: > 0 })
        {
            avatarUrl = Url.Action(
                nameof(Photo),
                "Profile",
                new { v = user.ConcurrencyStamp });
        }
        else if (
            !string.IsNullOrWhiteSpace(user.ProfilePictureUrl) &&
            Uri.TryCreate(
                user.ProfilePictureUrl,
                UriKind.Absolute,
                out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps)
        {
            avatarUrl = user.ProfilePictureUrl;
        }

        var displayName = !string.IsNullOrWhiteSpace(user.FullName)
            ? user.FullName!
            : user.Email ?? "";

        return new ProfileViewModel
        {
            FullName = user.FullName ?? "",
            Email = user.Email ?? "",
            AvatarUrl = avatarUrl,
            Initial = displayName.Length > 0
                ? char.ToUpperInvariant(displayName[0]).ToString()
                : "?"
        };
    }

    // Check the actual image file signature
    private static string? DetectImageType(byte[] bytes)
    {
        // JPEG
        if (bytes.Length >= 3 &&
            bytes[0] == 0xFF &&
            bytes[1] == 0xD8 &&
            bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        // PNG
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 &&
            bytes[1] == 0x50 &&
            bytes[2] == 0x4E &&
            bytes[3] == 0x47 &&
            bytes[4] == 0x0D &&
            bytes[5] == 0x0A &&
            bytes[6] == 0x1A &&
            bytes[7] == 0x0A)
        {
            return "image/png";
        }

        // WebP
        if (bytes.Length >= 12 &&
            bytes[0] == 'R' &&
            bytes[1] == 'I' &&
            bytes[2] == 'F' &&
            bytes[3] == 'F' &&
            bytes[8] == 'W' &&
            bytes[9] == 'E' &&
            bytes[10] == 'B' &&
            bytes[11] == 'P')
        {
            return "image/webp";
        }

        return null;
    }
}
