using System.Net;

namespace PNS4OneS
{
    public class ServiceConfiguration
    {
        private const int DEFAUL_PORT = 36695;

        public IPEndPoint EndPoint { get; private set; }
        public string LogLevel { get; private set; }

        private ServiceConfiguration() { }

        public class Builder
        {
            private readonly ServiceConfiguration configuration;

            public Builder()
            {
                configuration = new()
                {
                    EndPoint = new IPEndPoint(IPAddress.Any, DEFAUL_PORT),
                    LogLevel = "Warning"
                };
            }

            public Builder SetServiceAddress(string s)
            {
                configuration.EndPoint = IPEndPointExtensions.ParseOrDefault(s, DEFAUL_PORT);
                return this;
            }

            public Builder SetLogLevel(string logLevel)
            {
                configuration.LogLevel = logLevel;
                return this;
            }

            public ServiceConfiguration Build()
            {
                return configuration;
            }
        }
    }
}
