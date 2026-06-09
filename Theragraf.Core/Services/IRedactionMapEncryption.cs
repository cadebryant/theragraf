namespace Theragraf.Core.Services;

/// <summary>
/// Encrypts and decrypts the redaction map (placeholder → PII text) before it is
/// written to or read from persistent storage.
///
/// Implementations:
///   • <c>AesGcmRedactionMapEncryption</c>  — AES-256-GCM using a key from Azure Key Vault (production)
///   • <c>NullRedactionMapEncryption</c>     — pass-through; used when no Key Vault URI is configured (local dev)
/// </summary>
public interface IRedactionMapEncryption
{
    /// <summary>
    /// Returns <see langword="true"/> when this instance actually encrypts data.
    /// <see langword="false"/> for the null/pass-through implementation.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> and returns a base64-encoded ciphertext string
    /// containing the nonce, ciphertext, and authentication tag.
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a value previously produced by <see cref="Encrypt"/>.
    /// </summary>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown when the authentication tag does not match (tampered or wrong key).
    /// </exception>
    string Decrypt(string ciphertext);
}
