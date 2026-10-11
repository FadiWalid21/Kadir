import { Injectable, signal } from '@angular/core';
import { GoogleCompanyOption } from '../api/auth.models';

export interface CompanySelectionState {
  selectionToken: string;
  companies: GoogleCompanyOption[];
  /** Display-only Google email for the verified chip; may be null. */
  email: string | null;
}

/**
 * Holds the Google company-selection state in memory only.
 * It must never reach the URL, localStorage, or sessionStorage: a reload clears it.
 */
@Injectable({ providedIn: 'root' })
export class CompanySelectionService {
  private readonly stateSignal = signal<CompanySelectionState | null>(null);

  readonly state = this.stateSignal.asReadonly();

  set(state: CompanySelectionState): void {
    this.stateSignal.set(state);
  }

  clear(): void {
    this.stateSignal.set(null);
  }
}
