using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebMoi.Frontend.Controllers;

public class PageController : Controller
{
    [HttpGet]
        public IActionResult Index()
    {
        return View("~/Frontend/Views/Auth/Auth.cshtml");
    }

    
    [HttpGet("/Chat")]
    public IActionResult Chat()
    {
        return View("~/Frontend/Views/Chat/Index.cshtml");
    }
}