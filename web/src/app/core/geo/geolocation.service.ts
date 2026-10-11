import { Injectable } from '@angular/core';

export type GeoPermissionState = 'prompt' | 'granted' | 'denied' | 'unsupported';

export interface GeoFix {
  latitude: number;
  longitude: number;
  accuracyM: number;
  positionAgeMs: number;
}

export type GeoLocateResult =
  | { ok: true; fix: GeoFix }
  | { ok: false; reason: 'unsupported' | 'denied' | 'timeout' | 'unavailable' };

@Injectable({ providedIn: 'root' })
export class GeolocationService {
  private readonly maxAgeMs = 10_000;
  private readonly watchBudgetMs = 8_000;
  private readonly highAccuracy = true;

  async getPermissionState(): Promise<GeoPermissionState> {
    if (typeof navigator === 'undefined' || !('geolocation' in navigator)) {
      return 'unsupported';
    }
    const perms = (navigator as Navigator & { permissions?: Permissions }).permissions;
    if (!perms?.query) {
      return 'prompt';
    }
    try {
      const status = await perms.query({ name: 'geolocation' as PermissionName });
      if (status.state === 'granted' || status.state === 'denied') {
        return status.state;
      }
      return 'prompt';
    } catch {
      return 'prompt';
    }
  }

  /**
   * High-accuracy locate on punch: first fix within accuracy limit, else watch ≤8s for best, then clearWatch.
   * Never persists location.
   */
  async locateForPunch(maxAccuracyM: number): Promise<GeoLocateResult> {
    if (typeof navigator === 'undefined' || !navigator.geolocation) {
      return { ok: false, reason: 'unsupported' };
    }

    try {
      const first = await this.getCurrentPosition();
      if (first.accuracyM <= maxAccuracyM) {
        return { ok: true, fix: first };
      }
      const watched = await this.watchBest(maxAccuracyM, first);
      return { ok: true, fix: watched };
    } catch (err) {
      const code = (err as GeolocationPositionError | undefined)?.code;
      if (code === 1) {
        return { ok: false, reason: 'denied' };
      }
      if (code === 3) {
        return { ok: false, reason: 'timeout' };
      }
      return { ok: false, reason: 'unavailable' };
    }
  }

  /** Preview distance only when permission already granted — never prompts. */
  async tryPreviewFix(): Promise<GeoFix | null> {
    const state = await this.getPermissionState();
    if (state !== 'granted') {
      return null;
    }
    try {
      return await this.getCurrentPosition({ maximumAge: 30_000, timeout: 5_000 });
    } catch {
      return null;
    }
  }

  private getCurrentPosition(
    options?: PositionOptions,
  ): Promise<GeoFix> {
    return new Promise((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(
        (pos) => resolve(this.toFix(pos)),
        (err) => reject(err),
        {
          enableHighAccuracy: this.highAccuracy,
          maximumAge: options?.maximumAge ?? 0,
          timeout: options?.timeout ?? this.maxAgeMs,
        },
      );
    });
  }

  private watchBest(maxAccuracyM: number, seed: GeoFix): Promise<GeoFix> {
    return new Promise((resolve) => {
      let best = seed;
      let watchId = 0;
      const done = () => {
        navigator.geolocation.clearWatch(watchId);
        resolve(best);
      };
      const timer = setTimeout(done, this.watchBudgetMs);
      watchId = navigator.geolocation.watchPosition(
        (pos) => {
          const fix = this.toFix(pos);
          if (fix.accuracyM < best.accuracyM) {
            best = fix;
          }
          if (fix.accuracyM <= maxAccuracyM) {
            clearTimeout(timer);
            done();
          }
        },
        () => {
          clearTimeout(timer);
          done();
        },
        { enableHighAccuracy: this.highAccuracy, maximumAge: 0, timeout: this.watchBudgetMs },
      );
    });
  }

  private toFix(pos: GeolocationPosition): GeoFix {
    const stamped = pos.timestamp;
    const age = Math.max(0, Date.now() - stamped);
    return {
      latitude: pos.coords.latitude,
      longitude: pos.coords.longitude,
      accuracyM: pos.coords.accuracy,
      positionAgeMs: age,
    };
  }
}
