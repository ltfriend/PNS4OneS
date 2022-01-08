using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.Extensions.Logging;
using PNS4OneS.KeyStorage;

namespace PNS4OneS
{
    public static class AdminAccount
    {
        public static bool IsExists { get; set; } = false;

        private static string userName = "";
        private static string passwordHash = "";
        private static string saltBase64 = "";
        private static string sessionId = "";
        private static DateTime sessionExpiresIn;

        public static async Task<string> CreateAccount(string name, string password)
        {
            userName = name;
            HashPassword(password);

            await SaveAccount();
            SetSessionId();

            IsExists = true;
            return sessionId;
        }

        public static async Task<string> ChangePassword(string password)
        {
            HashPassword(password);
            await SaveAccount();
            SetSessionId();
            return sessionId;
        }

        public static string Auth(string name, string password)
        {
            if (string.IsNullOrEmpty(name) || name != userName)
                return "";
            else if (!CheckPassword(password))
                return "";

            SetSessionId();
            return sessionId;
        }

        public static bool CheckSessionId(string checkedSessionId) =>
            !string.IsNullOrEmpty(checkedSessionId)
                && sessionId == checkedSessionId
                && DateTime.UtcNow <= sessionExpiresIn;

        public static bool CheckPassword(string password)
        {
            byte[] salt = Convert.FromBase64String(saltBase64);
            string hashed = CalcPasswordHash(password, salt);
            return passwordHash == hashed;
        }

        public static void ReadAccount(ILogger logger)
        {
            string filePath = GetAccountFilePath();

            if (!File.Exists(filePath))
            {
                IsExists = false;
                return;
            }

            try
            {
                using var reader = File.OpenText(filePath);

                userName = reader.ReadLine();
                saltBase64 = reader.ReadLine();
                passwordHash = reader.ReadLine();

                IsExists = true;
            }
            catch (Exception e)
            {
                logger.LogError("Произошла ошибка при чтении учетной записи администратора: {message}", e.Message);
                return;
            }
        }

        private static async Task SaveAccount()
        {
            string filePath = GetAccountFilePath();
            using var writer = File.CreateText(filePath);
            await writer.WriteLineAsync(userName);
            await writer.WriteLineAsync(saltBase64);
            await writer.WriteLineAsync(passwordHash);
        }

        private static string GetAccountFilePath()
        {
            string confLocale = Utils.GetConfFilesLocale();
            string filePath = Path.Combine(confLocale, "account");
            return filePath;

        }

        private static void HashPassword(string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            saltBase64 = Convert.ToBase64String(salt);
            passwordHash = CalcPasswordHash(password, salt);
        }

        private static string CalcPasswordHash(string password, byte[] salt) =>
            Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password,
                salt,
                KeyDerivationPrf.HMACSHA256,
                100000,
                32));

        private static void SetSessionId()
        {
            sessionId = Utils.GenerateKey(64);
            sessionExpiresIn = DateTime.UtcNow.AddMinutes(60);
        }
    }
}
