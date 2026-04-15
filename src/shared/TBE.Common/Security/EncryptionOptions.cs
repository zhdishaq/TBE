namespace TBE.Common.Security;

/// <summary>
/// Options for AES-256 field encryption. Bound from the <c>Encryption</c> configuration section,
/// ultimately sourced from <c>.env</c> / environment variables per COMP-05.
/// </summary>
public sealed class EncryptionOptions
{
    /// <summary>Base64-encoded active 32-byte AES-256 key.</summary>
    public string ActiveKeyBase64 { get; set; } = string.Empty;

    /// <summary>Version byte emitted as the first byte of every encryption envelope (for rotation).</summary>
    public byte ActiveKeyVersion { get; set; } = 1;

    /// <summary>Historical keys keyed by their version byte — used only for decrypting legacy rows.</summary>
    public Dictionary<byte, string> HistoricalKeysBase64 { get; set; } = new();
}
