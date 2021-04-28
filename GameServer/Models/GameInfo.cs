using System.Collections.Generic;

namespace GameServer.Models
{
    class GameInfo
    {
        public string type = "game_info";
        public List<Player> players = new List<Player>();
    }
}
