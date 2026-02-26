using Microsoft.AspNetCore.Mvc;

namespace Platform.API.Controllers
{
    public class ScheduleController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
