using System.Collections.Generic;

namespace PNS4OneS.KeyStorage
{
    public interface IClientAppsStorage
    {
        void Init();
        ClientApplication CreateApp(string appName);
        bool DeleteApp(string appId);
        IEnumerable<ClientApplication> GetApps();
        ClientApplication GetApp(string appId);
        bool SaveApp(ClientApplication app);
    }
}
