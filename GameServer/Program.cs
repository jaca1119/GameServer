using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
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

    class Program
    {
        static TcpListener listener;
        private static ConcurrentDictionary<string, Socket> currentUsers = new ConcurrentDictionary<string, Socket>();
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
            Handshake(client);

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
                        Hello hello = JsonConvert.DeserializeObject<Hello>(data);

                        Console.WriteLine($"Data is:{data}");
                        Console.WriteLine($"hello- message:{hello.Message}, x:{hello.X}");
                        Console.WriteLine("Sending to client, totalbytes: " + totalBytes + ", " + totalBytes.Length);
                        client.Send(totalBytes);
                    }
                } catch (SocketException e)
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();

                } catch (Newtonsoft.Json.JsonException e)
                {
                    Console.WriteLine("Exception with json");
                    Console.WriteLine(e.ToString());
                }
            }).Start();

            BeginAccepting(listener);
        }

        private static void EndConnection(Socket client)
        {
            Console.WriteLine("Connectio with: " + client.RemoteEndPoint.ToString() + " ended");
            client.Shutdown(SocketShutdown.Both);
            client.Close();
        }

        private static void Handshake(Socket client)
        {
            IPEndPoint iPEndPoint = client.RemoteEndPoint as IPEndPoint;
            //string ip = iPEndPoint.Address.ToString();
            currentUsers.TryAdd("Client" + currentUsers.Count + 1, client);
            byte[] data = Encoding.UTF8.GetBytes($"Welcome to the GameServer! Client ip: {iPEndPoint}");
            client.Send(data);
        }
    }
}
