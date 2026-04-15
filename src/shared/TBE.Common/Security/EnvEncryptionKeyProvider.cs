using Microsoft.Extensions.Options;

namespace TBE.Common.Security;

/// <summary>
/// Default <see cref="IEncryptionKeyProvider"/> backed by <see cref="EncryptionOptions"/>
/// (bound from config / <c>.env</c>). Fails fast at construction if the active key is missing
/// or not exactly 32 bytes — no plaintext ever flows through a misconfigured service.
/// </summary>
public sealed class EnvEncryptionKeyProvider : IEncryptionKeyProvider
{
    private const int Aes256KeyBytes = 32;
    private readonly byte _activeVersion;
    private readonly Dictionary<byte, byte[]> _keysByVersion;

    public EnvEncryptionKeyProvider(IOptions<EncryptionOptions> options)
    {
        var opts = options.Value ?? throw new ArgumentNullException(nameof(options));
        _activeVersion = opts.ActiveKeyVersion;
        _keysByVersion = new Dictionary<byte, byte[]>();

        var activeKey = DecodeOrThrow(opts.ActiveKeyBase64, nameof(EncryptionOptions.ActiveKeyBase64));
        _keysByVersion[_activeVersion] = activeKey;

        foreach (var (version, base64) in opts.HistoricalKeysBase64)
        {
            if (version == _activeVersion) continue;
            _keysByVersion[version] = DecodeOrThrow(base64, $"HistoricalKeysBase64[{version}]");
        }
    }

    public byte ActiveKeyVersion => _activeVersion;

    public byte[] GetActiveKey() => _keysByVersion[_activeVersion];

    public byte[]? GetKey(byte version) =>
        _keysByVersion.TryGetValue(version, out var key) ? key : null;

    private static byte[] DecodeOrThrow(string? base64, string name)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException(
                "Encryption:ActiveKeyBase64 must be a Base64-encoded 32-byte AES-256 key");
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                "Encryption:ActiveKeyBase64 must be a Base64-encoded 32-byte AES-256 key");
        }

        if (decoded.Length != Aes256KeyBytes)
        {
            throw new InvalidOperationException(
                "Encryption:ActiveKeyBase64 must be a Base64-encoded 32-byte AES-256 key");
        }

        return decoded;
    }
}
