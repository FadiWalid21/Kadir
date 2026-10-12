import { describe, expect, it } from 'vitest';
import { resolveNotificationDeepLink } from './notification-deep-link';

describe('resolveNotificationDeepLink', () => {
  it('allows known relative paths', () => {
    expect(resolveNotificationDeepLink('/leave/approvals')).toBe('/leave/approvals');
    expect(resolveNotificationDeepLink('/notifications')).toBe('/notifications');
  });

  it('rejects unsafe paths', () => {
    expect(resolveNotificationDeepLink('https://evil.test/x')).toBe('/');
    expect(resolveNotificationDeepLink('//evil.test')).toBe('/');
    expect(resolveNotificationDeepLink('/leave/../admin')).toBe('/');
    expect(resolveNotificationDeepLink('/unknown')).toBe('/');
    expect(resolveNotificationDeepLink(null)).toBe('/');
  });
});
