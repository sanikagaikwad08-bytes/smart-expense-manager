using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartExpenseManager.Data;
using SmartExpenseManager.Models;

namespace SmartExpenseManager.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            var expenses = await _context.Expenses
                .Where(e => e.UserId == user.Id)
                .ToListAsync();

            var logins = await _userManager.GetLoginsAsync(user);

            ViewBag.FullName = user.FullName ?? user.Email ?? "User";
            ViewBag.Email = user.Email ?? "";
            ViewBag.ProfilePictureUrl = user.ProfilePictureUrl;
            ViewBag.Provider = logins.Any()
                ? logins.First().LoginProvider
                : "Email/Password";

            ViewBag.TotalExpenses = expenses.Sum(e => e.Amount);
            ViewBag.TransactionCount = expenses.Count;
            ViewBag.CreatedAt = user.CreatedAt;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateName(string fullName)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return RedirectToAction("Login", "Account");

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                user.FullName = fullName.Trim();

                await _userManager.UpdateAsync(user);

                TempData["Success"] = "Profile updated.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}