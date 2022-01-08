using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using PNS4OneS.KeyStorage;

namespace PNS4OneS
{
    public static class SendMessageHandler
    {
        public static async Task SendMessage(HttpRequest request, HttpResponse response)
        {
            if (!CheckAuth(request, out string clientAppId))
            {
                response.StatusCode = 401;
                return;
            }

            IncomingMessage incomingMessage = null;
            bool badRequest;

            try
            {
                incomingMessage = await ReadIncomingMessageFromRequestBody(request);
                badRequest = !IncomingMessageIsCorrect(incomingMessage);
            }
            catch
            {
                badRequest = true;
            }

            if (badRequest)
            {
                response.StatusCode = 400;
                return;
            }

            string recipientType = incomingMessage.Recipient.Type.ToLower();
            switch (recipientType)
            {
                case "user":
                    await Program.MessageSender.SendMessageToUserAsync(
                        clientAppId,
                        incomingMessage.Recipient.IbId,
                        incomingMessage.Recipient.UserId,
                        incomingMessage.Message
                    );
                    break;
                case "group":
                    await Program.MessageSender.SendMessageToGroupAsync(
                        clientAppId,
                        incomingMessage.Recipient.IbId,
                        incomingMessage.Recipient.UserGroup,
                        incomingMessage.Message
                    );
                    break;
                case "all":
                    await Program.MessageSender.SendMessageToAllAsync(
                        clientAppId,
                        incomingMessage.Recipient.IbId,
                        incomingMessage.Message
                    );
                    break;
            }

            response.StatusCode = 200;
        }

        private static async Task<IncomingMessage> ReadIncomingMessageFromRequestBody(HttpRequest request)
        {
            using var stream = request.BodyReader.AsStream();
            return await JsonSerializer.DeserializeAsync<IncomingMessage>(stream);
        }

        private static bool IncomingMessageIsCorrect(IncomingMessage message)
        {
            if (message == null
                || message.Message == null
                || message.Recipient == null
                || string.IsNullOrEmpty(message.Recipient.Type)
                || string.IsNullOrEmpty(message.Recipient.IbId))
            {
                return false;
            }

            return message.Recipient.Type switch
            {
                "user" => !string.IsNullOrEmpty(message.Recipient.UserId),
                "group" => !string.IsNullOrEmpty(message.Recipient.UserGroup),
                "all" => true,
                _ => false,
            };
        }

        private static bool CheckAuth(HttpRequest request, out string clientAppId)
        {
            clientAppId = "";

            string accessTokenAuth = request.Headers["Authorization"];
            if (string.IsNullOrEmpty(accessTokenAuth) || !accessTokenAuth.StartsWith("Bearer "))
                return false;

            string accessTokenStr = accessTokenAuth.Replace("Bearer ", "");

            var apps = Program.ClientAppsStorage.GetApps();
            ClientApplication app = apps.FirstOrDefault(x => x.AccessToken?.Token == accessTokenStr);

            if (app == null || DateTime.UtcNow > app.AccessToken.ExpiresIn)
                return false;

            clientAppId = app.Id;
            return true;
        }
    }
}
