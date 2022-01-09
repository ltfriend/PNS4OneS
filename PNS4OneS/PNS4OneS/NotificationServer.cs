using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Text;
using Microsoft.Extensions.Logging;
using PNS4OneS.KeyStorage;

namespace PNS4OneS
{
    class NotificationServer : IMessageSender
    {
        private const int SENDING_MESSAGE_WORKERS_COUNT = 4;

        private class MessageToSend
        {
            public string ClientAppId { get; set; }
            public List<ClientConnection> Recepients { get; set; }
            public Message Message { get; set; }
            public bool Terminate { get; private set; } = false;

            public static MessageToSend TerminatedMessage() => new() { Terminate = true };
        }

        private readonly List<ClientConnection> connections = new();
        private Socket socket = null;

        private readonly ILogger logger;

        private readonly Task[] sendingMessageWorkers = new Task[SENDING_MESSAGE_WORKERS_COUNT];
        private readonly Channel<MessageToSend> messagesChannel = Channel.CreateUnbounded<MessageToSend>();

        private bool stoppedService = false;

        public NotificationServer(ILogger logger)
        {
            this.logger = logger;
        }

        public void RunAsync(ServiceConfiguration configuration)
        {
            stoppedService = false;

            socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(configuration.EndPoint);
            socket.Listen();

            logger.LogInformation("Push Service Notification For 1C now listening on {EndPoint}", configuration.EndPoint);

            for (int i = 0; i < SENDING_MESSAGE_WORKERS_COUNT; i++)
            {
                sendingMessageWorkers[i] = new Task(async () => await SendingMessageWorker());
                sendingMessageWorkers[i].Start();
            }

            Task.Run(() => LoopConnections());
        }

        public void Stop()
        {
            stoppedService = true;

            for (int i = 0; i < SENDING_MESSAGE_WORKERS_COUNT; i++)
                messagesChannel.Writer.TryWrite(MessageToSend.TerminatedMessage());
            Task.WaitAll(sendingMessageWorkers);

            if (socket != null)
            {
                for (int i = 0; i < connections.Count; i++)
                {
                    // При использовании foreach после первой итерации, почему то,
                    // возникает ошибка о том, что коллекция была модифицирована.
                    CloseClientConnection(connections[i]);
                }
                connections.Clear();
                socket.Close();
            }
        }

        public async Task SendMessageToUserAsync(string appId, string ibId, string userId, Message message)
        {
            var recepients = GetRecipientsByUserId(ibId, userId);
            await SendMessageAsync(appId, recepients, message);
        }

        public async Task SendMessageToGroupAsync(string appId, string ibId, string userGroup, Message message)
        {
            var recepients = GetRecepientsByGroupNum(ibId, userGroup);
            await SendMessageAsync(appId, recepients, message);
        }

        public async Task SendMessageToAllAsync(string appId, string ibId, Message message)
        {
            var recepients = GetAllIbRecepients(ibId);
            await SendMessageAsync(appId, recepients, message);
        }

        private async Task SendMessageAsync(string appId, List<ClientConnection> recepients, Message message)
        {
            var messageToSend = new MessageToSend()
            {
                ClientAppId = appId,
                Recepients = recepients,
                Message = message
            };
            await messagesChannel.Writer.WriteAsync(messageToSend);
        }

        private void LoopConnections()
        {
            ArrayList checkReadList = new();
            ArrayList checkErrorsList = new();

            while (true)
            {
                checkReadList.Clear();
                checkReadList.Add(socket);

                checkErrorsList.Clear();
                checkErrorsList.Add(socket);

                foreach (ClientConnection client in connections)
                {
                    checkReadList.Add(client.Socket);
                    checkErrorsList.Add(client.Socket);
                }

                try
                {
                    Socket.Select(checkReadList, null, checkErrorsList, -1);
                }
                catch (Exception e)
                {
                    if (!stoppedService)
                    {
                        // При остановке сервиса закрывается сокет и так же возникает исключение.
                        // В этом случае оно просто игнорируется.
                        logger.LogWarning(
                            "Произошла ошибка при определении состояния сокетов: {message}. Отправление сообщений прервано.",
                            e.Message);
                    }
                    break;
                }

                foreach (Socket checkSocket in checkReadList)
                {
                    if (checkSocket == socket)
                    {
                        Socket handler = checkSocket.Accept();
                        ClientConnection client = AddNewClient(handler);
                    }
                    else
                    {
                        ClientConnection client = connections.FirstOrDefault(x => x.Socket == checkSocket);
                        if (client == null)
                            continue;

                        if (!client.ReceiveData())
                        {
                            CloseClientConnection(client);
                            continue;
                        }

                        while (client.ReceivedDataQueue.TryDequeue(out var receivedData))
                        {
                            if (receivedData.DataType == ClientConnection.ReceivedDataType.RegisterClient)
                            {
                                if (!client.RegisterClient(receivedData.Data))
                                {
                                    CloseClientConnection(client);
                                    continue;
                                }
                            }
                            else if (receivedData.DataType == ClientConnection.ReceivedDataType.CloseConnestion)
                            {
                                CloseClientConnection(client);
                                continue;
                            }
                        }
                    }
                }

                foreach (Socket checkSocket in checkErrorsList)
                {
                    ClientConnection client = connections.FirstOrDefault(x => x.Socket == checkSocket);
                    if (client != null)
                        CloseClientConnection(client);
                }
            }
        }

        private async Task SendingMessageWorker()
        {
            while (true)
            {
                MessageToSend messageToSend = await messagesChannel.Reader.ReadAsync();
                if (messageToSend.Terminate)
                    break;

                ClientApplication clientApp = Program.ClientAppsStorage.GetApp(messageToSend.ClientAppId);
                if (clientApp == null)
                    continue;

                byte[] data = SerializeMessage(messageToSend.Message);
                byte[] encrypted = EncryptMessage(data, clientApp.ClientKey, clientApp.ClientIV);

                foreach (ClientConnection conn in messageToSend.Recepients)
                {
                    try
                    {
                        using NetworkStream stream = new(conn.Socket);
                        using BinaryWriter writer = new(stream);
                        writer.Write(encrypted.Length + sizeof(int)); // + размер данных
                        writer.Write(data.Length);
                        writer.Write(encrypted);
                    }
                    catch (Exception e)
                    {
                        logger.LogWarning(
                            "Произошла ошибка при отправке сообщения клиенту {userId}: {message}. Соединение с клиентом прервано.",
                            conn.UserId,
                            e.Message);
                        CloseClientConnection(conn);
                        continue;
                    }
                }
            }
        }

        private ClientConnection AddNewClient(Socket handler)
        {
            ClientConnection client = new(handler, logger);
            connections.Add(client);
            return client;
        }

        private List<ClientConnection> GetRecipientsByUserId(string ibId, string userId)
        {
            return (from conn in connections
                    where conn.IbId == ibId && conn.UserId == userId
                    select conn).ToList();
        }

        private List<ClientConnection> GetRecepientsByGroupNum(string ibId, string userGroup)
        {
            return (from conn in connections
                    where conn.IbId == ibId && conn.UserGroup == userGroup
                    select conn).ToList();
        }

        private List<ClientConnection> GetAllIbRecepients(string ibId)
        {
            return (from conn in connections
                    where conn.IbId == ibId
                    select conn).ToList();
        }

        private void CloseClientConnection(ClientConnection client)
        {
            CloseClientSocket(client.Socket);
            connections.Remove(client);
        }

        private void CloseClientSocket(Socket clientSocket)
        {
            try
            {
                clientSocket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception e)
            {
                logger.LogWarning("Произошла ошибка при закрытии соединения: {message}", e.Message);
            }
            clientSocket.Close();
        }

        private static byte[] EncryptMessage(byte[] message, byte[] key, byte[] iv)
        {
            using Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using MemoryStream inputStream = new(message);
            using MemoryStream outputStream = new();
            using CryptoStream cryptoStream = new(outputStream, encryptor, CryptoStreamMode.Write);

            int blockSize = aes.BlockSize / 8;
            byte[] data = new byte[blockSize];
            int count = 0;

            do
            {
                count = inputStream.Read(data, 0, blockSize);
                cryptoStream.Write(data, 0, count);
            } while (count > 0);

            cryptoStream.FlushFinalBlock();
            cryptoStream.Close();

            return outputStream.ToArray();
        }

        private static byte[] SerializeMessage(Message message)
        {
            StringBuilder builder = new(512);
            builder.Append('{');

            SerializeStringValue(builder, "topic", message.Topic, false);
            bool needSeparator = !string.IsNullOrEmpty(message.Topic);

            if (message.Notification != null
                && (!string.IsNullOrEmpty(message.Notification.Title)
                    || !string.IsNullOrEmpty(message.Notification.Body)))
            {
                if (needSeparator)
                    builder.Append(", ");

                SerializeMessageNotification(builder, message.Notification);
                needSeparator = true;
            }

            if (message.Data != null && message.Data.Count > 0)
            {
                if (needSeparator)
                    builder.Append(", ");

                SerializeMessageData(builder, message.Data);
            }

            builder.Append('}');

            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        private static void SerializeMessageNotification(StringBuilder builder, Notification notification)
        {
            builder.Append("\"notification\": {");

            SerializeStringValue(builder, "title", notification.Title);
            SerializeStringValue(builder, "body", notification.Body);
            SerializeStringValue(builder, "icon", notification.Icon);
            SerializeStringValue(builder, "action", notification.Action);

            builder.Append("\"important\": " + (notification.Important ? "true" : "false"));

            builder.Append('}');
        }

        private static void SerializeMessageData(StringBuilder builder, Dictionary<string, string> data)
        {
            builder.Append("\"data\": {");

            bool first = true;
            foreach (var keyValue in data)
            {
                if (first)
                    first = false;
                else
                    builder.Append(", ");

                SerializeStringValue(builder, keyValue.Key, keyValue.Value, false);
            }

            builder.Append('}');
        }

        private static void SerializeStringValue(StringBuilder builder, string name, string value, bool appendSepatator = true)
        {
            if (!string.IsNullOrEmpty(value))
            {
                builder.Append("\"" + name + "\": \"" + value + "\"");
                if (appendSepatator)
                    builder.Append(", ");
            }
        }
    }
}
