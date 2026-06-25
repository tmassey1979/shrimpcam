using System.Security.Cryptography;
using System.Text;

namespace ShrimpCam.Core.Authentication;

public static class SessionTokenHasher
{
    public static string ComputeHash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
