using System.Security.Cryptography;
using System.Text;

namespace MudClient.App.Services;

/// <summary>
/// Encrypts account passwords at rest using Windows DPAPI (per-user scope),
/// so the profile JSON never contains the password in plain text.
/// </summary>
public static class PasswordProtector
{
    // Binds ciphertexts to this app so other DPAPI consumers can't decrypt them by accident.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KillerMudClient.AccountPassword.v1");

    /// <summary>Returns the encrypted password as base64, or empty for an empty password.</summary>
    public static string Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText) || !OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        var cipher = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipher);
    }

    /// <summary>Returns the decrypted password, or empty when the value is missing or unreadable.</summary>
    public static string Unprotect(string? encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64) || !OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        try
        {
            var plain = ProtectedData.Unprotect(
                Convert.FromBase64String(encryptedBase64), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return string.Empty;
        }
    }
}
