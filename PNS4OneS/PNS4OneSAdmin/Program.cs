using System;
using System.IO;
using Microsoft.Extensions.Logging;
using PNS4OneS.KeyStorage;

namespace PNS4OneSAdmin
{
    class Program
    {
        private static ClientAppsManager appsManager;

        static int Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h")
            {
                PrintHelp();
                return 0;
            }

            InitClientAppsManager();

            string command = args[0];
            string param = args.Length == 1 ? null : args[1];

            return ExecuteCommand(command, param);
        }

        private static void InitClientAppsManager()
        {
            string confLocale = Utils.GetConfFilesLocale();
            string storageFile = Path.Combine(confLocale, "keys");

            var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("PNS4OneSAdmin");

            var appsStorage = new ClientAppsFileStorage(storageFile, logger);
            appsStorage.Init();

            appsManager = new ClientAppsManager(appsStorage);
        }

        private static int ExecuteCommand(string command, string param)
        {
            switch (command)
            {
                case "create":
                    if (string.IsNullOrEmpty(param))
                    {
                        ShowParamError("Не указано название приложения.");
                        return 1;
                    }
                    ClientApplication app = appsManager.CreateApp(param);
                    if (app != null)
                    {
                        Console.WriteLine($"Создано новое приложение {app.Title}");
                        Console.WriteLine($"Идентификатор приложения: {app.Id}");
                        Console.WriteLine($"Ключ сервера для приложения: {app.ServerKey}");
                        Console.WriteLine($"Ключ клиента для приложения: {app.ClientKeyBase64}");
                    }
                    break;
                case "remove":
                    if (string.IsNullOrEmpty(param))
                    {
                        ShowParamError("Отсутствует идентификатор приложения.");
                        return 1;
                    }
                    if (!appsManager.DeleteApp(param))
                    {
                        Console.WriteLine($"Отсутствует приложение с идентификатором {param}");
                        return 1;
                    }
                    break;
                case "upd_srv_key":
                    if (string.IsNullOrEmpty(param))
                    {
                        ShowParamError("Отсутствует идентификатор приложения.");
                        return 1;
                    }
                    if (appsManager.UpdateServerKey(param, out string serverKey))
                    {
                        Console.WriteLine($"Новый ключ сервера приложения: {serverKey}");
                    }
                    else
                    {
                        Console.WriteLine($"Отсутствует приложение с идентификатором {param}");
                        return 1;
                    }
                    break;
                case "upd_cli_key":
                    if (string.IsNullOrEmpty(param))
                    {
                        ShowParamError("Отсутствует идентификатор приложения.");
                        return 1;
                    }
                    if (appsManager.UpdateClientKey(param, out string clientKey))
                    {
                        Console.WriteLine($"Новый ключ клиента приложения: {clientKey}");
                    }
                    else
                    {
                        Console.WriteLine($"Отсутствует приложение с идентификатором {param}");
                        return 1;
                    }
                    break;
                case "reset_token":
                    if (string.IsNullOrEmpty(param))
                    {
                        ShowParamError("Отсутствует идентификатор приложения.");
                        return 1;
                    }
                    if (!appsManager.ResetAccessToken(param))
                    {
                        Console.WriteLine($"Отсутствует приложение с идентификатором {param}");
                        return 1;
                    }
                    break;
                case "list":
                    PrintKeysList();
                    break;
                default:
                    ShowParamError($"Неизвестная команда {command}.");
                    break;
            }

            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("Утилита администрирования сервиса уведомлений PNS4OneS.");
            Console.WriteLine("Использование: PNS4OneSAdmin <Команда> [<Параметры команды>]");
            Console.WriteLine();
            Console.WriteLine("Список команд:");
            Console.WriteLine("    list           Вывод списка подключенных приложений, которые могут");
            Console.WriteLine("                   выполнять отправку уведомлений. Параметры отсутствуют.");
            Console.WriteLine("    create         Создает новое приложение для отправки уведомлений. В");
            Console.WriteLine("                   качестве параметра указывается название создаваемого");
            Console.WriteLine("                   приложения.");
            Console.WriteLine("    upd_srv_key    Обновляет ключ сервера для указанного приложения. В качестве");
            Console.WriteLine("                   параметра указывается идентификатор приложения, для которого");
            Console.WriteLine("                   необходимо обновить ключ сервера.");
            Console.WriteLine("    upd_cli_key    Обновляет ключ клиента для указанного приложения. В качестве");
            Console.WriteLine("                   параметра указывается идентификатор приложения, для которого");
            Console.WriteLine("                   необходимо обновить ключ клиента.");
            Console.WriteLine("    remove         Удаляет приложение, выполняющего отправку уведомлений. В");
            Console.WriteLine("                   качестве параметра указывается идентификатор приложения,");
            Console.WriteLine("                   которое необходимо удалить.");
            Console.WriteLine("    reset_token    Сбрасывает ключ доступа к серверу для приложения. В качестве");
            Console.WriteLine("                   параметра указывается идентификатор приложения, для которого");
            Console.WriteLine("                   необходимо сбросить ключ доступа. После сброса ключа");
            Console.WriteLine("                   приложению необходимо повторно выполнить процесс авторизации.");
        }

        private static void PrintKeysList()
        {
            var apps = appsManager.GetApps();
            bool appsExist = false;

            foreach (ClientApplication key in apps)
            {
                Console.WriteLine(key.Title);
                Console.WriteLine($"Идентификатор: {key.Id}");
                Console.WriteLine($"Ключ сервера: {key.ServerKey}");
                Console.WriteLine($"Ключ клиента: {key.ClientKeyBase64}");
                Console.WriteLine();

                appsExist = true;
            }

            if (!appsExist)
            {
                Console.WriteLine("Клиентские приложения отсутствуют.");
                Console.WriteLine("Используйте \"PNS4OneSAdmin create <имя приложения>\" для создания нового приложения.");
                return;
            }
        }

        private static void ShowParamError(string message)
        {
            Console.WriteLine(message);
            Console.WriteLine("Используйте 'PNS4OneS -h' для вывода справки.");
        }
    }
}
