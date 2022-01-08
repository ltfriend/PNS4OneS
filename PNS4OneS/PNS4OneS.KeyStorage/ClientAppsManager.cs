using System.Collections.Generic;

namespace PNS4OneS.KeyStorage
{
    public class ClientAppsManager
    {
        private readonly IClientAppsStorage storage;

        public ClientAppsManager(IClientAppsStorage storage)
        {
            this.storage = storage;
        }

        public IEnumerable<ClientApplication> GetApps()
        {
            return storage.GetApps();
        }

        public ClientApplication CreateApp(string appName)
        {
            return storage.CreateApp(appName);
        }

        public bool DeleteApp(string appId)
        {
            return storage.DeleteApp(appId);
        }

        public bool UpdateServerKey(string appId, out string serverKey)
        {
            serverKey = null;

            ClientApplication app = storage.GetApp(appId);
            if (app == null)
                return false;

            app.UpdateServerKey();
            serverKey = app.ServerKey;

            return storage.SaveApp(app);
        }

        public bool UpdateClientKey(string appId, out string clientKey)
        {
            clientKey = null;

            ClientApplication app = storage.GetApp(appId);
            if (app == null)
                return false;

            app.UpdateClientKey();
            clientKey = app.ClientKeyBase64;

            return storage.SaveApp(app);
        }

        public bool ResetAccessToken(string appId)
        {
            ClientApplication app = storage.GetApp(appId);
            if (app == null)
                return false;

            app.ResetAccessToken();
            storage.SaveApp(app);

            return true;
        }
    }
}
