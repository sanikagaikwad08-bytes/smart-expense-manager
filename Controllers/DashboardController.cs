using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartExpenseManager.Data;
using SmartExpenseManager.Models;

namespace SmartExpenseManager.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var expenses = await _context.Expenses
                .Where(e => e.UserId == userId)
                .ToListAsync();

            ViewBag.TotalSpent = expenses.Sum(e => e.Amount);

            ViewBag.ThisMonthSpent = expenses
                .Where(e => e.ExpenseDate.Month == DateTime.Today.Month &&
                            e.ExpenseDate.Year == DateTime.Today.Year)
                .Sum(e => e.Amount);

            ViewBag.TransactionCount = expenses.Count;

            ViewBag.TopCategory = expenses
                .GroupBy(e => e.Category)
                .OrderByDescending(g => g.Sum(e => e.Amount))
                .Select(g => g.Key)
                .FirstOrDefault() ?? "None";

            ViewBag.RecentExpenses = expenses
                .OrderByDescending(e => e.ExpenseDate)
                .Take(5)
                .ToList();

            return View();
        }
    }
}