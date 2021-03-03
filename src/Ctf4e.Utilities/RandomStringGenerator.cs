using System.Linq;
using System.Security.Cryptography;

namespace Ctf4e.Utilities
{
    public static class RandomStringGenerator
    {
        public static string GetRandomString(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz2345689";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[RandomNumberGenerator.GetInt32(s.Length)]).ToArray());
        }
    }
}