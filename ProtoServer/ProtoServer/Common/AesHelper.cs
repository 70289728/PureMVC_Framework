using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// AES-128-CBC encryption/decryption for password transport protection.
/// Must match the client-side AesHelper key/IV exactly.
/// </summary>
public static class AesHelper
{
    // 16 bytes = AES-128 — must match client-side AesHelper
    private static readonly byte[] Key = {
        0x4C, 0x75, 0x61, 0x48, 0x6F, 0x74, 0x55, 0x70,
        0x64, 0x61, 0x74, 0x65, 0x4B, 0x65, 0x79, 0x21
    };

    private static readonly byte[] IV = {
        0x4C, 0x75, 0x61, 0x53, 0x63, 0x72, 0x69, 0x70,
        0x74, 0x73, 0x49, 0x56, 0x31, 0x32, 0x33, 0x34
    };

    public static byte[] Encrypt(byte[] plainBytes)
    {
        if (plainBytes == null || plainBytes.Length == 0) return null;

        using (var aes = Aes.Create())
        {
            aes.Key = Key;
            aes.IV = IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var ms = new MemoryStream())
            using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();
                return ms.ToArray();
            }
        }
    }

    public static byte[] Decrypt(byte[] cipherBytes)
    {
        if (cipherBytes == null || cipherBytes.Length == 0) return null;

        using (var aes = Aes.Create())
        {
            aes.Key = Key;
            aes.IV = IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var ms = new MemoryStream(cipherBytes))
            using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (var result = new MemoryStream())
            {
                cs.CopyTo(result);
                return result.ToArray();
            }
        }
    }

    /// <summary>
    /// Encrypt a plaintext string → Base64-encoded ciphertext.
    /// </summary>
    public static string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return null;
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var encrypted = Encrypt(bytes);
        return Convert.ToBase64String(encrypted);
    }

    /// <summary>
    /// Decrypt a Base64-encoded ciphertext → plaintext string.
    /// </summary>
    public static string DecryptString(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return null;
        var bytes = Convert.FromBase64String(cipherText);
        var decrypted = Decrypt(bytes);
        return System.Text.Encoding.UTF8.GetString(decrypted);
    }
}
