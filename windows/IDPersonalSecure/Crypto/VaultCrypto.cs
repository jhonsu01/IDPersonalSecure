using System.Security.Cryptography;
using System.Text;

namespace IDPersonalSecure.Crypto;

/// <summary>
/// Implementación del formato criptográfico canónico definido en docs/CRYPTO.md.
/// DEBE mantenerse idéntica a la implementación de Android (VaultCrypto.kt).
///
/// EncBlob = nonce(12) || ciphertext(n) || tag(16)
/// </summary>
public static class VaultCrypto
{
    private const int Iterations = 210_000;
    private const int KeyBytes = 32;   // AES-256
    private const int NonceBytes = 12; // GCM nonce
    private const int TagBytes = 16;   // GCM tag (128 bits)
    private const int SaltBytes = 16;

    public sealed record Keys(byte[] Enc, byte[] Mac);

    public static byte[] RandomBytes(int n) => RandomNumberGenerator.GetBytes(n);
    public static byte[] NewSalt() => RandomBytes(SaltBytes);

    /// <summary>Deriva 64 bytes: [0..32) encKey (AES), [32..64) macKey (HMAC).</summary>
    public static Keys DeriveKeys(string pin, byte[] salt)
    {
        byte[] dk = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin), salt, Iterations, HashAlgorithmName.SHA256, KeyBytes * 2);
        return new Keys(dk[..KeyBytes], dk[KeyBytes..(KeyBytes * 2)]);
    }

    public static byte[] Encrypt(byte[] plain, byte[] encKey, string aad)
    {
        byte[] nonce = RandomBytes(NonceBytes);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[TagBytes];
        using var gcm = new AesGcm(encKey, TagBytes);
        gcm.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(aad));

        byte[] blob = new byte[NonceBytes + cipher.Length + TagBytes];
        Buffer.BlockCopy(nonce, 0, blob, 0, NonceBytes);
        Buffer.BlockCopy(cipher, 0, blob, NonceBytes, cipher.Length);
        Buffer.BlockCopy(tag, 0, blob, NonceBytes + cipher.Length, TagBytes);
        return blob;
    }

    public static byte[] Decrypt(byte[] blob, byte[] encKey, string aad)
    {
        byte[] nonce = blob[..NonceBytes];
        byte[] tag = blob[^TagBytes..];
        byte[] cipher = blob[NonceBytes..^TagBytes];
        byte[] plain = new byte[cipher.Length];
        using var gcm = new AesGcm(encKey, TagBytes);
        gcm.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(aad));
        return plain;
    }

    public static byte[] Hmac(byte[] macKey, byte[] data)
    {
        using var h = new HMACSHA256(macKey);
        return h.ComputeHash(data);
    }

    public static string B64(byte[] data) => Convert.ToBase64String(data);
    public static byte[] Unb64(string s) => Convert.FromBase64String(s);

    public static bool ConstantTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
