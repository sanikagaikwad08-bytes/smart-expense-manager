using Microsoft.AspNetCore.Identity;

namespace SmartExpenseManager.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }

    public int? Age { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public byte[]? ProfileImage { get; set; }

    public string? ProfileImageContentType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}