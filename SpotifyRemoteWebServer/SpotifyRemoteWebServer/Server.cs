using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Web;
using Thr;
using System.Collections.Specialized;
using System.IO;
using spotyapi;

namespace SpotifyRemoteWebServer
{
    class Server
    {
        static ArrayList ClientsList = new ArrayList();
        static Socket Listener_Socket;
        static SpotifyWebClient Newclient;
        static SpotifyAPI spot;
        static SpotyController controller = new SpotyController();
        public Server()
        {
        }
        public bool StartServer(SpotifyAPI spotty)
        {
            spot = spotty;
            StartMainServer(80);
            //Recording thread.
            return true;
        }
        public static void StopServer()
        {
            try
            {
                Listener_Socket.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Console.WriteLine("Shutting Down Server...");
                Thread.Sleep(5000);
            }
            catch (Exception ex) {  }
        }
        public static string LocalIPAddress
        {
            get
            {
                IPHostEntry host;
                string localip = "";
                host = Dns.GetHostEntry(Dns.GetHostName());
                for (int i = 0; i < host.AddressList.Length; i++)
                {
                    if (host.AddressList[i].AddressFamily.ToString() == "InterNetwork" && host.AddressList[i].ToString() != "")
                    {
                        localip += host.AddressList[i];
                    }
                    if (i < host.AddressList.Length - 1 && host.AddressList[i].ToString() != "" && host.AddressList[i].AddressFamily.ToString() == "InterNetwork")
                    {
                        localip += "/";
                    }
                }
                if (localip[localip.Length - 1] == '/')
                {
                    localip = localip.Substring(0, localip.Length - 1);
                }
                return localip;
            }
        }
        public static void StartMainServer(int Port)
        {
            try
            {
                Listener_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                Listener_Socket.Bind(new IPEndPoint(IPAddress.Any, Port));
                Listener_Socket.Listen(10);
                Listener_Socket.BeginAccept(new AsyncCallback(EndAccept), Listener_Socket);

                Console.WriteLine("Server is Listening on " + LocalIPAddress + ":" + Port);
                Thread tr = new Thread(new ThreadStart(keepAlive));
                tr.Start();
            }
            catch (Exception ex) { Console.WriteLine(ex.Message); Thread.Sleep(10000); }
        }
        static void keepAlive()
        {
            while (true)
            {
                Thread.Sleep(10000);
            }
        }
        private static void EndAccept(IAsyncResult ar)
        {
            try
            {
                Listener_Socket = (Socket)ar.AsyncState;
                AddClient(Listener_Socket.EndAccept(ar));
                Listener_Socket.BeginAccept(new AsyncCallback(EndAccept), Listener_Socket);
            }
            catch (Exception) { }
        }

        private static void AddClient(Socket sockClient)
        {
            Newclient = new SpotifyWebClient(sockClient);
            ClientsList.Add(Newclient);
            //The client logs in, so give them a list of available chat rooms.
            //Console.WriteLine(sockClient.RemoteEndPoint + " has logged in.");
            Newclient.SetupRecieveCallback();
        }
        static MemoryStream mf;
        public static void OnRecievedData(IAsyncResult ar)
        {
            SpotifyWebClient client = (SpotifyWebClient)ar.AsyncState;
            byte[] aryRet = client.GetRecievedData(ar);

            if (aryRet.Length < 1)
            {
                Console.WriteLine(client.ReadOnlySocket.RemoteEndPoint + " has left.");
                client.ReadOnlySocket.Close();
                ClientsList.Remove(client);
                return;
            }
            string data = Encoding.ASCII.GetString(aryRet);
            string[] headers = data.Split('\n');
            string[] get = headers[0].Split(' ');
            if (get.Length != 3) { client.ReadOnlySocket.Send(Encoding.ASCII.GetBytes("Invalid Request.")); client.ReadOnlySocket.Close(); }
            //get the GET shit.
            //list commands here.
            try
            {
                Responses.CFID cfid = spot.CFID;
                switch (get[1])
                {
                    case "/play":
                        Console.WriteLine("Play");
                        var f = spot.Resume;
                        Send(client.ReadOnlySocket, "Playing.");
                        break;
                    case "/pause":
                        Console.WriteLine("Pause");
                        var fg = spot.Pause;
                        Send(client.ReadOnlySocket, "Paused.");
                        break;
                    case "/next":
                        Console.WriteLine("Next");
                        controller.PlayNext();
                        Send(client.ReadOnlySocket, "Next.");
                        break;
                    case "/prev":
                        controller.PlayPrev();
                        Send(client.ReadOnlySocket, "Prev.");
                        break;
                    case "/volup":
                        controller.VolumeUp();
                        Send(client.ReadOnlySocket, "VolUp.");
                        break;
                    case "/voldwn":
                        controller.VolumeDown();
                        Send(client.ReadOnlySocket, "VolDown.");
                        break;
                    case "/live":
                        Send(client.ReadOnlySocket, "HTTP/1.1 200 OK\r\nServer: SpotifyWebServer\r\nTransfer-Encoding : chunked\r\nContent-Type : audio/wav\r\nConnection: close\r\n\r\n", false);
                        //Live Streaming not implemented yet. In progress.
                        break;
                    case "/":
                        Send(client.ReadOnlySocket, File.ReadAllText("page.html"));
                        break;
                    default:
                        //check for question mark.
                        if(get[1].Contains("?"))
                        {
                            status = spot.Status;
                            //Build our string.
                            Send(client.ReadOnlySocket, buildreq(status, get[1].Split('?')[1]));
                            return;
                        }
                        //Send(client.ReadOnlySocket, "Invalid Request.");
                        //Try to load the file.
                        try
                        {
                        byte[] m = File.ReadAllBytes(Environment.CurrentDirectory + get[1].Replace("/", "\\"));
                        Send(client.ReadOnlySocket, m);
                        }
                        catch(FileNotFoundException ex)
                        {
                            Send(client.ReadOnlySocket, "HTTP/1.1 404 NOT FOUND\r\n\r\n");
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.StartsWith("Object"))
                {
                    //Restart Spotify.
                    spot = new SpotifyAPI(SpotifyAPI.GetOAuth(), "localhost");
                }
                Send(client.ReadOnlySocket, ex.Message);
            }
        }
        public static string BuildHeader(string sMIMEHeader, int iTotBytes, string sStatusCode)
        {
            String sBuffer = "";
            // if Mime type is not provided set default to text/html
            if (sMIMEHeader.Length == 0)
            {
                sMIMEHeader = "text/html";  // Default Mime Type is text/html
            }
            sBuffer = sBuffer + "HTTP/1.1 " + sStatusCode + "\r\n";
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Content-Type: " + sMIMEHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            sBuffer = sBuffer + "Content-Length: " + iTotBytes + "\r\n\r\n";
            return sBuffer;
        }
        public static void Send(Socket f, string m)
        {
            f.Send(Encoding.ASCII.GetBytes(BuildHeader("text/html", m.Length, "202 OK") + m));
            f.Close();
        }
        public static void Send(Socket f, byte[] m)
        {
            f.Send(m);
            f.Close();
        }
        public static void Send(Socket f, string m, bool close)
        {
            f.Send(Encoding.ASCII.GetBytes(m));
        }
        public static byte[] str(string str)
        {
            return Encoding.ASCII.GetBytes(str);
        }
        public static string artURL = "";
        public static Responses.Status status;
        private static string buildreq(Responses.Status stat, string req)
        {
            StringBuilder f = new StringBuilder();
            if (stat.track.track_resource.uri == req)
            {
                f.AppendLine("not_changed");
                f.AppendLine((stat.playing ? 1 : 0).ToString());
                f.AppendLine((stat.repeat ? 1 : 0).ToString());
                f.AppendLine((stat.shuffle ? 1 : 0).ToString());
                f.AppendLine((stat.volume.ToString()));
                f.AppendLine(new DateTime().AddSeconds(stat.playing_position).ToString("HH:mm:ss"));
                f.AppendLine(stat.track.track_resource.uri);
                return f.ToString();
            }
            f.AppendLine((stat.playing ? 1 : 0).ToString());
            f.AppendLine((stat.repeat ? 1 : 0).ToString());
            f.AppendLine((stat.shuffle ? 1 : 0).ToString());
            f.AppendLine((stat.volume.ToString()));
            f.AppendLine(new DateTime().AddSeconds(stat.playing_position).ToString("HH:mm:ss"));
            f.AppendLine(stat.track.track_resource.name);
            f.AppendLine(stat.track.album_resource.name);
            f.AppendLine(stat.track.artist_resource.name);
            f.AppendLine(SpotifyAPI.GetArt(stat.track.album_resource.uri));
            f.AppendLine(stat.track.track_resource.uri);
            return f.ToString();
        }
    }
    class SpotifyWebClient
        {
            // To create a new socket for each client 
            private Socket New_Socket;
            private string ChatServer;
            private byte[] buffer = new byte[10000];

            public SpotifyWebClient(Socket PassedSock)
            {
                New_Socket = PassedSock;
            }

            public Socket ReadOnlySocket
            {
                get { return New_Socket; }
            }

            public void SetupRecieveCallback()
            {
                try
                {
                    AsyncCallback recieveData = new AsyncCallback(SpotifyRemoteWebServer.Server.OnRecievedData);
                    New_Socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, recieveData, this);
                }
                catch (Exception)
                {
                }
            }
            public byte[] GetRecievedData(IAsyncResult ar)
            {
                int nBytesRec = 0;
                try
                {
                    nBytesRec = New_Socket.EndReceive(ar);
                }
                catch { }
                byte[] byReturn = new byte[nBytesRec];
                Array.Copy(buffer, byReturn, nBytesRec);
                return byReturn;
            }
        }      
}
