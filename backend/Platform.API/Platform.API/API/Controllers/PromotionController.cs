using Microsoft.AspNetCore.Mvc;

namespace Platform.API.API.Controllers
{
    public class PromotionController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
