using System;
using System.IO;
using System.Security.Cryptography;

namespace PNS4OneS.KeyStorage
{
    public class ClientApplication
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ServerKey { get; set; }
        public byte[] ClientKey { get; private set; }
        public byte[] ClientIV { get; private set; }
        public AccessToken AccessToken { get; set; }

        public string ClientKeyBase64
        {
            get
            {
                byte[] keyData = new byte[ClientKey.Length + ClientIV.Length + sizeof(Int32) * 2];
                int pos = 0;

                Buffer.BlockCopy(BitConverter.GetBytes(ClientKey.Length), 0, keyData, pos, sizeof(Int32));
                pos += sizeof(Int32);

                Buffer.BlockCopy(BitConverter.GetBytes(ClientIV.Length), 0, keyData, pos, sizeof(Int32));
                pos += sizeof(Int32);

                Buffer.BlockCopy(ClientKey, 0, keyData, pos, count: ClientKey.Length);
                pos += ClientKey.Length;

                Buffer.BlockCopy(ClientIV, 0, keyData, pos, count: ClientIV.Length);

                return Convert.ToBase64String(keyData);
            }

            set
            {
                byte[] keyData = Convert.FromBase64String(value);

                using MemoryStream stream = new(keyData);
                using BinaryReader reader = new(stream);

                int keyLen = reader.ReadInt32();
                int ivLen = reader.ReadInt32();

                ClientKey = reader.ReadBytes(keyLen);
                ClientIV = reader.ReadBytes(ivLen);
            }
        }

        public static ClientApplication NewApp(string appName)
        {
            ClientApplication app = new()
            {
                Id = Guid.NewGuid().ToString(),
                Title = appName,
                AccessToken = null
            };

            app.GenerateServerKey();
            app.GenerateClientKey();

            return app;
        }

        public void UpdateServerKey()
        {
            GenerateServerKey();
            ResetAccessToken();
        }

        public void UpdateClientKey()
        {
            GenerateClientKey();
        }

        public void UpdateAccessToken()
        {
            AccessToken = AccessToken.NewToken();
        }

        public void ResetAccessToken()
        {
            AccessToken = null;
        }

        private void GenerateServerKey()
        {
            ServerKey = Utils.GenerateKey(64);
        }

        private void GenerateClientKey()
        {
            using Aes aes = Aes.Create();
            ClientKey = aes.Key;
            ClientIV = aes.IV;
        }
    }
}
