using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CastServer
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            ViewBag.rooms = Hubs.CastHub.GroupSet;
            return View();
        }
    }
}
