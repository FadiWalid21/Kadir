export interface GoogleCredentialClaims {
  email: string | null;
  name: string | null;
}

/**
 * Reads display-only claims (email, name) from a Google ID token for prefilling the UI.
 * The signature is NOT verified here and the values are never trusted: the API validates
 * the token and is the only source of identity.
 */
export function readGoogleCredentialClaims(credential: string): GoogleCredentialClaims | null {
  const parts = credential.split('.');
  if (parts.length < 2 || !parts[1]) {
    return null;
  }
  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const bytes = Uint8Array.from(atob(padded), (c) => c.charCodeAt(0));
    const payload = JSON.parse(new TextDecoder().decode(bytes)) as Record<string, unknown>;
    return {
      email: typeof payload['email'] === 'string' ? payload['email'] : null,
      name: typeof payload['name'] === 'string' ? payload['name'] : null,
    };
  } catch {
    return null;
  }
}
