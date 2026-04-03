using Microsoft.AspNetCore.Mvc;

namespace Recipes.Controllers
{
    public class DeleteAccountController : Controller
    {
        [HttpGet("/DeleteAccount")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
