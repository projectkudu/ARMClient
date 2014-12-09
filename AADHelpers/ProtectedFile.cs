using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AADHelpers
{
    public static class ProtectedFile
    {
        public static string ReadAllText(string file)
        {
            var bytes = File.ReadAllBytes(file);
            bytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }

        public static void WriteAllText(string file, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content);
            bytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(file, bytes);
        }
    }
}
