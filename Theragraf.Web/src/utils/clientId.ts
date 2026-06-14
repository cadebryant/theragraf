/**
 * Client ID namespace utilities — mirrors the server-side ClientIdHelper.cs logic.
 *
 * Stored client IDs in Cosmos have the form:
 *   "{8-char-hex-prefix}~{user-entered-id}"
 *   e.g. "a1b2c3d4~jane-doe"
 *
 * Demo records and local-dev records carry no prefix and should pass through unchanged.
 * All display surfaces should call stripClientIdPrefix() before rendering.
 */

const SEPARATOR = '~';

/**
 * Returns the user-visible part of a (possibly namespaced) client ID.
 * Strips everything up to and including the first `~`.
 *
 * Examples:
 *   "a1b2c3d4~jane-doe"  →  "jane-doe"
 *   "jane-doe"           →  "jane-doe"   (demo / local dev — no-op)
 */
export function stripClientIdPrefix(clientId: string): string {
  const idx = clientId.indexOf(SEPARATOR);
  return idx >= 0 ? clientId.slice(idx + 1) : clientId;
}

/**
 * Returns true when the client ID carries a namespace prefix.
 * Useful to decide whether to show a stripped version.
 */
export function isNamespacedClientId(clientId: string): boolean {
  return clientId.includes(SEPARATOR);
}
