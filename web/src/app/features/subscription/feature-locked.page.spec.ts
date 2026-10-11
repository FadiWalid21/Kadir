import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { provideTestTransloco } from '../../testing/transloco-testing';
import { FeatureLockedPage } from './feature-locked.page';

describe('FeatureLockedPage', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FeatureLockedPage],
      providers: [provideRouter([]), provideTestTransloco()],
    }).compileComponents();
  });

  it('renders lock message in Arabic', () => {
    const fixture = TestBed.createComponent(FeatureLockedPage);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('ميزة مقفلة');
    expect(fixture.nativeElement.textContent).toContain('عرض الخطط');
  });
});
