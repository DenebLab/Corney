using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Corney.Common.Bootstrap
{
    public static class IdentHelper
    {
        public static string GetNodeKey(string exeDir, string machineName, string semVersion = "")
        {
            var key1 = RemoveNonAscii($"{exeDir}{machineName}{semVersion}");
            var key2 = key1.ToLower();
            var id = Sha256(key2);
            var sub = id.Substring(0, 10);
            var m1 = string.Join("", machineName.Where(char.IsLetterOrDigit)).ToLowerInvariant();
            var v1 = string.Join("", semVersion.Where(char.IsLetterOrDigit)).ToLowerInvariant();
            var key3 = $"node-{sub}-{m1}-{v1}";
            return key3;
        }


        private static string Sha256(string randomString)
        {
            var crypt = new SHA256Managed();
            var hash = new StringBuilder();
            var crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(randomString));
            foreach (var theByte in crypto) hash.Append(theByte.ToString("x2"));
            return hash.ToString();
        }

        private static string RemoveNonAscii(string s, char toReplace = '-')
        {
            var sb = new StringBuilder();

            foreach (var c in s)
                if (c >= 32 && c <= 175)
                    sb.Append(c);
                else
                    sb.Append(toReplace);

            return sb.ToString();
        }
    }
}