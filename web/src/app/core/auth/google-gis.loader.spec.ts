import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { GOOGLE_GIS_SCRIPT_URL, GoogleGisLoader } from './google-gis.loader';

type WindowWithGoogle = Window & { google?: unknown };

describe('GoogleGisLoader', () => {
  let loader: GoogleGisLoader;
  let doc: Document;

  const scripts = () =>
    Array.from(doc.querySelectorAll<HTMLScriptElement>(`script[src="${GOOGLE_GIS_SCRIPT_URL}"]`));

  beforeEach(() => {
    loader = TestBed.inject(GoogleGisLoader);
    doc = TestBed.inject(DOCUMENT);
    delete (doc.defaultView as WindowWithGoogle).google;
    scripts().forEach((s) => s.remove());
  });

  afterEach(() => {
    delete (doc.defaultView as WindowWithGoogle).google;
    scripts().forEach((s) => s.remove());
  });

  it('does not touch the network until load() is called', () => {
    expect(scripts().length).toBe(0);
  });

  it('adds the script once and resolves when the library is available', async () => {
    const first = loader.load();
    const second = loader.load();
    expect(scripts().length).toBe(1);

    const api = { initialize: vi.fn(), renderButton: vi.fn(), disableAutoSelect: vi.fn() };
    (doc.defaultView as WindowWithGoogle).google = { accounts: { id: api } };
    scripts()[0]!.onload?.(new Event('load'));

    await expect(first).resolves.toBe(api);
    await expect(second).resolves.toBe(api);
  });

  it('reuses the library without adding a script when already present', async () => {
    const api = { initialize: vi.fn(), renderButton: vi.fn(), disableAutoSelect: vi.fn() };
    (doc.defaultView as WindowWithGoogle).google = { accounts: { id: api } };
    await expect(loader.load()).resolves.toBe(api);
    expect(scripts().length).toBe(0);
  });

  it('rejects when the script fails and can be retried', async () => {
    const failed = loader.load();
    scripts()[0]!.onerror?.(new Event('error'));
    await expect(failed).rejects.toThrow();
    expect(scripts().length).toBe(0);

    const retry = loader.load();
    expect(scripts().length).toBe(1);
    const api = { initialize: vi.fn(), renderButton: vi.fn(), disableAutoSelect: vi.fn() };
    (doc.defaultView as WindowWithGoogle).google = { accounts: { id: api } };
    scripts()[0]!.onload?.(new Event('load'));
    await expect(retry).resolves.toBe(api);
  });

  it('disableAutoSelect is a no-op until the script was loaded', () => {
    expect(() => loader.disableAutoSelect()).not.toThrow();
    const api = { initialize: vi.fn(), renderButton: vi.fn(), disableAutoSelect: vi.fn() };
    (doc.defaultView as WindowWithGoogle).google = { accounts: { id: api } };
    loader.disableAutoSelect();
    expect(api.disableAutoSelect).toHaveBeenCalledOnce();
  });
});
