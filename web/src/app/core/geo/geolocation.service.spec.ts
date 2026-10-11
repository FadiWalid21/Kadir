import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GeolocationService } from './geolocation.service';

describe('GeolocationService', () => {
  let service: GeolocationService;
  let getCurrentPosition: ReturnType<typeof vi.fn>;
  let watchPosition: ReturnType<typeof vi.fn>;
  let clearWatch: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.useFakeTimers();
    service = new GeolocationService();
    getCurrentPosition = vi.fn();
    watchPosition = vi.fn();
    clearWatch = vi.fn();
    Object.defineProperty(globalThis.navigator, 'geolocation', {
      configurable: true,
      value: { getCurrentPosition, watchPosition, clearWatch },
    });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('returns first fix when accuracy is within limit', async () => {
    getCurrentPosition.mockImplementation((success: PositionCallback) => {
      success({
        coords: { latitude: 30, longitude: 31, accuracy: 20, altitude: null, altitudeAccuracy: null, heading: null, speed: null },
        timestamp: Date.now(),
      } as GeolocationPosition);
    });

    const result = await service.locateForPunch(100);
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.fix.accuracyM).toBe(20);
      expect(result.fix.positionAgeMs).toBeGreaterThanOrEqual(0);
    }
    expect(watchPosition).not.toHaveBeenCalled();
  });

  it('watches then clearWatch when first fix is inaccurate', async () => {
    getCurrentPosition.mockImplementation((success: PositionCallback) => {
      success({
        coords: { latitude: 30, longitude: 31, accuracy: 200, altitude: null, altitudeAccuracy: null, heading: null, speed: null },
        timestamp: Date.now(),
      } as GeolocationPosition);
    });
    watchPosition.mockImplementation((success: PositionCallback) => {
      success({
        coords: { latitude: 30, longitude: 31, accuracy: 40, altitude: null, altitudeAccuracy: null, heading: null, speed: null },
        timestamp: Date.now(),
      } as GeolocationPosition);
      return 7;
    });

    const promise = service.locateForPunch(100);
    await vi.runAllTimersAsync();
    const result = await promise;
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.fix.accuracyM).toBe(40);
    }
    expect(clearWatch).toHaveBeenCalled();
  });

  it('maps permission denied', async () => {
    getCurrentPosition.mockImplementation((_s: PositionCallback, err: PositionErrorCallback) => {
      err({ code: 1, message: 'denied', PERMISSION_DENIED: 1, POSITION_UNAVAILABLE: 2, TIMEOUT: 3 } as GeolocationPositionError);
    });
    const result = await service.locateForPunch(100);
    expect(result).toEqual({ ok: false, reason: 'denied' });
  });
});
