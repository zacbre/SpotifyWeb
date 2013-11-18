using System;
using System.Collections.Generic;
using System.Text;
using Thr;
namespace SpotifyRemoteWebServer
{
    class Program
    {
        static Server server = new Server();
        static SpotifyAPI spot;
        static void Main(string[] args)
        {
            //first auth spotifyapi;
            spot = new SpotifyAPI(SpotifyAPI.GetOAuth(), "localhost");
            //now start the spotify checker thread.
            server.StartServer(spot);
        }
    }
}
