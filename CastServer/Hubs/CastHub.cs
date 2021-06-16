﻿using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using TCPStreamer;

namespace CastServer.Hubs
{
    public struct Part
    {
        public string ms;
        public int width;
        public int height;
        public bool encrypted;
    }
    public struct Message
    {
        public string sender;
        public string message;
        public Message(string sender, string message) { this.sender = sender; this.message = message; }
    }
    public class CastHub : Hub
    {
        static Dictionary<string, Part[,]> LastScreen = new Dictionary<string, Part[,]>();
        static Dictionary<string, string> groupof = new Dictionary<string, string>();
        static Dictionary<string, List<Message>> messages = new Dictionary<string, List<Message>>();
        static Dictionary<string, StreamServer> servers = new Dictionary<string, StreamServer>();
        static Dictionary<string, int> ports = new Dictionary<string, int>();
        static Dictionary<string, string> names = new Dictionary<string, string>();
        public static Dictionary<string, string> sessions = new Dictionary<string, string>();
        string group;

        static HashSet<string> RoomSet = new HashSet<string>();
        static HashSet<int> PortSet = new HashSet<int>();
        static HashSet<string> GroupSet = new HashSet<string>();
        static Random random = new Random();
        static string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz0123456789";

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine(Context.ConnectionId + " Connected");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception e)
        {
            string id = Context.ConnectionId;
            Console.WriteLine(id + " disonnected");
            try
            {
                names.Remove(Context.ConnectionId);
                await RemoveFromGroup(id);
            }
            catch { }
            await base.OnDisconnectedAsync(e);
        }
        public void SetName(string name) => names.Add(Context.ConnectionId, name);
        public string GetGroupId(string room_name) => sessions[room_name];
        public int getport(string groupname) => ports[groupname];

        public async Task getMessages()
        {
            group = groupof[Context.ConnectionId];
            foreach (Message m in messages[group])
                await Clients.Client(Context.ConnectionId).SendAsync("newMessage", m.sender, m.message);
        }
        public async Task newMessage(string message)
        {
            group = groupof[Context.ConnectionId];
            string name = names[Context.ConnectionId];
            Message m = new Message(name, message);
            messages[group].Add(m);
            await Clients.Group(group).SendAsync("newMessage", name, message);
        }
        public static bool PortInUse(int port)
        {
            bool inUse = false;

            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();
            foreach (IPEndPoint endPoint in ipEndPoints)
                if (endPoint.Port == port)
                {
                    inUse = true;
                    break;
                }
            return inUse;
        }
        public void DeleteRoom(string room_name)
        {
            RoomSet.Remove(room_name);
            var group = sessions[room_name];
            sessions.Remove(room_name);
            LastScreen.Remove(group);
            messages.Remove(group);
            servers[group].ConnectToServer();
            servers[group].disconnect();
            servers.Remove(group);
            GroupSet.Remove(group);
            PortSet.Remove(ports[group]);
            ports.Remove(group);
        }
        public bool CreateRoom(string room_name)
        {
            string groupName;
            int newport;
            if (RoomSet.Contains(room_name)) return false;
            RoomSet.Add(room_name);
            while (true)
            {
                groupName = RandomString(12);
                if (!GroupSet.Contains(groupName)) break;
            }
            sessions[room_name] = groupName;
            Console.WriteLine(groupName);
            GroupSet.Add(groupName);
            while (true)
            {
                newport = random.Next(1000, 65000);
                if (!PortSet.Contains(newport) && !PortInUse(newport)) break;
            }
            PortSet.Add(newport);
            ports.Add(groupName, newport);
            try
            {
                LastScreen.Add(groupName, new Part[10, 10]);
                messages.Add(groupName, new List<Message>());
            }
            catch { }
            StreamServer streamServer = new StreamServer(newport);
            servers.Add(groupName, streamServer);
            streamServer.Init();
            streamServer.ConnectToServer();
            return true;
        }
        public async Task getscreen()
        {
            group = groupof[Context.ConnectionId];
            try
            {
                for (int i = 0; i < 10; i++)
                    for (int j = 0; j < 10; j++)
                        await Clients.Client(Context.ConnectionId).SendAsync("UpdateScreen",
                            LastScreen[group][i, j].ms, i, j, LastScreen[group][i, j].encrypted,
                            LastScreen[group][i, j].height, LastScreen[group][i, j].width );
            }
            catch { }
        }
        public string ok()
        {
            Console.WriteLine("OK");
            return "OK";
        }
        public static string RandomString(int length)
        {
            var stringChars = new char[length];
            for (int i = 0; i < stringChars.Length; i++)
                stringChars[i] = chars[random.Next(chars.Length)];
            return new String(stringChars);
        }
        public async void AddToGroup(string groupName)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            groupof.Add(Context.ConnectionId, groupName);
            group = groupName;
        }

        public async Task RemoveFromGroup(string id)
        {
            string group = groupof[id];
            await Groups.RemoveFromGroupAsync(id, group);
            groupof.Remove(id);
        }
        public async void UpdateScreen(string ms, int r, int c, bool encrypted, int height, int width)
        {
            group = groupof[Context.ConnectionId];
            Console.WriteLine("received " + r + " " + c);
            LastScreen[group][r, c].ms = ms;
            LastScreen[group][r, c].width = width;
            LastScreen[group][r, c].height = height;
            LastScreen[group][r, c].encrypted = encrypted;

            await Clients.OthersInGroup(group).SendAsync("UpdateScreen", ms, r, c, encrypted, height, width);
        }
    }
}
