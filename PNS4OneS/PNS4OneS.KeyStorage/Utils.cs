using System;
using System.IO;

namespace PNS4OneS.KeyStorage
{
    public static class Utils
    {
        public static string GetConfFilesLocale()
        {
            if (OperatingSystem.IsLinux())
            {
                return "/etc/pns4ones/";
            }
            else
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\PNS4OneS";

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
        }

        public static string GenerateKey(int keyLen)
        {
            string chars = "0123456789abcdefghjiklmnopqrstuvwxyzABCDEFGHJIKLMNOPQRSTUVWXYZ";
            string key = "";

            Random rnd = new();
            int maxNum = chars.Length;

            for (int i = 0; i < keyLen; i++)
                key += chars[rnd.Next(maxNum)];

            return key;
        }
    }
}
