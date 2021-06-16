using CastServer.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace CastServer.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class RoomsController : ControllerBase
    {
        [HttpPost]
        public List<string> get_rooms() => CastHub.sessions.Keys.ToList();
        [HttpGet]
        public string CheckApi() => "This Api working fine";
    }
}
