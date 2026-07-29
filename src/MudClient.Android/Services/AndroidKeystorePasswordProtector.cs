using System.Text;
using System.Security.Cryptography;
using Android.Security.Keystore;
using Java.Security;
using Javax.Crypto;
using MudClient.App.Services;

namespace MudClient.Android.Services;

public sealed class AndroidKeystorePasswordProtector : IPasswordProtector
{
    private const string KeyAlias = "KillerMudClient.AccountPassword.v1";
    private const string Transformation = "AES/GCM/NoPadding";
    private const byte FormatVersion = 1;

    public string Protect(string? plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return string.Empty;
        }

        using var cipher = Cipher.GetInstance(Transformation)
            ?? throw new CryptographicException("Android nie udostępnił szyfru AES/GCM.");
        cipher.Init(Javax.Crypto.CipherMode.EncryptMode, GetOrCreateKey());

        var initializationVector = cipher.GetIV()
            ?? throw new CryptographicException("Android nie utworzył wektora inicjalizującego.");
        var ciphertext = cipher.DoFinal(Encoding.UTF8.GetBytes(plainText))
            ?? throw new CryptographicException("Android nie zaszyfrował hasła.");
        if (initializationVector.Length > byte.MaxValue)
        {
            throw new CryptographicException("Wektor inicjalizujący Android Keystore jest zbyt długi.");
        }

        var payload = new byte[2 + initializationVector.Length + ciphertext.Length];
        payload[0] = FormatVersion;
        payload[1] = (byte)initializationVector.Length;
        initializationVector.CopyTo(payload, 2);
        ciphertext.CopyTo(payload, 2 + initializationVector.Length);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string? protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return string.Empty;
        }

        try
        {
            var payload = Convert.FromBase64String(protectedText);
            if (payload.Length < 3 || payload[0] != FormatVersion)
            {
                return string.Empty;
            }

            var initializationVectorLength = payload[1];
            if (initializationVectorLength == 0
                || payload.Length <= 2 + initializationVectorLength)
            {
                return string.Empty;
            }

            var initializationVector = payload.AsSpan(2, initializationVectorLength).ToArray();
            var ciphertext = payload.AsSpan(2 + initializationVectorLength).ToArray();

            using var cipher = Cipher.GetInstance(Transformation)
                ?? throw new CryptographicException("Android nie udostępnił szyfru AES/GCM.");
            using var parameters = new Javax.Crypto.Spec.GCMParameterSpec(
                128,
                initializationVector);
            cipher.Init(Javax.Crypto.CipherMode.DecryptMode, GetOrCreateKey(), parameters);
            var plaintext = cipher.DoFinal(ciphertext);
            return plaintext is null ? string.Empty : Encoding.UTF8.GetString(plaintext);
        }
        catch (Exception exception) when (exception is FormatException
            or CryptographicException
            or GeneralSecurityException)
        {
            // A damaged profile or a key invalidated by Android must not crash startup.
            // Returning empty makes the profile picker request a replacement password.
            return string.Empty;
        }
    }

    private static IKey GetOrCreateKey()
    {
        using var keyStore = KeyStore.GetInstance("AndroidKeyStore")
            ?? throw new CryptographicException("Android Keystore jest niedostępny.");
        keyStore.Load(null);

        if (!keyStore.ContainsAlias(KeyAlias))
        {
            using var generator = KeyGenerator.GetInstance(
                KeyProperties.KeyAlgorithmAes,
                "AndroidKeyStore")
                ?? throw new CryptographicException("Generator klucza Android Keystore jest niedostępny.");
            using var specification = new KeyGenParameterSpec.Builder(
                    KeyAlias,
                    KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
                .SetBlockModes(KeyProperties.BlockModeGcm)
                .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
                .Build();
            generator.Init(specification);
            generator.GenerateKey();
        }

        return keyStore.GetKey(KeyAlias, null)
            ?? throw new CryptographicException("Nie udało się odczytać klucza Android Keystore.");
    }
}
