using System.Net;

namespace PNS4OneS
{
    public class NotificationsListeningConfiguration
    {
        private const int DEFAUL_PORT = 36696;

        public enum SslModes
        {
            None,
            FromStorage,
            FromFileWithPrivateKey,
            FromFileWithPassword
        }

        public IPEndPoint EndPoint { get; private set; }
        public string LogLevel { get; private set; }
        public SslModes SslMode { get; private set; }
        public string SslCertificate { get; private set; }
        public string SslCertificateKey { get; private set; }
        public string SslCertificatePassword { get; private set; }

        public class Builder
        {
            private readonly NotificationsListeningConfiguration configuration;

            public Builder()
            {
                configuration = new NotificationsListeningConfiguration()
                {
                    EndPoint = new IPEndPoint(IPAddress.Any, DEFAUL_PORT),
                    SslMode = SslModes.None,
                    SslCertificate = "",
                    SslCertificateKey = "",
                    SslCertificatePassword = "",
                    LogLevel = "Warning"
                };
            }

            public Builder SetListenAddress(string s)
            {
                configuration.EndPoint = IPEndPointExtensions.ParseOrDefault(s, DEFAUL_PORT);
                return this;
            }

            public Builder SetLogLevel(string logLevel)
            {
                configuration.LogLevel = logLevel;
                return this;
            }

            public Builder SetSslMode(string sslMode)
            {
                configuration.SslMode = (SslModes)System.Enum.Parse(typeof(SslModes), sslMode);
                return this;
            }

            public Builder SetSslCertificate(string name)
            {
                configuration.SslCertificate = name;
                return this;
            }

            public Builder SetSslCertificateKey(string key)
            {
                configuration.SslCertificateKey = key;
                return this;
            }

            public Builder SetSslCertificatePassword(string password)
            {
                configuration.SslCertificatePassword = password;
                return this;
            }

            public NotificationsListeningConfiguration Build()
            {
                switch (configuration.SslMode)
                {
                    case SslModes.FromStorage:
                        if (string.IsNullOrEmpty(configuration.SslCertificate))
                            throw new AppConfigurationException(
                                "Не указано имя сертификата в хранилище. Используйте параметр \"/ssl_certificate <Имя сертификата>\".");
                        break;
                    case SslModes.FromFileWithPrivateKey:
                        if (string.IsNullOrEmpty(configuration.SslCertificate))
                            throw new AppConfigurationException(
                                "Не указан путь к файлу сертификата. Используйте параметр \"/ssl_certificate <Путь к сертификату>\"");
                        if (string.IsNullOrEmpty(configuration.SslCertificateKey))
                            throw new AppConfigurationException(
                                "Не указан путь к приватному ключу сертификата. Используйте параметр \"/ssl_certificate_key <Путь к приватному ключу>\"");
                        break;
                    case SslModes.FromFileWithPassword:
                        if (string.IsNullOrEmpty(configuration.SslCertificate))
                            throw new AppConfigurationException(
                                "Не указан путь к файлу сертификата. Используйте параметр \"/ssl_certificate <Путь к сертификату>\"");
                        if (string.IsNullOrEmpty(configuration.SslCertificatePassword))
                            throw new AppConfigurationException(
                                "Не указан пароль приватного ключа сертификата. Используйте параметр \"/ssl_certificate_password <Пароль>\"");
                        break;
                }    

                return configuration;
            }
        }
    }
}
