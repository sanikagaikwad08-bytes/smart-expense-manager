using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartExpenseManager.Models; // change if your ApplicationUser namespace differs

namespace SmartExpenseManager.Controllers;

public class ProfileViewModel
{
    [Required(ErrorMessage = "Please enter your name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Name must be 2 to 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    public string Email { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string Initial { get; set; } = "?";
}

[Authorize]
public class ProfileController : Controller
{
    private const long MaxPhotoBytes = 2 * 1024 * 1024; // 2 MB

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public ProfileController(UserManager<ApplicationUser> userManager,
                             SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        return View(BuildModel(user));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateName(ProfileViewModel input)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var name = string.Join(' ',
            (input.FullName ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (name.Any(char.IsControl))
            ModelState.AddModelError(nameof(input.FullName), "Name contains invalid characters.");
        if (name.Length < 2 || name.Length > 100)
            ModelState.AddModelError(nameof(input.FullName), "Name must be 2 to 100 characters.");

        if (!ModelState.IsValid)
        {
            var vm = BuildModel(user);
            vm.FullName = input.FullName ?? "";
            return View("Index", vm);
        }

        user.FullName = name;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = "Could not save your name. Please try again.";
            return RedirectToAction(nameof(Index));
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Your name has been updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(IFormFile? photo)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (photo is null || photo.Length == 0)
        {
            TempData["Error"] = "Please choose a photo first.";
            return RedirectToAction(nameof(Index));
        }
        if (photo.Length > MaxPhotoBytes)
        {
            TempData["Error"] = "Photo is too large. Maximum size is 2 MB.";
            return RedirectToAction(nameof(Index));
        }

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await photo.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        var contentType = DetectImageType(bytes);
        if (contentType is null)
        {
            TempData["Error"] = "Only real JPG, PNG or WebP images are allowed.";
            return RedirectToAction(nameof(Index));
        }

        if (!TrySetProfileImage(user, bytes, contentType))
        {
            TempData["Error"] = "Profile photo storage is not configured.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            await _signInManager.RefreshSignInAsync(user);
            TempData["Success"] = "Your photo has been updated.";
        }
        else
        {
            TempData["Error"] = "Could not save your photo. Please try again.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Photo()
    {
        var user = await _userManager.GetUserAsync(User);
        var image = user is null ? null : GetProfileImage(user);
        var contentType = user is null ? null : GetProfileImageContentType(user);
        if (image is null || contentType is null)
            return NotFound();

        Response.Headers["Cache-Control"] = "private, max-age=3600";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(image, contentType);
    }

    private ProfileViewModel BuildModel(ApplicationUser user)
    {
        string? avatar = null;
        if (GetProfileImage(user) is { Length: > 0 })
            avatar = Url.Action(nameof(Photo), "Profile", new { v = user.ConcurrencyStamp });
        else if (!string.IsNullOrWhiteSpace(user.ProfilePictureUrl) &&
                 user.ProfilePictureUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            avatar = user.ProfilePictureUrl;

        var display = !string.IsNullOrWhiteSpace(user.FullName) ? user.FullName! : (user.Email ?? "");
        return new ProfileViewModel
        {
            FullName = user.FullName ?? "",
            Email = user.Email ?? "",
            AvatarUrl = avatar,
            Initial = display.Length > 0 ? char.ToUpperInvariant(display[0]).ToString() : "?"
        };
    }

    // Keep this controller compatible with ApplicationUser models that do not
    // define image-storage properties.
    private static byte[]? GetProfileImage(ApplicationUser user) =>
        typeof(ApplicationUser).GetProperty("ProfileImage")?.GetValue(user) as byte[];

    private static string? GetProfileImageContentType(ApplicationUser user) =>
        typeof(ApplicationUser).GetProperty("ProfileImageContentType")?.GetValue(user) as string;

    private static bool TrySetProfileImage(ApplicationUser user, byte[] image, string contentType)
    {
        var imageProperty = typeof(ApplicationUser).GetProperty("ProfileImage");
        var contentTypeProperty = typeof(ApplicationUser).GetProperty("ProfileImageContentType");
        if (imageProperty?.CanWrite != true || contentTypeProperty?.CanWrite != true)
            return false;

        imageProperty.SetValue(user, image);
        contentTypeProperty.SetValue(user, contentType);
        return true;
    }

    // Checks the real file bytes, not the file name or the browser's content type.
    private static string? DetectImageType(byte[] b)
    {
        if (b.Length > 12 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length > 12 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' &&
            b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        return null;
    }
}