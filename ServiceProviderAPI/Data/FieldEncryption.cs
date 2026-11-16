using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ServiceProviderAPI.Data;

/// <summary>
/// Transparent at-rest encryption for sensitive string columns (Pro/Business payout bank details)
/// using AES-256-GCM. Configured once at startup from a base64 32-byte key.
///
/// Designed to degrade safely:
/// - When no valid key is configured (e.g. unit tests), encrypt/decrypt are no-ops, so values
///   round-trip as plaintext.
/// - Stored values are tagged with a version prefix; a value without the prefix is treated as
///   legacy plaintext, so pre-existing rows keep reading correctly and are re-encrypted on next write.
/// </summary>
public static class FieldEncryption
{
    private const string Prefix = "enc:v1:";
    private static byte[]? _key;

    /// <summary>Set the AES-256 key (base64 of 32 bytes). Invalid/empty leaves encryption disabled.</summary>
    public static void Configure(string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(base64Key)) return;
        try
        {
            var key = Convert.FromBase64String(base64Key);
            if (key.Length == 32) _key = key;
        }
        catch (FormatException) { /* malformed key → stay disabled */ }
    }

    /// <summary>EF value converter: encrypts on write, decrypts on read. EF skips null values.</summary>
    public static readonly ValueConverter<string, string> Converter =
        new(v => Encrypt(v), v => Decrypt(v));

    private static string Encrypt(string plaintext)
    {
        if (_key == null || string.IsNullOrEmpty(plaintext)) return plaintext;

        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize); // 12 bytes
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var combined = new byte[nonce.Length + tag.Length + cipher.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipher, 0, combined, nonce.Length + tag.Length, cipher.Length);

        return Prefix + Convert.ToBase64String(combined);
    }

    private static string Decrypt(string stored)
    {
        if (string.IsNullOrEmpty(stored) || !stored.StartsWith(Prefix, StringComparison.Ordinal))
            return stored;            // legacy plaintext (or empty)
        if (_key == null)
            return stored;            // key not configured — surface as-is rather than throw

        var combined = Convert.FromBase64String(stored[Prefix.Length..]);
        int nonceLen = AesGcm.NonceByteSizes.MaxSize;
        int tagLen = AesGcm.TagByteSizes.MaxSize;

        var nonce = combined.AsSpan(0, nonceLen);
        var tag = combined.AsSpan(nonceLen, tagLen);
        var cipher = combined.AsSpan(nonceLen + tagLen);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(_key, tagLen);
        aes.Decrypt(nonce, cipher, tag, plain);
        return Encoding.UTF8.GetString(plain);
    }
}
