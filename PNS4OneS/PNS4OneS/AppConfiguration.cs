using System.IO;
using Microsoft.Extensions.Configuration;
using PNS4OneS.KeyStorage;

namespace PNS4OneS
{
    class AppConfiguration
    {
        public NotificationsListeningConfiguration ListeningConfiguration { get; private set; }
        public ServiceConfiguration ServiceConfiguration { get; private set; }

        private AppConfiguration() { }

        public static AppConfiguration Configure(string[] args)
        {
            AppConfiguration result = new();

            var listeningConfigurationBuilder = new NotificationsListeningConfiguration.Builder();
            var serviceConfigurationBuiler = new ServiceConfiguration.Builder();

            var config = new ConfigurationBuilder()
                .AddIniFile(Path.Combine(Utils.GetConfFilesLocale(), "PNS4OneS.ini"), true)
                .AddCommandLine(args)
                .Build();

            var parameters = config.GetChildren();
            foreach (var param in parameters)
            {
                switch (param.Key)
                {
                    case "listen":
                        listeningConfigurationBuilder.SetListenAddress(param.Value);
                        break;
                    case "service":
                        serviceConfigurationBuiler.SetServiceAddress(param.Value);
                        break;
                    case "log_level":
                        listeningConfigurationBuilder.SetLogLevel(param.Value);
                        serviceConfigurationBuiler.SetLogLevel(param.Value);
                        break;
                    case "ssl_mode":
                        listeningConfigurationBuilder.SetSslMode(param.Value);
                        break;
                    case "ssl_certificate":
                        listeningConfigurationBuilder.SetSslCertificate(param.Value);
                        break;
                    case "ssl_certificate_key":
                        listeningConfigurationBuilder.SetSslCertificateKey(param.Value);
                        break;
                    case "ssl_certificate_password":
                        listeningConfigurationBuilder.SetSslCertificatePassword(param.Value);
                        break;
                    default:
                        throw new AppConfigurationException($"Неизвестный параметр {param.Key}");
                }
            }

            result.ListeningConfiguration = listeningConfigurationBuilder.Build();
            result.ServiceConfiguration = serviceConfigurationBuiler.Build();

            return result;
        }
    }
}
