using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace PNS4OneS.KeyStorage
{
    public class ClientAppsFileStorage : IClientAppsStorage
    {
        private readonly string filePath;
        private readonly ILogger logger;

        private readonly Dictionary<string, ClientApplication> apps = new();

        public ClientAppsFileStorage(string filePath, ILogger logger)
        {
            this.filePath = filePath;
            this.logger = logger;
        }

        public void Init()
        {
            ReadAppsFromFile();
        }

        public ClientApplication CreateApp(string appName)
        {
            var app = ClientApplication.NewApp(appName);
            apps.Add(app.Id, app);

            SaveAppsToFile();

            return app;
        }

        public bool DeleteApp(string appId)
        {
            if (!apps.TryGetValue(appId, out ClientApplication app))
                return false;

            apps.Remove(app.Id);
            SaveAppsToFile();

            return true;
        }

        public ClientApplication GetApp(string appId)
        {
            if (!apps.TryGetValue(appId, out ClientApplication app))
                return null;
            return app;
        }

        public IEnumerable<ClientApplication> GetApps()
        {
            List<ClientApplication> list = new();
            foreach (var app in apps.Values)
                list.Add(app);

            return list;
        }

        public bool SaveApp(ClientApplication app)
        {
            if (!apps.ContainsKey(app.Id))
                return false;

            apps[app.Id] = app;
            SaveAppsToFile();

            return true;
        }

        private void ReadAppsFromFile()
        {
            apps.Clear();

            if (!File.Exists(filePath))
            {
                try
                {
                    using var reader = File.CreateText(filePath);
                }
                catch (Exception e)
                {
                    logger.LogError("Произошла ошибка при создании файла приложений: {Message}", e.Message);
                    return;
                }
            }

            try
            {
                using var reader = File.OpenText(filePath);

                string elemType = "title";
                ClientApplication app = null;

                string s = "";
                while ((s = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrEmpty(s))
                        continue;

                    if (s.StartsWith('#'))
                    {
                        if (elemType != "title" && elemType != "token")
                        {
                            logger.LogError("Приложения не загружены: Неверный формат файла приложений. Для создания нового файла приложений используйте pns4onesadmin.");
                            return;
                        }

                        elemType = "id";

                        if (app != null)
                            AddAppFromFile(app);

                        app = new();
                        app.Title = s[1..].Trim();
                    }
                    else if (elemType == "id")
                    {
                        elemType = "server_key";
                        app.Id = s.Trim();
                    }
                    else if (elemType == "server_key")
                    {
                        elemType = "client_key";
                        app.ServerKey = s.Trim();
                    }
                    else if (elemType == "client_key")
                    {
                        elemType = "token";
                        app.ClientKeyBase64 = s.Trim();
                    }
                    else if (elemType == "token")
                    {
                        elemType = "title";

                        int pos = s.LastIndexOf(':');
                        if (pos == -1)
                        {
                            logger.LogWarning("Приложение {Title}: неверный формат access token. Access token сброшен.", app.Title);
                            continue;
                        }

                        string token = s[..pos];
                        string expiriesInStr = s[(pos + 1)..];

                        if (!long.TryParse(expiriesInStr, out long expiresIn))
                        {
                            logger.LogWarning("Приложение {Title}: неверное значение срока жизни access token. Access token сброшен.", app.Title);
                            continue;
                        }

                        app.AccessToken = new AccessToken(token, expiresIn);
                    }
                    else
                    {
                        logger.LogError("Приложения не загружены: Неверный формат файла приложений. Для создания нового файла приложений используйте pns4onesadmin.");
                        return;
                    }
                }

                if (app != null)
                    AddAppFromFile(app);
            }
            catch (Exception e)
            {
                logger.LogError("Произошла ошибка при чтении приложений: {Message}! Для создания нового файла приложений используйте pns4onesadmin.", e.Message);
                return;
            }
        }

        private void AddAppFromFile(ClientApplication app)
        {
            bool isFailure = false;

            if (string.IsNullOrEmpty(app.Id))
            {
                logger.LogWarning("Приложение {Title} не загружено: отсутствует идентификатор", app.Title);
                isFailure = true;
            }
            if (string.IsNullOrEmpty(app.ServerKey))
            {
                logger.LogWarning("Приложение {Title} не загружено: отсутствует ключ сервера", app.Title);
                isFailure = true;
            }
            if (app.ClientKey.Length == 0)
            {
                logger.LogWarning("Приложение {Title} не загружено: отсутствует ключ клиента", app.Title);
                isFailure = true;
            }

            if (apps.TryGetValue(app.Id, out ClientApplication existApp))
            {
                logger.LogWarning(
                    "Приложение {KeyTitle} не загружено: идентификатор {KeyId} уже указан для приложения {ExistAppTitle}",
                    app.Title,
                    app.Id,
                    existApp.Title);
                isFailure = true;
            }

            if (!isFailure)
                apps.Add(app.Id, app);
        }

        private void SaveAppsToFile()
        {
            try
            {
                using var writer = File.CreateText(filePath);

                foreach (var app in apps.Values)
                {
                    writer.WriteLine($"#{app.Title}");
                    writer.WriteLine(app.Id);
                    writer.WriteLine(app.ServerKey);
                    writer.WriteLine(app.ClientKeyBase64);

                    if (app.AccessToken != null)
                        writer.WriteLine($"{app.AccessToken.Token}:{app.AccessToken.ExpiresIn.Ticks}");
                }
            }
            catch (Exception e)
            {
                logger.LogError("Произошла ошибка при записи приложений: {Message}", e.Message);
                return;
            }
        }
    }
}
