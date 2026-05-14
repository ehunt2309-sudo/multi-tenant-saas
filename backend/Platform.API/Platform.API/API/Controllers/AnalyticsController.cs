using Microsoft.AspNetCore.Mvc;

namespace Platform.API.API.Controllers
{
    public class AnalyticsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
