using System.Security.Cryptography;
using System.Text;

namespace FileEncrypter.Helpers
{
    public static class HashHelper
    {
        public static string ComputeHash(string input)
        {
            using var sha256 = SHA256.Create();
            byte[] data = Encoding.UTF8.GetBytes(input);
            byte[] hash = sha256.ComputeHash(data);
            var sb = new StringBuilder();
            foreach (var b in hash)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
