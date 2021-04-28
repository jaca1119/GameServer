using System.Net.Sockets;

namespace GameServer.Models
{
    class PlayerInfo
    {
        public Socket socket { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public string direction { get; set; }
    }
}
