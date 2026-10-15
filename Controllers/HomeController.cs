
using Microsoft.AspNetCore.Mvc;

namespace SmartExpenseManager.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();

        public IActionResult Privacy() => View();

        public IActionResult Dashboard() => View();

        [Route("Home/Error")]
        public IActionResult Error() => View();
    }
}
