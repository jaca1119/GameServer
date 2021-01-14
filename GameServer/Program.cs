using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace GameServer
{
    class PlayerInfo
    {
        public Socket socket { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public string direction { get; set; }
    }

    class PlayerInfoUpdate
    { 
        public int x { get; set; }
        public int y { get; set; }
        public string direction { get; set; }
    }

    class GameInfo
    {
        public string type = "game_info";
        public List<Player> players = new List<Player>();
    }

    class BulletInfo
    {
        public string type = "bullet_info";
        public int x { get; set; }
        public int y { get; set; }
        public string direction { get; set; }
    }

    class Player
    {
        public string name { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public string direction { get; set; }
    }

    class Program
    {
        static TcpListener listener;
        private static ConcurrentDictionary<string, PlayerInfo> currentUsers = new ConcurrentDictionary<string, PlayerInfo>();
        static void Main(string[] args)
        {
            Console.Title = "Game server";
            listener = new TcpListener(IPAddress.Any, 2000);
            listener.Start();

            Console.WriteLine("Starting server");
            BeginAccepting(listener);
            Console.WriteLine("Server started");

            Console.ReadKey();
        }

        private static void BeginAccepting(TcpListener listener)
        {
            listener.BeginAcceptSocket(connectionCallback, listener);
        }

        private static void connectionCallback(IAsyncResult ar)
        {
            var curListener = (TcpListener)ar.AsyncState;
            var client = curListener.EndAcceptSocket(ar);

            Console.WriteLine("Client connecting");
            string name = Handshake(client);

            new Thread(() =>
            {
                try
                {
                    while (client.Connected)
                    {
                        var buffer = new byte[1024];
                        var readBytes = client.Receive(buffer);

                        if (readBytes == 0)
                        {
                            EndConnection(client);
                            break;
                        }

                        MemoryStream memoryStream = new MemoryStream();

                        while (readBytes > 0)
                        {
                            memoryStream.Write(buffer, 0, readBytes);

                            if (client.Available > 0)
                            {
                                readBytes = client.Receive(buffer);
                            }
                            else
                            {
                                break;
                            }
                        }

                        byte[] totalBytes = memoryStream.ToArray();
                        memoryStream.Close();

                        string data = Encoding.UTF8.GetString(totalBytes);

                        HandleData(name, data);

                        SendGameInfo(name);
                    }
                }
                catch (SocketException e)
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    currentUsers.Remove(name, out _);
                    RemovePlayer(name);

                    Console.WriteLine("Exception with socket");

                } catch (Newtonsoft.Json.JsonException e)
                {
                    Console.WriteLine("Exception with json");
                }
                catch (Exception e)
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    currentUsers.Remove(name, out _);
                    RemovePlayer(name);
                }

                currentUsers.Remove(name, out _);
                RemovePlayer(name);

            }).Start();

            BeginAccepting(listener);
        }

        private static void RemovePlayer(string name)
        {
            var playerLeft = new
            {
                type = "player_left",
                name
            };

            string jsonPlayerLeft = JsonConvert.SerializeObject(playerLeft);
            var bytes = Encoding.UTF8.GetBytes(jsonPlayerLeft);

            foreach (var player in currentUsers)
            {
                player.Value.socket.Send(bytes);
            }
        }

        private static void SendGameInfo(string name)
        {
            GameInfo gameInfo = new GameInfo();
            PlayerInfo playerInfo = currentUsers[name];

            
            foreach (var player in currentUsers)
            {
                if (!player.Key.Equals(name))
                {
                    Player player_info = new Player
                    {
                        name = player.Key,
                        x = player.Value.x,
                        y = player.Value.y,
                        direction = player.Value.direction
                    };

                    gameInfo.players.Add(player_info);
                }
            }

            string jsonGameInfo = JsonConvert.SerializeObject(gameInfo);

            playerInfo.socket.Send(Encoding.UTF8.GetBytes(jsonGameInfo));
        }

        private static void HandleData(string name, string data)
        {
            JObject obj = JsonConvert.DeserializeObject(data, new JsonSerializerSettings { CheckAdditionalContent = false }) as JObject;

            if (obj.Value<string>("type").Equals("player_info_update"))
            {
                var infoUpdate = obj.ToObject<PlayerInfoUpdate>();

                if (infoUpdate != null)
                {

                    PlayerInfo playerInfo = currentUsers[name];
                    playerInfo.x = infoUpdate.x;
                    playerInfo.y = infoUpdate.y;
                    playerInfo.direction = infoUpdate.direction;
                }

            }
            else if (obj.Value<string>("type").Equals("bullet_info"))
            {
                var bulletInfo = obj.ToObject<BulletInfo>();
                string bulletInfoStr = JsonConvert.SerializeObject(bulletInfo);

                foreach (var player in currentUsers)
                {
                    if (!player.Key.Equals(name))
                    {
                        player.Value.socket.Send(Encoding.UTF8.GetBytes(bulletInfoStr));
                    }
                }
            }
            else
            {
                Console.WriteLine("not recognized object:");
                Console.WriteLine(obj);
            }
        }

        private static void EndConnection(Socket client)
        {
            Console.WriteLine("Connectio with: " + client.RemoteEndPoint.ToString() + " end.");
            client.Shutdown(SocketShutdown.Both);
            client.Close();
        }

        private static string Handshake(Socket client)
        {
            IPEndPoint iPEndPoint = client.RemoteEndPoint as IPEndPoint;
            string ip = iPEndPoint.Address.ToString();
            string name = "Client" + (currentUsers.Count + 1);
            currentUsers.TryAdd(name, new PlayerInfo { socket = client });
            byte[] data = Encoding.UTF8.GetBytes($"Welcome to the GameServer! Client ip: {ip}");
            client.Send(data);

            return name;
        }
    }
}
