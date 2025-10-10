using Microsoft.AspNetCore.Mvc;

namespace MO.Controllers
{
    public class JuegoController : Controller
    {
        public IActionResult Iniciar()
        {
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username))
                return RedirectToAction("Login", "Usuario");

            ViewBag.Username = username;
            return View();
        }
    }
}
