using Microsoft.AspNetCore.Mvc;

namespace Platform.API.API.Controllers
{
    public class BookingController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
