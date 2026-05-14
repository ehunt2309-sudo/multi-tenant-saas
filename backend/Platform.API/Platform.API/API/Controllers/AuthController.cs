using Microsoft.AspNetCore.Mvc;

namespace Platform.API.API.Controllers
{
    public class AuthController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
