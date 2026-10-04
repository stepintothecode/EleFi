using System.Security.Cryptography;

namespace EleFi.App.Services;

/// <summary>
/// Supplies the SQLCipher key, generating one on first run.
/// </summary>
/// <remarks>
/// <para>
/// The key lives in <see cref="SecureStorage"/>, which on Android is backed by the
/// hardware-backed keystore through EncryptedSharedPreferences (NFR-5.3). It is never in
/// configuration, never in the repository, and never derived from anything guessable.
/// </para>
/// <para>
/// Losing this key means losing the database. That is the correct trade for a local-first
/// app: the alternative is a key we could recover, which means a key an attacker with the
/// device could recover too. The Drive backup (a separate, passphrase-derived key) is what
/// makes device loss survivable, not this.
/// </para>
/// </remarks>
public static class DatabaseKey
{
    private const string StorageKey = "elefi.db.key.v1";

    /// <summary>
    /// Reads the key, creating a 256-bit random one on first run.
    /// </summary>
    /// <remarks>
    /// Base64 rather than raw bytes because the key travels as a connection-string value.
    /// </remarks>
    public static async Task<string> GetOrCreateAsync()
    {
        var existing = await SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(existing))
        {
            return existing;
        }

        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await SecureStorage.Default.SetAsync(StorageKey, key).ConfigureAwait(false);

        return key;
    }
}
