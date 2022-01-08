namespace PNS4OneS.KeyStorage
{
    public static class Utils
    {
        public static string GetConfFilesLocale()
        {
            if (System.OperatingSystem.IsLinux())
                return "/etc/pns4ones/";

            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;

            for (int i = path.Length - 1; i >= 0; i--)
            {
                if (path[i] == '\\' || path[i] == '/')
                {
                    path = path[..i];
                    break;
                }
            }

            return System.IO.Path.Combine(path, "conf");
        }

        public static string GenerateKey(int keyLen)
        {
            string chars = "0123456789abcdefghjiklmnopqrstuvwxyzABCDEFGHJIKLMNOPQRSTUVWXYZ";
            string key = "";

            System.Random rnd = new();
            int maxNum = chars.Length;

            for (int i = 0; i < keyLen; i++)
                key += chars[rnd.Next(maxNum)];

            return key;
        }
    }
}
