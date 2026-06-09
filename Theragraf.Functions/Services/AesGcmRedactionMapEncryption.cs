namespace Theragraf.Functions.Services;

using System.Security.Cryptography;
using System.Text;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Theragraf.Core.Services;

/// <summary>
/// AES-256-GCM envelope encryption for the redaction map.
///
/// Wire format (all concatenated, then base64-encoded):
///   [ 12-byte nonce ][ variable-length ciphertext ][ 16-byte GCM auth tag ]
///
/// The AES-256 key is fetched once from Azure Key Vault on construction and
/// cached for the lifetime of the singleton. No Key Vault round-trip occurs
/// on every encrypt/decrypt call.
///
/// Key Vault secret name: <c>redaction-map-key</c>
/// Secret value: base64-encoded 32 bytes (256 bits) of key material.
/// </summary>
public sealed class AesGcmRedactionMapEncryption : IRedactionMapEncryption
{
    private const string   SecretName  = "redaction-map-key";
    private const int      NonceSize   = 12;   // 96-bit nonce — GCM recommended size
    private const int      TagSize     = 16;   // 128-bit authentication tag

    private readonly byte[] _key;

    /// <param name="vaultUri">
    /// URI of the Azure Key Vault, e.g. <c>https://theragraf-kv-dev.vault.azure.net/</c>.
    /// </param>
    /// <param name="credential">
    /// Azure credential — in production this is the Function App's Managed Identity
    /// via <c>DefaultAzureCredential</c>; in tests it can be substituted.
    /// </param>
    public AesGcmRedactionMapEncryption(Uri vaultUri, Azure.Core.TokenCredential credential)
    {
        var client = new SecretClient(vaultUri, credential);
        var secret = client.GetSecret(SecretName);
        _key = Convert.FromBase64String(secret.Value.Value);

        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"Key Vault secret '{SecretName}' must be 32 bytes (256-bit) base64-encoded. Got {_key.Length} bytes.");
    }

    public bool IsEnabled => true;

    /// <inheritdoc/>
    public string Encrypt(string plaintext)
    {
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce          = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintextBytes.Length];
        var tag        = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Wire format: nonce | ciphertext | tag — all concatenated then base64-encoded.
        var combined = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce,       0, combined, 0,                              NonceSize);
        Buffer.BlockCopy(ciphertext,  0, combined, NonceSize,                      ciphertext.Length);
        Buffer.BlockCopy(tag,         0, combined, NonceSize + ciphertext.Length,  TagSize);

        return Convert.ToBase64String(combined);
    }

    /// <inheritdoc/>
    public string Decrypt(string ciphertext)
    {
        var combined = Convert.FromBase64String(ciphertext);

        if (combined.Length < NonceSize + TagSize)
            throw new CryptographicException("Ciphertext is too short to be a valid encrypted redaction map.");

        var ciphertextLength = combined.Length - NonceSize - TagSize;

        var nonce          = combined.AsSpan(0,                              NonceSize);
        var ciphertextSpan = combined.AsSpan(NonceSize,                      ciphertextLength);
        var tag            = combined.AsSpan(NonceSize + ciphertextLength,   TagSize);

        var plaintextBytes = new byte[ciphertextLength];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertextSpan, tag, plaintextBytes);

        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
