/** Relative paths the app may open from a push notification click. */
const ALLOWED = new Set([
  '/',
  '/notifications',
  '/leave/approvals',
  '/leave/me',
  '/attendance/corrections',
]);

/**
 * Returns an allow-listed relative path, or `/` when the payload path is missing/unsafe.
 * Rejects absolute URLs, protocol-relative URLs, and path traversal.
 */
export function resolveNotificationDeepLink(raw: unknown): string {
  if (typeof raw !== 'string' || !raw.trim()) {
    return '/';
  }
  const path = raw.trim().split('?', 1)[0] ?? '/';
  if (
    !path.startsWith('/') ||
    path.startsWith('//') ||
    path.includes('://') ||
    path.includes('\\') ||
    path.includes('..')
  ) {
    return '/';
  }
  return ALLOWED.has(path) ? path : '/';
}
