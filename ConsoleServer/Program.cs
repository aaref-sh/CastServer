using System;
using StreamServer;
namespace ConsoleServer
{
    class Program
    {
        static void Main(string[] args)
        {
            string ip = args[0];
            int port = int.Parse(args[1]);
            StreameServer ss = new StreameServer(ip, port);
            ss.Init();
            ss.ConnectToServer();
        }
    }
}
