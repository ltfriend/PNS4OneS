using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PNS4OneS.KeyStorage;

namespace PNS4OneS
{
    class Program
    {
        public static IMessageSender MessageSender;
        public static IClientAppsStorage ClientAppsStorage;
        public static ClientAppsManager ClientAppsManager;

        public static int Main(string[] args)
        {
            bool printHelp = args.Length == 1 && args[0] == "-h";
            if (printHelp)
            {
                PrintHelp();
                return 0;
            }

            AppConfiguration configuration;

            try
            {
                configuration = AppConfiguration.Configure(args);
            }
            catch (AppConfigurationException e)
            {
                Console.WriteLine($"Переданы неверные параметры: {e.Message}.");
                Console.WriteLine("Используйте 'PNS4OneS -h' для вывода справки.");
                return 1;
            }

            var hostAdmin = CreateHostBuilderAdmin(configuration.ServiceConfiguration).Build();
            var hostService = CreateHostBuilderService(configuration.ListeningConfiguration).Build();
            var logger = hostService.Services.GetRequiredService<ILogger<Program>>();

            InitClientAppsManager(logger);
            AdminAccount.ReadAccount(logger);

            NotificationServer server = new(logger);
            MessageSender = server;

            server.RunAsync(configuration.ServiceConfiguration);
            hostAdmin.RunAsync();
            hostService.Run();

            server.Stop();

            return 0;
        }

        private static IHostBuilder CreateHostBuilderService(NotificationsListeningConfiguration configuration)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<StartupService>();
                    webBuilder.UseKestrel(serverOption =>
                    {
                        serverOption.Listen(configuration.EndPoint, listenOptions =>
                        {
                            switch (configuration.SslMode)
                            {
                                case NotificationsListeningConfiguration.SslModes.FromStorage:
                                    listenOptions.UseHttps(
                                        StoreName.Root,
                                        configuration.SslCertificate,
                                        true,
                                        StoreLocation.LocalMachine);
                                    break;
                                case NotificationsListeningConfiguration.SslModes.FromFileWithPrivateKey:
                                    X509Certificate2 cert = X509Certificate2.CreateFromPemFile(
                                        configuration.SslCertificate,
                                        configuration.SslCertificateKey);
                                    listenOptions.UseHttps(cert);
                                    break;
                                case NotificationsListeningConfiguration.SslModes.FromFileWithPassword:
                                    listenOptions.UseHttps(configuration.SslCertificate, configuration.SslCertificatePassword);
                                    break;
                            }
                        });
                    });
                    webBuilder.UseSetting("Logging:LogLevel:Default", configuration.LogLevel);
                });
        }

        private static IHostBuilder CreateHostBuilderAdmin(ServiceConfiguration configuration)
        {
            return Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<StartupAdmin>();
                    webBuilder.UseKestrel(serverOption =>
                    {
                        serverOption.Listen(System.Net.IPAddress.Loopback, 36697);
                    });
                    webBuilder.UseSetting("Logging:LogLevel:Default", configuration.LogLevel);
                });
        }

        private static void InitClientAppsManager(ILogger logger)
        {
            string confLocale = Utils.GetConfFilesLocale();
            string storageFile = Path.Combine(confLocale, "keys");

            ClientAppsStorage = new ClientAppsFileStorage(storageFile, logger);
            ClientAppsStorage.Init();

            ClientAppsManager = new ClientAppsManager(ClientAppsStorage);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Использование:");
            Console.WriteLine("PNS4OneS [/listen <адрес сервера>[:<номер порта>]]");
            Console.WriteLine("         [/service <адрес сервера>[:<номер порта>]]");
            Console.WriteLine("         [/log_level <Уровень логов]");
            Console.WriteLine("         [/ssl_mode <режим использования SSL сертификата>]");
            Console.WriteLine("         [/ssl_certificate <имя сертификата или путь к файлу>]");
            Console.WriteLine("         [/ssl_certificate_key <путь к приватному ключу сертификата>]");
            Console.WriteLine("         [/ssl_certificate_password <пароль к сертификату>]");
            Console.WriteLine();
            Console.WriteLine("Описание параметров:");
            Console.WriteLine("    listen       устанавливает сетевой интерфейс, на котором будет");
            Console.WriteLine("                 прослушиваться прием новых уведомлений. По умолчанию");
            Console.WriteLine("                 прослушиваются все сетевые интерфейсы.");
            Console.WriteLine("    service      устанавливает сетевой интерфейс, к которому будут подключаться");
            Console.WriteLine("                 клиенты для приема уведомлений. По умолчанию клиентские");
            Console.WriteLine("                 соединения принимаются со всех сетевых интерфейсов.");
            Console.WriteLine("    log_level    уровень выводимых логов. Возможные значения: Trace, Debug,");
            Console.WriteLine("                 Information, Warning, Error, Critical, None.");
            Console.WriteLine("    ssl_mode     режим использования SSL сертификата. Возможные значения:");
            Console.WriteLine("        None - используется незащищенное HTTP соединение.");
            Console.WriteLine("        FromStorage - используется SSL сертификат из хранилища сертификатов ОС.");
            Console.WriteLine("        FromFileWithPrivateKey - используется SSL сертификат, хранящийся в");
            Console.WriteLine("            PEM-файле на диске. Приватный ключ располагается в отдельном");
            Console.WriteLine("            PEM-файле.");
            Console.WriteLine("        FromFileWithPassword - используется SSL сертификат, хранящийся в");
            Console.WriteLine("            PFX-файле на диске вместе с приватным ключем, защищенным паролем.");
            Console.WriteLine("    ssl_certificate              указывает имя сертификата в хранилище, если");
            Console.WriteLine("                                 параметр ssl_mode=FromStorage, или путь к");
            Console.WriteLine("                                 файлу, в котором хранится SSL сертификат.");
            Console.WriteLine("    ssl_certificate_key          указывает путь к файлу приватного ключа SSL");
            Console.WriteLine("                                 сертификата. Обязетелен, если параметр");
            Console.WriteLine("                                 ssl_mode=FromFileWithPrivateKey, в противном");
            Console.WriteLine("                                 случае игнорируется.");
            Console.WriteLine("    ssl_certificate_password     указывает пароль приватного ключа SSL");
            Console.WriteLine("                                 сертификата. Обязетелен, если параметр");
            Console.WriteLine("                                 ssl_mode=FromFileWithPassword, в противном");
            Console.WriteLine("                                 случае игнорируется.");
        }
    }
}
