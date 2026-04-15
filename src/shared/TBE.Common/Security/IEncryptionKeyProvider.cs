namespace TBE.Common.Security;

/// <summary>
/// Supplies AES-256 keys to <see cref="AesGcmFieldEncryptor"/>.
/// <see cref="ActiveKeyVersion"/> is emitted as the first byte of every ciphertext envelope so
/// ciphertexts produced under a rotated-out key can still be decrypted by looking the historical
/// key up via <see cref="GetKey(byte)"/>.
/// </summary>
public interface IEncryptionKeyProvider
{
    /// <summary>Version byte emitted in the envelope. Must match the byte prefix of every new ciphertext.</summary>
    byte ActiveKeyVersion { get; }

    /// <summary>Returns the currently active key. Always 32 bytes (AES-256).</summary>
    byte[] GetActiveKey();

    /// <summary>Looks up a historical (or the active) key by its version byte. Returns <c>null</c> if unknown.</summary>
    byte[]? GetKey(byte version);
}
