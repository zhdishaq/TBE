using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TBE.Common.Security;
using Xunit;

namespace TBE.Tests.Shared;

[Trait("Category", "Unit")]
public class AesGcmFieldEncryptorTests
{
    private static readonly string ActiveKeyBase64 =
        Convert.ToBase64String(new byte[]
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
        });

    private static AesGcmFieldEncryptor CreateEncryptor(byte version = 1, string? activeKey = null)
    {
        var opts = Options.Create(new EncryptionOptions
        {
            ActiveKeyBase64 = activeKey ?? ActiveKeyBase64,
            ActiveKeyVersion = version,
        });
        return new AesGcmFieldEncryptor(new EnvEncryptionKeyProvider(opts));
    }

    [Theory(DisplayName = "COMP06: round-trip preserves plaintext")]
    [InlineData("Smith")]
    [InlineData("测试")]
    [InlineData("")]
    public void COMP06_roundtrip_preserves_plaintext(string plaintext)
    {
        var enc = CreateEncryptor();
        var envelope = enc.Encrypt(plaintext);
        var recovered = enc.Decrypt(envelope);
        recovered.Should().Be(plaintext);
    }

    [Fact(DisplayName = "COMP06: round-trip preserves 10KB payload")]
    public void COMP06_roundtrip_preserves_large_payload()
    {
        var enc = CreateEncryptor();
        var plaintext = new string('x', 10_000);
        var envelope = enc.Encrypt(plaintext);
        enc.Decrypt(envelope).Should().Be(plaintext);
    }

    [Fact(DisplayName = "COMP06: envelope has expected header + ciphertext length")]
    public void COMP06_envelope_has_expected_structure()
    {
        var enc = CreateEncryptor();
        var plaintext = "Hello";
        var envelope = enc.Encrypt(plaintext);

        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        // 1 version + 12 nonce + 16 tag + plaintext UTF-8 length
        envelope.Length.Should().Be(29 + plainBytes.Length);
        envelope[0].Should().Be(1); // active version
    }

    [Fact(DisplayName = "COMP06: flipped cipher byte throws CryptographicException (tamper rejected)")]
    public void COMP06_flipped_cipher_byte_throws_cryptographic_exception()
    {
        var enc = CreateEncryptor();
        var envelope = enc.Encrypt("sensitive");
        envelope[^1] ^= 0xFF;

        Action act = () => enc.Decrypt(envelope);
        act.Should().Throw<CryptographicException>();
    }

    [Fact(DisplayName = "COMP06: unknown key version throws InvalidOperationException")]
    public void COMP06_unknown_key_version_throws()
    {
        var enc = CreateEncryptor();
        var envelope = enc.Encrypt("x");
        envelope[0] = 99; // unknown version

        Action act = () => enc.Decrypt(envelope);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown encryption key version*");
    }

    [Fact(DisplayName = "COMP06: missing key at startup throws InvalidOperationException")]
    public void COMP06_missing_key_at_startup_throws_invalidoperation()
    {
        var opts = Options.Create(new EncryptionOptions { ActiveKeyBase64 = "", ActiveKeyVersion = 1 });
        Action act = () => _ = new EnvEncryptionKeyProvider(opts);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32-byte AES-256 key*");
    }

    [Fact(DisplayName = "COMP06: non-32-byte key at startup throws InvalidOperationException")]
    public void COMP06_wrong_length_key_throws()
    {
        var opts = Options.Create(new EncryptionOptions
        {
            ActiveKeyBase64 = Convert.ToBase64String(new byte[16]),
            ActiveKeyVersion = 1,
        });
        Action act = () => _ = new EnvEncryptionKeyProvider(opts);
        act.Should().Throw<InvalidOperationException>();
    }
}
