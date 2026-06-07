using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace BunnyChat.Frontend.Controllers;

public class PageController : Controller
{
    [HttpGet]
        public IActionResult Index()
    {
        return View("~/Frontend/Views/Auth/Auth.cshtml");
    }

    [HttpGet("/Forgot")]
    public IActionResult Forgot()
    {
        return View("~/Frontend/Views/Auth/Forgot.cshtml");
    }
    
    [HttpGet("/Chat")]
    public IActionResult Chat()
    {
        return View("~/Frontend/Views/Chat/Index.cshtml");
    }
    [HttpGet("/Information")]
    public IActionResult Information()
    {
        return View("~/Frontend/Views/Information/Information.cshtml");
    }
}