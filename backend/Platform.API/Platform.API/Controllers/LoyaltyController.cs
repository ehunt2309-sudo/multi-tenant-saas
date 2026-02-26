using Microsoft.AspNetCore.Mvc;

namespace Platform.API.Controllers
{
    public class LoyaltyController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
