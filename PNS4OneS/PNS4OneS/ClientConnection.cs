using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace PNS4OneS
{
    class ClientConnection
    {
        private const ushort RECV_DATA_MAX_SIZE = 1024;

        public Socket Socket { get; }
        public string IbId { get; set;  }
        public string UserId { get; set; }
        public string UserGroup { get; set; }

        private byte[] receiveBuf = new byte[RECV_DATA_MAX_SIZE];
        private int receiveBufPos = 0;
        private int dataSize = -1;

        private readonly ILogger logger;

        public enum ReceivedDataType
        {
            RegisterClient,
            CloseConnestion
        }

        public struct ReceivedData
        {
            public ReceivedDataType DataType { get; set; }
            public byte[] Data { get; set; }
        }

        public Queue<ReceivedData> ReceivedDataQueue { get; } = new();

        public ClientConnection(Socket socket, ILogger logger)
        {
            Socket = socket;
            IbId = "";
            UserId = "";
            UserGroup = "";
            this.logger = logger;
        }

        public bool ReceiveData()
        {
            int maxSize = RECV_DATA_MAX_SIZE - receiveBufPos;
            int count;

            try
            {
                count = Socket.Receive(receiveBuf, receiveBufPos, maxSize, SocketFlags.None);
            }
            catch
            {
                return false;
            }

            if (count == 0)
            {
                ReceivedData receivedData = new()
                {
                    DataType = ReceivedDataType.CloseConnestion,
                    Data = null
                };
                ReceivedDataQueue.Enqueue(receivedData);
                return true;
            }

            receiveBufPos += count;
            if (receiveBufPos >= RECV_DATA_MAX_SIZE)
                return false;

            do
            {
                bool getDataSize = receiveBufPos < sizeof(ushort);
                if (getDataSize)
                {
                    // Ещё получены не все байты, определяющие размер принимаемых данных.
                    return true;
                }

                if (dataSize == -1)
                    dataSize = receiveBuf[0] + receiveBuf[1] * 256;

                bool allDataReceived = receiveBufPos >= dataSize + sizeof(ushort);
                if (!allDataReceived)
                {
                    // Ещё получены не все данные. Возврат для ожидания новой порции.
                    return true;
                }

                byte[] data = new byte[dataSize];
                Buffer.BlockCopy(receiveBuf, sizeof(ushort), data, 0, dataSize);

                ReceivedDataQueue.Enqueue(new()
                {
                    DataType = ReceivedDataType.RegisterClient,
                    Data = data
                });

                byte[] newReciveBuf = new byte[RECV_DATA_MAX_SIZE];

                int copyBytes = receiveBufPos - dataSize - sizeof(ushort);
                if (copyBytes > 0)
                    Buffer.BlockCopy(receiveBuf, dataSize + sizeof(ushort), newReciveBuf, 0, copyBytes);

                receiveBuf = newReciveBuf;
                receiveBufPos = copyBytes;
            } while (receiveBufPos > 0);

            return true;
        }

        public bool RegisterClient(byte[] registerData)
        {
            if (registerData == null)
                return false;

            using MemoryStream stream = new(registerData);
            using BinaryReader reader = new(stream);

            ushort hashSize = reader.ReadUInt16();
            if (hashSize == 0 || hashSize > registerData.Length)
                return false;

            byte[] verifiedHash = reader.ReadBytes(hashSize);

            try
            {
                int dataOffset = sizeof(ushort) + hashSize;
                int dataSize = registerData.Length - dataOffset;

                string appId = ReadStringFromBuf(reader);
                if (!CheckConnectDataHash(appId, verifiedHash, registerData, dataOffset, dataSize))
                    return false;

                IbId = ReadStringFromBuf(reader);
                UserId = ReadStringFromBuf(reader);
                UserGroup = ReadStringFromBuf(reader);
            }
            catch (Exception e)
            {
                logger.LogWarning("Произошла ошибка при регистрации клиента: {message}", e.Message);
                return false;
            }

            return true;
        }

        private static bool CheckConnectDataHash(string appId, byte[] verifiedHash, byte[] data, int offset, int count)
        {
            var clientKey = Program.ClientAppsStorage.GetApp(appId)?.ClientKey;
            if (clientKey == null)
                return false;

            using HMACSHA256 hmac = new(clientKey);
            byte[] computedHash = hmac.ComputeHash(data, offset, count);

            if (computedHash.Length != verifiedHash.Length)
                return false;

            for (int i = 0; i < computedHash.Length; i++)
            {
                if (computedHash[i] != verifiedHash[i])
                    return false;
            }

            return true;
        }

        private static string ReadStringFromBuf(BinaryReader buf)
        {
            StringBuilder stringBuilder = new();
            while (true)
            {
                char ch = buf.ReadChar();
                if (ch == 0)
                    break;

                stringBuilder.Append(ch);
            }

            return stringBuilder.ToString();
        }
    }
}
