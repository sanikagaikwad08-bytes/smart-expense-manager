using Microsoft.AspNetCore.Mvc;

namespace SmartExpenseManager.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}