using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CastServer.Hubs
{
    public class CastHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine(Context.ConnectionId + " Connected");
            await base.OnConnectedAsync();
        }
        public override async Task OnDisconnectedAsync(Exception e)
        {
            Console.WriteLine(Context.ConnectionId + " disonnected");
            await base.OnDisconnectedAsync(e);
        }
        public string ok()
        {
            Console.WriteLine("OK");
            return "OK";
        }
        public async void UpdateScreen(string base64 , int r,int c)
        {
            Console.WriteLine("received " + r + " " + c);
            await Clients.All.SendAsync("UpdateScreen",base64,r,c);
        }
    }
}
