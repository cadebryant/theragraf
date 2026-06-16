namespace Theragraf.Functions.Helpers;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Provides server-side namespacing for client identifiers to prevent cross-therapist
/// data collisions in the shared Cosmos DB container.
///
/// Problem: <c>clientId</c> is free text typed by the therapist (e.g. "jane-doe").
/// Two therapists can enter the same value for completely different patients, causing
/// their sessions to land in the same Cosmos partition and statistics queries to bleed
/// across therapist boundaries — a PHI exposure risk.
///
/// Solution: prefix the user-entered value with an 8-character hex digest of the
/// therapist's email/UPN, separated by <c>~</c>:
///   "jane-doe"  +  "alice@hospital.org"  →  "a1b2c3d4~jane-doe"
///
/// The prefix is derived from SHA-256 so it is deterministic, consistent across
/// requests, and contains no PII itself.  The <c>~</c> separator is chosen because
/// it is URL-safe (RFC 3986 unreserved) and not commonly present in patient identifiers.
///
/// Demo records (stored by SeedFunction with pre-set client IDs) are intentionally
/// unnamespaced so they are shared across all authenticated users.  All callers must
/// use <see cref="IsDemo"/> to skip namespacing for these records.
/// </summary>
public static class ClientIdHelper
{
    /// <summary>Separator between the therapist namespace prefix and the user-entered id.</summary>
    public const char Separator = '~';

    /// <summary>
    /// Length of the hex-encoded namespace prefix (4 bytes = 8 hex chars).
    /// Short enough to be readable in URLs, long enough to avoid accidental collisions.
    /// </summary>
    private const int PrefixBytes = 4;

    /// <summary>
    /// Returns a namespaced client ID by prefixing <paramref name="rawClientId"/> with
    /// an 8-char hex digest of <paramref name="therapistEmail"/>.
    ///
    /// If <paramref name="therapistEmail"/> is null or empty (local dev with auth disabled),
    /// returns <paramref name="rawClientId"/> unchanged so existing local records are unaffected.
    ///
    /// If <paramref name="rawClientId"/> already contains the separator it is returned as-is
    /// (idempotent — safe to call on already-namespaced IDs).
    /// </summary>
    public static string Namespace(string? therapistEmail, string rawClientId)
    {
        if (string.IsNullOrWhiteSpace(therapistEmail))
            return rawClientId;

        // Idempotent: if the id already carries a namespace prefix, leave it alone.
        if (rawClientId.Contains(Separator))
            return rawClientId;

        var prefix = ComputePrefix(therapistEmail);
        return $"{prefix}{Separator}{rawClientId}";
    }

    /// <summary>
    /// Strips the namespace prefix from a stored client ID, returning only the
    /// user-entered portion suitable for display in the UI.
    /// Returns <paramref name="namespacedId"/> unchanged if it contains no separator
    /// (e.g. demo records, local dev records, already-stripped values).
    /// </summary>
    public static string StripPrefix(string namespacedId)
    {
        var idx = namespacedId.IndexOf(Separator);
        return idx >= 0 ? namespacedId[(idx + 1)..] : namespacedId;
    }

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="clientId"/> looks like a
    /// demo record ID (no namespace prefix). Used as a quick heuristic alongside
    /// <c>IsDemoRecord</c> on the session document itself.
    /// </summary>
    public static bool IsNamespaced(string clientId) => clientId.Contains(Separator);

    /// <summary>
    /// Returns <see langword="true"/> when the namespace prefix embedded in
    /// <paramref name="clientId"/> matches the prefix that would be derived from
    /// <paramref name="therapistEmail"/>, or when <paramref name="clientId"/> has
    /// no prefix (demo / local-dev record — accessible to everyone).
    /// </summary>
    public static bool IsOwner(string therapistEmail, string clientId)
    {
        if (!IsNamespaced(clientId))
            return true; // demo / unnamespaced record

        var expectedPrefix = ComputePrefix(therapistEmail);
        var actualPrefix   = clientId[..clientId.IndexOf(Separator)];
        return string.Equals(expectedPrefix, actualPrefix, StringComparison.OrdinalIgnoreCase);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static string ComputePrefix(string therapistEmail)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(therapistEmail.ToLowerInvariant()));
        return Convert.ToHexString(bytes[..PrefixBytes]).ToLowerInvariant();
    }
}
