using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartExpenseManager.Data;
using SmartExpenseManager.Models;

namespace SmartExpenseManager.Controllers
{
    [Authorize]
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ExpensesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? search, string? category, DateTime? fromDate, DateTime? toDate, string sort = "date_desc")
        {
            var userId = _userManager.GetUserId(User);
            var query = _context.Expenses.Where(e => e.UserId == userId);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(e => e.Description != null && e.Description.Contains(search));

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(e => e.Category == category);

            if (fromDate.HasValue)
                query = query.Where(e => e.ExpenseDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(e => e.ExpenseDate <= toDate.Value);

            query = sort switch
            {
                "date_asc" => query.OrderBy(e => e.ExpenseDate),
                "amount_asc" => query.OrderBy(e => e.Amount),
                "amount_desc" => query.OrderByDescending(e => e.Amount),
                _ => query.OrderByDescending(e => e.ExpenseDate)
            };

            ViewBag.Categories = ExpenseOptions.Categories;
            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Sort = sort;

            return View(await query.ToListAsync());
        }

        public IActionResult Create()
        {
            ViewBag.Categories = ExpenseOptions.Categories;
            ViewBag.PaymentMethods = ExpenseOptions.PaymentMethods;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Expense expense)
        {
            ModelState.Remove(nameof(Expense.UserId));

            if (ModelState.IsValid)
            {
                expense.UserId = _userManager.GetUserId(User)!;
                expense.CreatedAt = DateTime.UtcNow;
                _context.Expenses.Add(expense);

                expense.ExpenseDate = DateTime.SpecifyKind(
                    expense.ExpenseDate,
                    DateTimeKind.Utc);

                await _context.SaveChangesAsync();
                TempData["Success"] = "Expense added.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = ExpenseOptions.Categories;
            ViewBag.PaymentMethods = ExpenseOptions.PaymentMethods;
            return View(expense);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (expense == null) return NotFound();

            ViewBag.Categories = ExpenseOptions.Categories;
            ViewBag.PaymentMethods = ExpenseOptions.PaymentMethods;
            return View(expense);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Expense expense)
        {
            var userId = _userManager.GetUserId(User);
            var existing = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (existing == null) return NotFound();

            ModelState.Remove(nameof(Expense.UserId));

            if (ModelState.IsValid)
            {
                existing.Amount = expense.Amount;
                existing.Category = expense.Category;
                existing.Description = expense.Description;
                existing.ExpenseDate = expense.ExpenseDate;
                existing.PaymentMethod = expense.PaymentMethod;

                await _context.SaveChangesAsync();
                TempData["Success"] = "Expense updated.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categories = ExpenseOptions.Categories;
            ViewBag.PaymentMethods = ExpenseOptions.PaymentMethods;
            return View(existing);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (expense == null) return NotFound();
            return View(expense);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Expense deleted.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}