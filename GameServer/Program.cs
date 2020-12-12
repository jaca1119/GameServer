using Newtonsoft.Json;
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
    class Hello
    {
        public string Message { get; set; }
        public int X { get; set; }
    }

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
        public List<Player> players = new List<Player>();
    }

    class Player
    {
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
                        Console.WriteLine("Waiting to recive message");
                        var readBytes = client.Receive(buffer);
                        Console.WriteLine("Bytes recived: " + readBytes);

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
                                Console.WriteLine("More data");
                            }
                            else
                            {
                                Console.WriteLine("End of data");
                                break;
                            }
                        }

                        byte[] totalBytes = memoryStream.ToArray();
                        memoryStream.Close();

                        string data = Encoding.UTF8.GetString(totalBytes);
                        Console.WriteLine($"Data is:{data}");

                        HandleData(name, data);

                        //Console.WriteLine($"hello- message:{hello.Message}, x:{hello.X}");
                        //Console.WriteLine("Sending to client, totalbytes: " + totalBytes + ", " + totalBytes.Length);
                        //client.Send(totalBytes);
                        SendGameInfo(name);
                    }
                } catch (SocketException e)
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                    Console.WriteLine("Exception with socket");
                    Console.WriteLine(e);

                } catch (Newtonsoft.Json.JsonException e)
                {
                    Console.WriteLine("Exception with json");
                    Console.WriteLine(e.ToString());
                }
            }).Start();

            BeginAccepting(listener);
        }

        private static void SendGameInfo(string name)
        {
            GameInfo gameInfo = new GameInfo();

            foreach (var player in currentUsers)
            {
                if (!player.Key.Equals(name))
                {
                    Player player_info = new Player
                    {
                        x = player.Value.x,
                        y = player.Value.y,
                        direction = player.Value.direction
                    };

                    gameInfo.players.Add(player_info);
                }
            }

            string jsonGameInfo = JsonConvert.SerializeObject(gameInfo);

            PlayerInfo playerInfo = currentUsers[name];
            playerInfo.socket.Send(Encoding.UTF8.GetBytes(jsonGameInfo));
        }

        private static void HandleData(string name, string data)
        {
            //var obj = JsonConvert.DeserializeObject(data, new JsonSerializerSettings { CheckAdditionalContent = false });
            var obj = JsonConvert.DeserializeObject<PlayerInfoUpdate>(data, new JsonSerializerSettings { CheckAdditionalContent = false });
            Console.WriteLine("Recived: " + obj.GetType().Name);

            //todo check object type
            var infoUpdate = obj as PlayerInfoUpdate;

            if (infoUpdate != null)
            {
                Console.WriteLine($"Recived: {infoUpdate.x} {infoUpdate.y} {infoUpdate.direction}");

                PlayerInfo playerInfo = currentUsers[name];
                playerInfo.x = infoUpdate.x;
                playerInfo.y = infoUpdate.y;
                playerInfo.direction = infoUpdate.direction;
            }
            else
            {
                Console.WriteLine("is null");
            }
            
            //todo check object type
            //if (obj is PlayerInfoUpdate)
            //{
            //    var infoUpdate2 = obj as PlayerInfoUpdate;

            //    Console.WriteLine($"Recived: {infoUpdate.x} {infoUpdate.y} {infoUpdate.direction}");

            //    PlayerInfo playerInfo2 = currentUsers[name];
            //    playerInfo.x = infoUpdate.x;
            //    playerInfo.y = infoUpdate.y;
            //    playerInfo.direction = infoUpdate.direction;
            //}
            //else
            //{
            //    Console.WriteLine(obj);
            //}
        }

        private static void EndConnection(Socket client)
        {
            Console.WriteLine("Connectio with: " + client.RemoteEndPoint.ToString() + " ended");
            client.Shutdown(SocketShutdown.Both);
            client.Close();
        }

        private static string Handshake(Socket client)
        {
            IPEndPoint iPEndPoint = client.RemoteEndPoint as IPEndPoint;
            string ip = iPEndPoint.Address.ToString();
            string name = "Client" + currentUsers.Count + 1;
            currentUsers.TryAdd(name, new PlayerInfo { socket = client });
            byte[] data = Encoding.UTF8.GetBytes($"Welcome to the GameServer! Client ip: {ip}");
            client.Send(data);

            return name;
        }
    }
}
