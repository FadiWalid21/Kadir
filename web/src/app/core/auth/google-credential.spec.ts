import { describe, expect, it } from 'vitest';
import { readGoogleCredentialClaims } from './google-credential';

function token(payload: object): string {
  const encode = (value: object) =>
    btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(value))))
      .replace(/\+/g, '-')
      .replace(/\//g, '_')
      .replace(/=+$/, '');
  return `${encode({ alg: 'RS256' })}.${encode(payload)}.signature`;
}

describe('readGoogleCredentialClaims', () => {
  it('reads email and a non-latin name for display', () => {
    const claims = readGoogleCredentialClaims(
      token({ email: 'ahmed@example.com', name: 'أحمد محمد السيد' }),
    );
    expect(claims).toEqual({ email: 'ahmed@example.com', name: 'أحمد محمد السيد' });
  });

  it('returns null fields for missing claims', () => {
    expect(readGoogleCredentialClaims(token({ sub: '1' }))).toEqual({ email: null, name: null });
  });

  it('returns null for malformed credentials', () => {
    expect(readGoogleCredentialClaims('not-a-jwt')).toBeNull();
    expect(readGoogleCredentialClaims('a.%%%.c')).toBeNull();
  });
});
