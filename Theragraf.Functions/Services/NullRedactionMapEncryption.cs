namespace Theragraf.Functions.Services;

using Theragraf.Core.Services;

/// <summary>
/// No-op implementation of <see cref="IRedactionMapEncryption"/> used when
/// <c>KeyVault:VaultUri</c> is not configured (local development against the
/// Cosmos Emulator). Returns plaintext unchanged in both directions.
/// </summary>
public sealed class NullRedactionMapEncryption : IRedactionMapEncryption
{
    public bool IsEnabled => false;

    public string Encrypt(string plaintext) => plaintext;

    public string Decrypt(string ciphertext) => ciphertext;
}
