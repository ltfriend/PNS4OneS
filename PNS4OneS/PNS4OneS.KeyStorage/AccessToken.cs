using System;

namespace PNS4OneS.KeyStorage
{
    public class AccessToken
    {
        public string Token { get; private set; }
        public DateTime ExpiresIn { get; private set; }

        public static AccessToken NewToken()
        {
            return new AccessToken();
        }

        private AccessToken()
        {
            Token = Utils.GenerateKey(64);
            ExpiresIn = DateTime.UtcNow.AddMinutes(60);
        }

        public AccessToken(string token, long expiresIn)
        {
            Token = token;
            ExpiresIn = new DateTime(expiresIn);
        }
    }
}
