using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VoiceClient;

namespace CastServer.Controllers
{
    //[Controller]
    //[Route("[controller]/[action]")]
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public IActionResult Index(IFormCollection f)
        {
            var port = int.Parse(f["port"]);
            StreamClient sc = new StreamClient(port,"192.168.1.111");
            sc.Init();
            sc.ConnectToServer();
            sc.mictougle();
            return View();
        }
    }
}
