using System.Security.Cryptography;
using System.Text;

namespace TBE.Common.Security;

/// <summary>
/// AES-256-GCM authenticated field encryptor. Envelope layout:
///   byte[0]     = key version (from <see cref="IEncryptionKeyProvider.ActiveKeyVersion"/>)
///   byte[1..13) = 12-byte random nonce (GCM standard)
///   byte[13..29)= 16-byte authentication tag
///   byte[29..N) = ciphertext (same length as plaintext UTF-8 bytes)
///
/// This is the primitive only — Phase 4 (per W5 / D-20) adopts it for the Passenger.PassportNumber
/// column. Phase 3 ships round-trip + tamper-rejection tests; no callers yet.
/// </summary>
public sealed class AesGcmFieldEncryptor
{
    internal const int VersionBytes = 1;
    internal const int NonceBytes = 12;  // GCM standard
    internal const int TagBytes = 16;    // GCM standard
    internal const int HeaderBytes = VersionBytes + NonceBytes + TagBytes; // 29

    private readonly IEncryptionKeyProvider _keys;

    public AesGcmFieldEncryptor(IEncryptionKeyProvider keys)
    {
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
    }

    /// <summary>Encrypts the UTF-8 bytes of <paramref name="plaintext"/> under the active key.</summary>
    public byte[] Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        var version = _keys.ActiveKeyVersion;
        var key = _keys.GetActiveKey();
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);

        var envelope = new byte[HeaderBytes + plainBytes.Length];
        envelope[0] = version;
        var nonce = envelope.AsSpan(VersionBytes, NonceBytes);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(key, TagBytes);
        aes.Encrypt(
            nonce,
            plainBytes,
            envelope.AsSpan(HeaderBytes, plainBytes.Length),
            envelope.AsSpan(VersionBytes + NonceBytes, TagBytes));

        return envelope;
    }

    /// <summary>
    /// Decrypts an envelope produced by <see cref="Encrypt"/>. Throws
    /// <see cref="InvalidOperationException"/> if the envelope references an unknown key version,
    /// <see cref="CryptographicException"/> on authentication-tag mismatch (tampering / wrong key).
    /// </summary>
    public string Decrypt(byte[] envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.Length < HeaderBytes)
        {
            throw new CryptographicException("Encryption envelope is too short");
        }

        var version = envelope[0];
        var key = _keys.GetKey(version)
            ?? throw new InvalidOperationException($"Unknown encryption key version {version}");

        var nonce = envelope.AsSpan(VersionBytes, NonceBytes);
        var tag = envelope.AsSpan(VersionBytes + NonceBytes, TagBytes);
        var cipher = envelope.AsSpan(HeaderBytes);
        var plain = new byte[cipher.Length];

        using var aes = new AesGcm(key, TagBytes);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
