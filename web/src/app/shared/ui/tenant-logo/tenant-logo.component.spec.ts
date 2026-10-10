import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { TenantLogoComponent } from './tenant-logo.component';

@Component({
  selector: 'kadr-tenant-logo-host',
  imports: [TenantLogoComponent],
  template: `
    <kadr-tenant-logo
      [logoUrl]="logoUrl()"
      [logoVersion]="logoVersion()"
      [fallbackName]="fallbackName"
      size="md"
    />
  `,
})
class TenantLogoHost {
  readonly logoUrl = signal<string | null>('/api/v1/files/11111111-1111-1111-1111-111111111111');
  readonly logoVersion = signal<string | number>(1);
  fallbackName = 'شركة الاختبار';
}

describe('TenantLogoComponent', () => {
  let http: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TenantLogoHost],
      providers: [provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('loads logo via HttpClient blob and shows img', async () => {
    const fixture = TestBed.createComponent(TenantLogoHost);
    fixture.detectChanges();

    const req = http.expectOne((r) => r.url.startsWith('/api/v1/files/'));
    expect(req.request.responseType).toBe('blob');
    const bytes = new Uint8Array([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
    req.flush(new Blob([bytes], { type: 'image/png' }));

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const img = fixture.nativeElement.querySelector('img') as HTMLImageElement | null;
    expect(img).toBeTruthy();
    expect(img!.getAttribute('src') ?? img!.src).toMatch(/^blob:/);
    http.verify();
  });

  it('falls back to avatar on 401', async () => {
    const fixture = TestBed.createComponent(TenantLogoHost);
    fixture.detectChanges();

    const req = http.expectOne((r) => r.url.startsWith('/api/v1/files/'));
    req.flush(new Blob(['unauthorized'], { type: 'application/problem+json' }), {
      status: 401,
      statusText: 'Unauthorized',
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('kadr-avatar')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('img[src^="blob:"]')).toBeFalsy();
    http.verify();
  });

  it('falls back to avatar on 404', async () => {
    const fixture = TestBed.createComponent(TenantLogoHost);
    fixture.detectChanges();

    const req = http.expectOne((r) => r.url.startsWith('/api/v1/files/'));
    req.flush(new Blob(['missing'], { type: 'application/problem+json' }), {
      status: 404,
      statusText: 'Not Found',
    });

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('kadr-avatar')).toBeTruthy();
    http.verify();
  });

  it('revokes previous object URL when logoUrl changes', async () => {
    const revoke = vi.spyOn(URL, 'revokeObjectURL');
    const fixture = TestBed.createComponent(TenantLogoHost);
    const host = fixture.componentInstance;
    fixture.detectChanges();

    const first = http.expectOne((r) => r.url.includes('11111111-1111-1111-1111-111111111111'));
    first.flush(new Blob([new Uint8Array([1, 2, 3])], { type: 'image/png' }));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    host.logoUrl.set('/api/v1/files/22222222-2222-2222-2222-222222222222');
    host.logoVersion.set(2);
    fixture.detectChanges();

    expect(revoke).toHaveBeenCalled();
    const second = http.expectOne((r) => r.url.includes('22222222-2222-2222-2222-222222222222'));
    second.flush(new Blob([new Uint8Array([4, 5, 6])], { type: 'image/png' }));
    fixture.detectChanges();
    http.verify();
  });
});
