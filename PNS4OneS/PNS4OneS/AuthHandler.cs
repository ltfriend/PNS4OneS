using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PNS4OneS.KeyStorage;

namespace PNS4OneS
{
    public static class AuthHandler
    {
        public static async Task Auth(HttpRequest request, HttpResponse response)
        {
            string serverKey = request.Query["server_key"].ToString();
            if (string.IsNullOrEmpty(serverKey))
            {
                response.StatusCode = 401;
                return;
            }

            ClientApplication clientApp = GetClientAppByServerKey(serverKey);
            if (clientApp == null)
            {
                response.StatusCode = 400;
                return;
            }

            clientApp.UpdateAccessToken();
            if (!Program.ClientAppsStorage.SaveApp(clientApp))
            {
                response.StatusCode = 500;
                return;
            }

            response.Headers.Add("Content-Type", "application/json");

            string body = string.Format("{{\"access_token\": \"{0}\", \"expires_in\": {1:0}}}",
                clientApp.AccessToken.Token,
                new System.TimeSpan(clientApp.AccessToken.ExpiresIn.Ticks).TotalSeconds);

            await response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(body));
        }

        private static ClientApplication GetClientAppByServerKey(string serverKey)
        {
            var apps = Program.ClientAppsStorage.GetApps();
            return apps.FirstOrDefault(x => x.ServerKey == serverKey);
        }
    }
}
