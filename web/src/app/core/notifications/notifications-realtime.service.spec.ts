import { resolveHubUrl } from './notifications-realtime.service';

describe('resolveHubUrl', () => {
  it('strips /api/v1 from absolute API base', () => {
    expect(resolveHubUrl('https://kadir.runasp.net/api/v1')).toBe(
      'https://kadir.runasp.net/hubs/notifications',
    );
  });

  it('uses relative hub path for proxied local API', () => {
    expect(resolveHubUrl('/api/v1')).toBe('/hubs/notifications');
  });
});
