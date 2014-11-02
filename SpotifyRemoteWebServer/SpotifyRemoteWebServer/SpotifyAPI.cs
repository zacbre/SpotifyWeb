using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Newtonsoft.Json;

namespace Thr
{
    public class SpotifyAPI
    {
        private string _oauth;
        private string _host;
        private string _cfid;
        /// <summary>
        /// Initializes a new SpotifyAPI object which can be used to recieve
        /// </summary>
        /// <param name="OAuth">Use <seealso cref="SpotifyAPI.GetOAuth()"/> to get this, Or specify your own</param>
        /// <param name="Host">Most of the time 127.0.0.1, or for lulz use something like my-awesome-program.spotilocal.com</param>
        public SpotifyAPI(string OAuth, string Host)
        {
            Console.WriteLine("Creating new OAUTH with " + Host);
            _oauth = OAuth;
            _host = Host;

            //emulate the embed code [NEEDED]
            wc.Proxy = null;
            wc.Headers.Add("Origin", "https://embed.spotify.com");
            wc.Headers.Add("Referer", "https://embed.spotify.com/?uri=spotify:track:5Zp4SWOpbuOdnsxLqwgutt");
        }

        /// <summary>
        /// Get a link to the 640x640 cover art image of a spotify album
        /// </summary>
        /// <param name="uri">The Spotify album URI</param>
        /// <returns></returns>
        public static string GetArt(string uri)
        {
            try
            {
                bool grabbed = false;
                WebClient xraw = new WebClient();
                xraw.Proxy = null;
                string raw = xraw.DownloadString("http://open.spotify.com/album/" + uri.Split(new string[] { ":" }, StringSplitOptions.None)[2]);
                raw = raw.Replace(" ", "");
                string[] lines = raw.Split(new string[] { "\n" }, StringSplitOptions.None);
                foreach (string line in lines)
                {
                    if (line.Contains("VariousArtists"))
                    {
                        grabbed = true;
                    }
                    if (line.Contains("big-cover"))
                    {
                        //int content = line.IndexOf("content=\"");
                        //string url = line.Substring(content + 9);
                        //return url.Split('\"')[0];
                        string vvs = line.Replace('\t', ' ');
                        vvs = vvs.Replace(" ", "");
                        int indx = vvs.IndexOf("src=\"") + "src=\"".Length;
                        int indexx = vvs.IndexOf("\"border=\"0\"", indx);
                        string vm = vvs.Substring(indx, indexx - indx);
                        Console.WriteLine("Got art: " + vm);
                        return vm.Replace("/300/", "/640/");
                    }
                    else if (line.Contains("mo-image") && !line.Contains("mo-image-background") && line.Contains("url(//i.scdn.co/image"))
                    {
                        if (!grabbed) { grabbed = true; continue; }
                        //int content = line.IndexOf("content=\"");
                        //string url = line.Substring(content + 9);
                        //return url.Split('\"')[0];
                        string vvs = line.Replace('\t', ' ');
                        vvs = vvs.Replace(" ", "");
                        int indx = vvs.IndexOf("url(//i.scdn.co/image") + "url(//i.scdn.co/image".Length;
                        int indexx = vvs.IndexOf(")\">", indx);
                        string vm = vvs.Substring(indx, indexx - indx);
                        Console.WriteLine("Got art: " + vm);
                        return vm.Replace("/", "http://i.scdn.co/image/");
                    }
                }
            }
            catch
            {
                return "nope";
            }
            return "nope";
        }


        /// <summary>
        /// Gets the current Unix Timestamp
        /// Mostly for internal use
        /// </summary>
        public int TimeStamp
        {
            get
            {
                return Convert.ToInt32((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            }
        }

        WebClient wc = new WebClient();

        /// <summary>
        /// Gets the 'CFID', a unique identifier for the current session.
        /// Note: It's required to get the CFID before making any other calls
        /// </summary>
        public Responses.CFID CFID
        {
            get
            {
                string a = recv("simplecsrf/token.json");
                List<Responses.CFID> d = (List<Responses.CFID>)JsonConvert.DeserializeObject(a, typeof(List<Responses.CFID>));
                _cfid = d[0].token;
                //Console.WriteLine("Got CFID: " + d[0].token);
                return d[0];
            }
        }

        string _uri = "";
        /// <summary>
        /// Used by SpotifyAPI.Play to play Spotify URI's
        /// Change this URI and then call SpotifyAPI.Play
        /// </summary>
        public string URI
        {
            get
            {
                return _uri;
            }
            set
            {
                _uri = value;
            }
        }
        
        /// <summary>
        /// Plays a certain URI and returns the status afterwards
        /// Change SpotifyAPI.URI into the needed uri!
        /// </summary>
        public Responses.Status Play
        {
            get
            {
                Console.WriteLine("Playing " + URI);
                string a = recv("remote/play.json?uri=" + URI, true, true, -1);
                List<Responses.Status> d = (List<Responses.Status>)JsonConvert.DeserializeObject(a, typeof(List<Responses.Status>));
                return d[0];
            }
        }

        /// <summary>
        /// Resume Spotify playback and return the status afterwards 
        /// </summary>
        public Responses.Status Resume
        {
            get
            {
                try
                {
                    Console.WriteLine("Resuming " + URI);
                    string a = recv("remote/pause.json?pause=false", true, true, -1);
                    List<Responses.Status> d = (List<Responses.Status>)JsonConvert.DeserializeObject(a, typeof(List<Responses.Status>));
                    return d[0];
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Pause Spotify playback and return the status afterwards
        /// </summary>
        public Responses.Status Pause
        {
            get
            {
                try
                {
                    Console.WriteLine("Pausing " + URI);
                    string a = recv("remote/pause.json?pause=true", true, true, -1);
                    List<Responses.Status> d = (List<Responses.Status>)JsonConvert.DeserializeObject(a, typeof(List<Responses.Status>));
                    return d[0];
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns the current track info.
        /// Change <seealso cref="Wait"/> into the amount of waiting time before it will return
        /// When the current track info changes it will return before elapsing the amount of seconds in <seealso cref="Wait"/>
        /// (look at the project site for more information if you do not understand this)
        /// </summary>
        public Responses.Status Status
        {
            get
            {
                string a = recv("remote/status.json", true, true, _wait);
                List<Responses.Status> d = (List<Responses.Status>)JsonConvert.DeserializeObject(a, typeof(List<Responses.Status>));
                //Console.WriteLine("Got Status: " + d[0]);
                return d[0];
            }
        }

        int _wait = -1;
        /// <summary>
        /// Please see <seealso cref="Status"/> for more information
        /// </summary>
        public int Wait
        {
            get
            {
                return _wait;
            }
            set
            {
                _wait = value;
            }
        }

        /// <summary>
        /// Recieves a OAuth key from the Spotify site
        /// </summary>
        /// <returns></returns>
        public static string GetOAuth()
        {
            try
            {
                WebClient xraw = new WebClient();
                xraw.Proxy = null;
                xraw.Headers.Add("User-Agent: Mozilla/5.0 (Windows NT 6.3; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/32.0.1700.107 Safari/537.36");
                string raw = xraw.DownloadString("http://open.spotify.com/token");
                Token data = JsonConvert.DeserializeObject<Token>(raw);
                if(data != null)
                    return data.t;
                Console.WriteLine("Could not find OAuth token...");
                return null;
            }
            catch(Exception ex)
            {
                //failed.
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        internal class Token
        {
            public string t;
        }

        private string recv(string request)
        {
            return recv(request, false, false, -1);
        }

        private string recv(string request, bool oauth, bool cfid)
        {
            return recv(request, oauth, cfid, -1);
        }

        private string recv(string request, bool oauth, bool cfid, int wait)
        {
            wc.Proxy = null;
            string parameters = "?&ref=&cors=&_=" + TimeStamp;
            if (request.Contains("?"))
            {
                parameters = parameters.Substring(1);
            }

            if (oauth)
            {
                parameters += "&oauth=" + _oauth;
            }
            if (cfid)
            {
                parameters += "&csrf=" + _cfid;
            }

            if (wait != -1)
            {
                parameters += "&returnafter=" + wait;
                parameters += "&returnon=login%2Clogout%2Cplay%2Cpause%2Cerror%2Cap";
            }

            string a = "http://" + _host + ":4380/" + request + parameters;
            string derp = "";
            try
            {
                derp = wc.DownloadString(a);
                derp = "[ " + derp + " ]";
            }
            catch (NotSupportedException)
            { 
            }
            catch (Exception z)
            {
                //perhaps spotifywebhelper isn't started (happens sometimes)
                if (Process.GetProcessesByName("SpotifyWebHelper").Length < 1)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Spotify\\Data\\SpotifyWebHelper.exe");
                    }
                    catch (Exception dd)
                    {
                        Console.WriteLine("Could not launch SpotifyWebHelper.");
                    }

                    return recv(request, oauth, cfid);
                }
                //spotifywebhelper is running but we still can't connect, wtf?!
                //else //throw new Exception("Unable to connect to SpotifyWebHelper", z);
            }
            return derp;
        }

        /// <summary>
        /// Recieves client version information.
        /// Doesn't require a OAuth/CFID
        /// </summary>
        public Responses.ClientVersion ClientVersion
        {
            get
            {
                string a = recv("service/version.json?service=remote");
                List<Responses.ClientVersion> d = (List<Responses.ClientVersion>)JsonConvert.DeserializeObject(a, typeof(List<Responses.ClientVersion>));
                Console.WriteLine("Got ClientVersion: " + d[0]);
                return d[0];
            }
        }
    }
}
