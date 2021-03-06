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
        public List<string> GetRooms() => CastHub.sessions.Keys.ToList();
        [HttpPost]
        public bool CreateRoom(room room) => CastHub.CreateRoom(room.room_name);
        [HttpPost]
        public void DeleteRoom(room room) => CastHub.DeleteRoom(room.room_name);
        [HttpGet]
        public string CheckApi() => "This Api working fine";
    }
}
