using System.Net;

namespace PNS4OneS
{
    public static class IPEndPointExtensions
    {
        public static IPEndPoint ParseOrDefault(string s, int defaultPort)
        {
            if (string.IsNullOrEmpty(s))
                return DefaultEndPoint(defaultPort);

            string host;
            int port;

            int pos = s.IndexOf(':');
            if (pos != -1)
            {
                host = s[..pos];
                if (!int.TryParse(s[(pos + 1)..], out port))
                    port = defaultPort;
            }
            else
            {
                host = s;
                port = defaultPort;
            }

            IPAddress ipAddress;
            switch (host.ToLower())
            {
                case "*":
                    ipAddress = IPAddress.Any;
                    break;
                case "localhost":
                case "127.0.0.1":
                    ipAddress = IPAddress.Loopback;
                    break;
                default:
                    if (!IPAddress.TryParse(host, out ipAddress))
                        ipAddress = IPAddress.Any;
                    break;
            }

            return new IPEndPoint(ipAddress, port);
        }

        private static IPEndPoint DefaultEndPoint(int defaultPort)
        {
            return new IPEndPoint(IPAddress.Any, defaultPort);
        }
    }
}
